using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using Confuser.Core.Poly.Expressions;

namespace Confuser.Core.Poly.Visitors
{
    public class ReflectionVisitor : ExpressionVisitor
    {
        List<Tuple<OpCode, int?>> insts;
        DynamicMethod dm;

        public ReflectionVisitor(Expression exp)
        {
            dm = new DynamicMethod("", typeof(int), new Type[] { typeof(int) });
            ILGenerator gen = dm.GetILGenerator();
            insts = new List<Tuple<OpCode, int?>>();
            exp.VisitPostOrder(this);
            insts.Reverse();
            foreach (var i in insts)
            {
                if (i.Item2 == null)
                    gen.Emit(i.Item1);
                else
                    gen.Emit(i.Item1, i.Item2.Value);
            }
            gen.Emit(OpCodes.Ret);
        }

        public object Eval(object var)
        {
            return (int)dm.Invoke(null, new object[] { (int)var });
        }

        public override void VisitPostOrder(Expression exp)
        {
            if (exp is ConstantExpression)
                insts.Add(new Tuple<OpCode, int?>(OpCodes.Ldc_I4, (exp as ConstantExpression).Value));

            else if (exp is VariableExpression)
                insts.Add(new Tuple<OpCode, int?>(OpCodes.Ldarg_0, null));

            else if (exp is AddExpression)
                insts.Add(new Tuple<OpCode, int?>(OpCodes.Add, null));

            else if (exp is SubExpression)
                insts.Add(new Tuple<OpCode, int?>(OpCodes.Sub, null));

            else if (exp is MulExpression)
                insts.Add(new Tuple<OpCode, int?>(OpCodes.Mul, null));

            else if (exp is DivExpression)
                insts.Add(new Tuple<OpCode, int?>(OpCodes.Div, null));

            else if (exp is NegExpression)
                insts.Add(new Tuple<OpCode, int?>(OpCodes.Neg, null));

            else if (exp is InvExpression)
                insts.Add(new Tuple<OpCode, int?>(OpCodes.Not, null));

            else if (exp is XorExpression)
                insts.Add(new Tuple<OpCode, int?>(OpCodes.Xor, null));
        }

        public override void VisitPreOrder(Expression exp)
        {
            //
        }
    }
}
