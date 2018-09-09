using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil.Cil;
using Confuser.Core.Poly.Expressions;

namespace Confuser.Core.Poly.Visitors
{
    public class CecilVisitor : ExpressionVisitor
    {
        List<Instruction> insts;
        Instruction[] args;

        public CecilVisitor(Expression exp, Instruction[] args)
        {
            
            insts = new List<Instruction>();
            this.args = args;
            exp.VisitPostOrder(this);
        }

        public Instruction[] GetInstructions()
        {
            return insts.ToArray();
        }

        public override void VisitPostOrder(Expression exp)
        {
            if (exp is ConstantExpression)
                insts.Add(Instruction.Create(OpCodes.Ldc_I4, (int)(exp as ConstantExpression).Value));

            // Add the argument supplied in the args array (the load for the ldc_i4, the inputted value).
            // Var expression is a placeholder for the variable the user is meant to supply.
            else if (exp is VariableExpression)
                insts.AddRange(args);

            else if (exp is AddExpression)
                insts.Add(Instruction.Create(OpCodes.Add));

            else if (exp is SubExpression)
                insts.Add(Instruction.Create(OpCodes.Sub));

            else if (exp is MulExpression)
                insts.Add(Instruction.Create(OpCodes.Mul));

            else if (exp is DivExpression)
                insts.Add(Instruction.Create(OpCodes.Div));

            else if (exp is NegExpression)
                insts.Add(Instruction.Create(OpCodes.Neg));

            else if (exp is InvExpression)
                insts.Add(Instruction.Create(OpCodes.Not));

            else if (exp is XorExpression)
                insts.Add(Instruction.Create(OpCodes.Xor));
        }

        public override void VisitPreOrder(Expression exp)
        {
            //
        }
    }
}
