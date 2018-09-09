using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using M = System.Math;
namespace Confuser.Core.Poly.Math
{
    public class MathGenerator
    {
        public ModuleDefinition Module { get; set; }
        
        public string[] Operators = new string[] { "Abs", "Log10", "Log", "Sqrt" };

        private Random rand;
        public MathGenerator(int seed, ModuleDefinition mod)
        {
            rand = new Random(seed);
            
            Module = mod;
        }
        public List<Instruction> GenerateLevels(double origNum, OpCode instType, int maxLevels, bool innerRound = false, bool outerRound = true)
        {



            List<Instruction> total = new List<Instruction>();

            // Long, we do not handle them...
            if (instType == OpCodes.Ldc_I4_1 || instType == OpCodes.Ldc_I8)
            {
                throw new ArgumentException("Bad input number, cannot be long/ulong");
                //return new List<Instruction>() { Instruction.Create(OpCodes.Ldc_I8, (long)origNum) };
            }

            var ret = Generate(origNum, instType, outerRound);

            for (int x = 0; x < maxLevels; x++)
            {
                for (int i = 0; i < ret.Count; i++)
                {
                    Instruction inst = ret[i];
                    if ((inst.OpCode == OpCodes.Ldc_I4 || inst.OpCode == OpCodes.Ldc_R4 || inst.OpCode == OpCodes.Ldc_R8))
                    {
                        double real = -1;

                        if (!double.TryParse(inst.Operand.ToString(), out real))
                            continue;

                        var res = Generate(real, inst.OpCode, innerRound);

                        ret[i] = res[0];
                        if (innerRound)
                            ret.Insert(i + 1, res[4]);
                        ret.Insert(i + 1, res[3]);
                        ret.Insert(i + 1, res[2]);
                        ret.Insert(i + 1, res[1]);
                        

                        i += res.Count;
                        
                    }
                }
            }
            total.AddRange(ret);

            return total;
        }
        public List<Instruction> Generate(double origNum, OpCode instType, bool useRound = false)
        {
            List<Instruction> builder = new List<Instruction>();

            string mathOper = Operators[rand.Next(Operators.Length)];

            Instruction instMathOper = Instruction.Create(OpCodes.Call, Module.Import(typeof(M).GetMethod(mathOper, new Type[] { typeof(Double) })));

            double real = origNum;

            double random = 0.001;
            if (mathOper.EndsWith("h"))
            {
                random = RandomSmallDouble(8);
            }
            else if (mathOper.StartsWith("A"))
            {
                random = RandomSmallDouble(8);
            }
            else
            {
                random = RandomSmallDouble(8);
                //random = Generator.randomInt(5);
            }

            double sRandom = GetOffset(random, mathOper);

            if (double.IsNaN(sRandom) || double.IsInfinity(sRandom) || double.IsNegativeInfinity(sRandom) || double.IsPositiveInfinity(sRandom) || sRandom == 0 || random == 0)
            {
                return Generate(origNum, instType);
            }

            string resSummary = string.Empty;
            double res = 0;

            List<Instruction> results = new MathExpression().Get(rand, real, sRandom, mathOper, out resSummary, out res);

            if (double.IsNaN(res) || double.IsInfinity(res) || double.IsNegativeInfinity(res) || double.IsPositiveInfinity(res))
            {
                return Generate(origNum, instType);
            }

            double restored = 0;

            switch (results[0].OpCode.Code)
            {
                case Code.Div:
                    restored = res / GetOffset(random, mathOper);
                    break;
                case Code.Mul:
                    restored = res * GetOffset(random, mathOper);
                    break;
                case Code.Add:
                    restored = res + GetOffset(random, mathOper);
                    break;
                case Code.Sub:
                    restored = res - GetOffset(random, mathOper);
                    break;
            }

            results[1].Operand = (double)random;
           

            // Add generated
            builder.Add(results[2]);
            builder.Add(results[1]);
            builder.Add(instMathOper);
            builder.Add(results[0]);

            if (useRound)
                builder.Add(Instruction.Create(OpCodes.Call, Module.Import(typeof(M).GetMethod("Round", new Type[] { typeof(Double) }))));

            // Convert here...
            if (instType == OpCodes.Ldc_I4)
            {
                if (res > int.MaxValue || res < int.MinValue)
                    return Generate(origNum, instType);

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

            return builder;
        }
        private double RandomSmallDouble(double max)
        {
            double rnd = rand.NextDouble() * (max - 0.01) + max;
            return rnd;
        }

        private double GetOffset(double originalNumber, string type)
        {
            return (double)typeof(M).GetMethod(type, new Type[] { typeof(Double) }).Invoke(null, new object[] { originalNumber });
        }
    }
}
