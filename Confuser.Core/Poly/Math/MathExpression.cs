using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Confuser.Core.Poly.Math
{
    public class MathExpression
    {
        public List<Instruction> Get(Random rand, double realNumber, double offsetNumber, string mathOper,  out string resultSummary, out double resultNumber)
        {
            double result = 0;
            Instruction op = Instruction.Create(OpCodes.Sub);
            switch (rand.Next(0, 5))
            {
                // Continual problems with Sinh/sin/tan/tanh/atan/atan2 on mul/div
                case 1:
                    //if (mathOper.ToLower().Contains("tan"))
                    //    return Get(realNumber, offsetNumber, mathOper, out resultSummary, out resultNumber);
                    result = realNumber * offsetNumber;
                    op = Instruction.Create(OpCodes.Div);
                    resultSummary = string.Format("{0} * {1} = {2} (inverse: {0} / {4}({1}) = {3})", realNumber, offsetNumber, result, realNumber, mathOper);
                    break;
                case 2:
                    result = realNumber - offsetNumber;
                    op = Instruction.Create(OpCodes.Add);
                    resultSummary = string.Format("{0} - {1} = {2} (inverse: {0} + {4}({1} = {3}))", realNumber, offsetNumber, result, realNumber, mathOper);
                    break;

                // Problem with 7, cannot use 7 (Atan) with this.
                case 3:
                    //if (mathOper.ToLower().Contains("tan"))
                    //    return Get(realNumber, offsetNumber, mathOper, out resultSummary, out resultNumber);
                    result = realNumber / offsetNumber;
                    op = Instruction.Create(OpCodes.Mul);
                    resultSummary = string.Format("{0} / {1} = {2} (inverse: {0} * {4}({1} = {3}))", realNumber, offsetNumber, result, realNumber, mathOper);
                    break;
                case 4:
                    result = realNumber + offsetNumber;
                    resultSummary = string.Format("{0} + {1} = {2} (inverse: {0} - {4}({1} = {3}))", realNumber, offsetNumber, result, realNumber, mathOper);
                    break;
                default:
                    result = realNumber + offsetNumber;
                    resultSummary = string.Format("{0} + {1} = {2} (inverse: {0} - {4}({1} = {3}))", realNumber, offsetNumber, result, realNumber, mathOper);
                    break;
            }
            resultNumber = result;

            return new List<Instruction>() { op, Instruction.Create(OpCodes.Ldc_R8, (double)offsetNumber), Instruction.Create(OpCodes.Ldc_R8, (double)result) };
        }
    }
}
