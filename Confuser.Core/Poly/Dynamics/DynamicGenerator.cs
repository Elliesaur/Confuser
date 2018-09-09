using Confuser.Core.Poly.Math;
using Confuser.Core.Poly.Strings;
using Confuser.Core.Poly.Visitors;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Confuser.Core.Poly.Dynamics
{
    public class DynamicGenerator
    {
        public ModuleDefinition Module { get; set; }

        AssemblyDefinition injection { get; set; }

        private Random rand;

        private ExpressionGenerator ExpGen;
        private MathGenerator MathGen;
        public Instruction emit;

        public List<DynamicInfo> Dynamics = new List<DynamicInfo>();
        
        public DynamicGenerator(int seed, ModuleDefinition mod, bool useMath = false)
        {
            rand = new Random(seed);

            Module = mod;

            injection = AssemblyDefinition.ReadAssembly(typeof(Iid).Assembly.Location);
            injection.MainModule.ReadSymbols();

            ExpGen = new ExpressionGenerator(rand.Next(500000), Module);
            MathGen = new MathGenerator(rand.Next(500000), Module);

            emit = Instruction.Create(OpCodes.Call, Module.Import(typeof(System.Reflection.Emit.ILGenerator).GetMethod("Emit", new Type[] {
                typeof(System.Reflection.Emit.OpCode)
            })));

            int expNum = rand.Next(5, 50);
            for (int i = 0; i < expNum; i++)
            {
                ExpGen = new ExpressionGenerator(rand.Next(50000), Module);
                Expression ex = ExpGen.Generate(rand.Next(5, 14));

                int evald = ExpressionEvaluator.Evaluate(ex, 5);
                Expression exR = ExpressionInverser.InverseExpression(ex);

                CreateDynamic(ex, exR, 5, evald, useMath);
            }
        }

        
        public List<Instruction> Generate(MethodDefinition CurrentMethod, int target, OpCode instType, out DynamicInfo UsedInfo, bool useMath = false)
        {

            

            //FieldDefinition fDef = GenerateDynamic();


            List<Instruction> builder = new List<Instruction>();

            /*ExpGen = new ExpressionGenerator(rand.Next(50000), Module);
            Expression ex = ExpGen.Generate(5);
            int evald = ExpressionEvaluator.Evaluate(ex, target);
            Expression exR = ExpressionInverser.InverseExpression(ex);

            int myTarget = target + 2;

            int myTargetTest = ExpressionEvaluator.Evaluate(ex, myTarget);
            Expression exR2 = ExpressionInverser.InverseExpression(ex);
            int difference = myTargetTest - evald;



            DynamicInfo dynCall = CreateDynamic(ex, exR, target, evald, useMath);*/
            DynamicInfo dynCall = null;
            //while (dynCall.Wrapper.Methods.FirstOrDefault(x => x.Name == "Initialize"))
            if (CurrentMethod == null)
            {
                dynCall = Dynamics[rand.Next(0, Dynamics.Count - 1)];
            }
            else
            {
                do
                {
                    dynCall = Dynamics[rand.Next(0, Dynamics.Count - 1)];
                }
                while (dynCall.Wrapper.Methods.FirstOrDefault(x => x.Name == "DYN___Initialize") == CurrentMethod);
            }

            int evald = ExpressionEvaluator.Evaluate(dynCall.Exp, target);


            builder.Add(Instruction.Create(OpCodes.Ldsfld, dynCall.DelegateInstance));
            builder.Add(Instruction.Create(OpCodes.Ldc_I4, evald));
            builder.Add(Instruction.Create(OpCodes.Conv_R8));
            builder.Add(Instruction.Create(OpCodes.Callvirt, dynCall.DelegateType.Methods.FirstOrDefault(x => x.Name == "Invoke")));


            // Convert here...
            if (instType == OpCodes.Ldc_I4)
            {
                builder.Add(Instruction.Create(OpCodes.Conv_I4));
            }
            else if (instType == OpCodes.Ldc_R4)
            {
                builder.Add(Instruction.Create(OpCodes.Conv_R4));
            }
            else if (instType == OpCodes.Ldc_I8)
            {
                builder.Add(Instruction.Create(OpCodes.Conv_I8));
            }
            else if (instType == OpCodes.Ldc_I4_0)
            {
                builder.Add(Instruction.Create(OpCodes.Conv_U4));
            }
            else if (instType == OpCodes.Ldc_I4_1)
            {
                builder.Add(Instruction.Create(OpCodes.Conv_U8));
            }
            else
            {
                // Double, so no worries.
            }
            UsedInfo = dynCall;
            return builder;
        }

        private List<Instruction> GenerateFor(MethodDefinition md, int target, bool useMath)
        {
            List<Instruction> builder = new List<Instruction>();

            if (useMath || Dynamics.Count == 0)
            {
                // Testing if math is the real issue.
                //builder.Add(Instruction.Create(OpCodes.Ldc_I4, target));
                builder.AddRange(MathGen.GenerateLevels(target, OpCodes.Ldc_I4, rand.Next(1, 3)));
            }
            else
            {
                DynamicInfo dCall = null;
                builder.AddRange(Generate(md, target, OpCodes.Ldc_I4, out dCall, true));
            }
            return builder;
        }

        public DynamicInfo CreateDynamic(Expression exP, Expression exR, int input, int output, bool useMath)
        {
            TypeDefinition injType = CecilHelper.Inject(Module, injection.MainModule.GetType("PolyInjection"));
            injType.Name = ObfuscationHelper.Instance.GetRandomName();

            FieldDefinition dmInstance = injType.Fields.FirstOrDefault(x => x.Name == "dm");
            FieldDefinition delInstance = injType.Fields.FirstOrDefault(x => x.Name == "Invoke");

            MethodDefinition initMd = injType.Methods.FirstOrDefault(x => x.Name == "Initialize");

            initMd.Name = "DYN___" + initMd.Name;

            ILProcessor ilp = initMd.Body.GetILProcessor();
            for (int i = 0; i < initMd.Body.Instructions.Count; i++)
            {
                Instruction inst = initMd.Body.Instructions[i];
                if (inst.Operand is FieldReference)
                {
                    if ((inst.Operand as FieldReference).Name == "dm")
                    {
                        inst.Operand = dmInstance;
                    }
                    else if ((inst.Operand as FieldReference).Name == "Invoke")
                    {
                        inst.Operand = delInstance;
                    }
                    if ((inst.Operand as FieldReference).Name == "MethodName")
                    {
                        inst.OpCode = OpCodes.Ldstr;
                        inst.Operand = ObfuscationHelper.GetRandomString(rand.Next(1, 5));
                    }

                }
                
            }

            // Ldarg_0 is evald
            CecilVisitor cv = new Visitors.CecilVisitor(exR, new Instruction[] { Instruction.Create(OpCodes.Ldarg_0) });

            List<Instruction> cvInsts = cv.GetInstructions().ToList();
            for (int i = 0; i < cvInsts.Count; i++)
            {
                Instruction cvInst = cvInsts[i];
                if (cvInst.OpCode == OpCodes.Ldarg_0)
                {
                    cvInsts.Insert(i + 1, Instruction.Create(OpCodes.Conv_I4));
                    i += 1;
                }
            }


            for (int i = 0; i < initMd.Body.Instructions.Count; i++)
            {
                Instruction inst = initMd.Body.Instructions[i];
                if (inst.OpCode == OpCodes.Ldsfld && (inst.Operand as FieldReference).Name == "Conv_R8")
                {
                    // Go back two, start inserting
                    i -= 2;
                    Instruction real = initMd.Body.Instructions[i];

                    List<Instruction> ei = ConvertToEmit(initMd, cvInsts, useMath);//new List<Instruction>();

                    ei.Reverse();
                    foreach (Instruction a in ei)
                    {
                        initMd.Body.Instructions.Insert(i + 1, a);
                    }

                    initMd.Body.ComputeHeader();
                    initMd.Body.ComputeOffsets();
                    initMd.Body.OptimizeMacros();
                    
                    break;
                }
            }


            

            Module.Types.Add(injType);

            DynamicInfo di = new DynamicInfo();
            di.DelegateInstance = delInstance;
            di.DelegateType = injType.NestedTypes[0];
            di.Wrapper = injType;
            di.Input = input;
            di.Output = output;
            di.Exp = exP;
            Dynamics.Add(di);

            return di;
        }

        private List<Instruction> ConvertToEmit(MethodDefinition md, List<Instruction> toProcess, bool useMath)
        {
            List<Instruction> ei = new List<Instruction>();

            foreach (Instruction a in toProcess)
            {

                // Load ilgen
                ei.Add(Instruction.Create(OpCodes.Ldloc_0));

                // Load opcode
                ei.Add(Instruction.Create(OpCodes.Ldsfld, Module.Import(typeof(System.Reflection.Emit.OpCodes).GetField(a.OpCode.Code.ToString()))));

                if (a.Operand != null)
                {
                    Type secParam = null;
                    if (a.Operand is int)
                    {
                        //if (rand.Next(0, 100) % 2 == 0)
                        if (Dynamics.Count > 0)
                        {

                        }
                            ei.AddRange(GenerateFor(md, (int)a.Operand, useMath));
                        //else
                        //    ei.Add(a.Clone());
                        secParam = typeof(Int32);
                    }

                    ei.Add(Instruction.Create(OpCodes.Call, Module.Import(typeof(System.Reflection.Emit.ILGenerator).GetMethod("Emit", new Type[] {
                            typeof(System.Reflection.Emit.OpCode),
                            secParam
                        }))));

                }
                else
                {
                    ei.Add(emit.Clone());
                }

            }

            return ei;
        }

        public class DynamicInfo
        {
            public FieldDefinition DelegateInstance;
            public TypeDefinition DelegateType;

            public TypeDefinition Wrapper;

            public Expression Exp;

            public int Output;
            public int Input;

            public override string ToString()
            {
                return string.Format("{0} - {1}", Wrapper, Wrapper.Methods[1]);
            }
        }
    }
}
