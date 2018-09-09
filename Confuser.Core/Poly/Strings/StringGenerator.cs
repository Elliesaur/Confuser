using Confuser.Core.Poly.Dynamics;
using Confuser.Core.Poly.Math;
using Confuser.Core.Poly.Visitors;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Confuser.Core.Poly.Strings
{
    public class StringGenerator
    {
        public ModuleDefinition Module { get; set; }

        private Random rand;

        private Instruction LengthCall;
        private Instruction StringEmpty;

        private ExpressionGenerator ExpGen;

        public MathGenerator MathGen;
        public DynamicGenerator DynGen;

        public StringGenerator(int seed, ModuleDefinition mod)
        {
            rand = new Random(seed);

            Module = mod;

            LengthCall = Instruction.Create(OpCodes.Call, Module.Import(typeof(String).GetMethod("get_Length", Type.EmptyTypes)));
            StringEmpty = Instruction.Create(OpCodes.Ldsfld, Module.Import(typeof(String).GetField("Empty")));

            ExpGen = new ExpressionGenerator(rand.Next(500000), Module);
            MathGen = new MathGenerator(rand.Next(500000), Module);
            DynGen = new DynamicGenerator(rand.Next(500000), Module);
        }
        public List<Instruction> GenerateLevels(MethodDefinition curMethod, int target, int maxLevels, int maxLength = 20, bool unsigned = false, ModuleDefinition ovModule = null)
        {
            if (ovModule != null)
            {
                DynGen.Module = ovModule;
                ExpGen.Module = ovModule;
                MathGen.Module = ovModule;
                Module = ovModule;

                LengthCall = Instruction.Create(OpCodes.Call, Module.Import(typeof(String).GetMethod("get_Length", Type.EmptyTypes)));
                
                // Avoid creating a new instance...
                // That way I can keep the previous stuff and still rename that.

                DynGen = new DynamicGenerator(rand.Next(500000), ovModule);
            }

            List<Instruction> builder = new List<Instruction>();

            builder.AddRange(Generate(curMethod, target, maxLength, unsigned));

            for (int x = 0; x < maxLevels; x++)
            {
                for (int i = 0; i < builder.Count; i++)
                {
                    Instruction inst = builder[i];

                    if (rand.Next(0, 100) % 2 == 0)
                        continue;

                    if (inst.OpCode == OpCodes.Ldc_I4)
                    {
                        int t = (int)inst.Operand;
                        if (t < 0)
                            continue;

                        var insts = Generate(curMethod, t, maxLength, unsigned);

                        builder[i] = insts[0];
                        var ee = insts.Skip(1).Reverse();
                        foreach (Instruction a in ee)
                        {
                            builder.Insert(i + 1, a);
                        }
                        i += insts.Count + 1;
                        //builder.AddRange(Generate(t, maxLength));
                    }
                }
            }

            return builder;
        }
        public List<Instruction> Generate(MethodDefinition CurrentMethod, int target, int maxLength = 20, bool unsigned = false)
        {
            List<Instruction> builder = new List<Instruction>();
            DynamicGenerator.DynamicInfo dCall;

            if (target < 0)
            {
                return new List<Instruction>() { Instruction.Create(OpCodes.Ldc_I4, target) };
            }
            if (target <= maxLength)
            {
                // Make one string and use the length of int in the place of the target.
                Instruction strInst = Instruction.Create(OpCodes.Ldstr, ObfuscationHelper.GetRandomString(target));

                if (target == 0 && rand.Next(0, 100) % 2 == 0)
                {
                    strInst = StringEmpty;
                }

                builder.Add(strInst);
                builder.Add(LengthCall);
            }
            else
            {
                // Make one string (random length) and use the length of it, + rest (perhaps expression gen for that?).
                
                int take = rand.Next(0, 20);

                Instruction strInst = Instruction.Create(OpCodes.Ldstr, ObfuscationHelper.GetRandomString(take));

                if (take == 0 && rand.Next(0, 100) % 2 == 0)
                {
                    strInst = StringEmpty;
                }

                int remainder = target - take;


                bool hasAdded = false;

                if (rand.Next(0, 100) % 2 == 0)
                {
                    builder.Add(strInst);
                    builder.Add(LengthCall);
                    hasAdded = true;
                }

                if (rand.Next(0, 100) % 2 == 0)
                {
                    Expression ex = ExpGen.Generate(5);

                    int evald = ExpressionEvaluator.Evaluate(ex, remainder);

                    Expression exR = ExpressionInverser.InverseExpression(ex);

                    CecilVisitor cv = new Visitors.CecilVisitor(exR, new Instruction[] { Instruction.Create(OpCodes.Ldc_I4, evald) });

                    builder.AddRange(cv.GetInstructions());
                }
                else if (rand.Next(0, 100) % 2 == 0  )
                {
                    builder.AddRange(MathGen.GenerateLevels(remainder, !unsigned ? OpCodes.Ldc_I4 : OpCodes.Ldc_I4_0, rand.Next(0, 3)));
                }
                else if (!CurrentMethod.Name.StartsWith("DYN___"))
                {
                    builder.AddRange(DynGen.Generate(CurrentMethod, remainder, !unsigned ? OpCodes.Ldc_I4 : OpCodes.Ldc_I4_0, out dCall));
                }
                else
                {
                    /*Expression ex = ExpGen.Generate(5);

                    int evald = ExpressionEvaluator.Evaluate(ex, remainder);

                    Expression exR = ExpressionInverser.InverseExpression(ex);

                    CecilVisitor cv = new Visitors.CecilVisitor(exR, new Instruction[] { Instruction.Create(OpCodes.Ldc_I4, evald) });

                    builder.AddRange(cv.GetInstructions());*/
                    builder.AddRange(MathGen.GenerateLevels(remainder, !unsigned ? OpCodes.Ldc_I4 : OpCodes.Ldc_I4_0, rand.Next(0, 4)));
                }

                if (!hasAdded)
                {
                    builder.Add(strInst);
                    builder.Add(LengthCall);
                }

                builder.Add(Instruction.Create(OpCodes.Add));

                if (unsigned)
                    builder.Add(Instruction.Create(OpCodes.Conv_U4));
            }


            return builder;
        }
    }
}
