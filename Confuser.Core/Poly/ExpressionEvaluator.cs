using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core.Poly.Expressions;

namespace Confuser.Core.Poly
{
    public class ExpressionEvaluator
    {
        public static int Evaluate(Expression exp, int var)
        {
            if (exp is ConstantExpression)
            {
                ConstantExpression tExp = exp as ConstantExpression;
                return (int)(exp as ConstantExpression).Value;
            }
            else if (exp is VariableExpression)
            {
                return var;
            }
            else if (exp is AddExpression)
            {
                AddExpression nExp = (AddExpression)exp;
                return Evaluate(nExp.OperandA, var) + Evaluate(nExp.OperandB, var);
            }
            else if (exp is SubExpression)
            {
                SubExpression nExp = (SubExpression)exp;
                return Evaluate(nExp.OperandA, var) - Evaluate(nExp.OperandB, var);
            }
            else if (exp is MulExpression)
            {
                MulExpression nExp = (MulExpression)exp;
                return Evaluate(nExp.OperandA, var) * Evaluate(nExp.OperandB, var);
            }
            else if (exp is DivExpression)
            {
                DivExpression nExp = (DivExpression)exp;
                return Evaluate(nExp.OperandA, var) / Evaluate(nExp.OperandB, var);
            }
            else if (exp is NegExpression)
            {
                NegExpression nExp = (NegExpression)exp;
                return -Evaluate(nExp.Value, var);
            }
            else if (exp is InvExpression)
            {
                InvExpression nExp = (InvExpression)exp;
                return ~Evaluate(nExp.Value, var);
            }
            else if (exp is XorExpression)
            {
                XorExpression nExp = (XorExpression)exp;
                return Evaluate(nExp.OperandA, var) ^ Evaluate(nExp.OperandB, var);
            }
            throw new NotSupportedException();
        }
    }
}