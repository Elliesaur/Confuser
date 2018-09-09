using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Confuser.Core.Poly.Expressions
{
    public class MulExpression : Expression
    {
        public Expression OperandA { get; set; }
        public Expression OperandB { get; set; }
        public override IEnumerable<Expression> Children
        {
            get
            {
                yield return OperandA;
                yield return OperandB;
            }
        }
        public override void Generate(ExpressionGenerator gen, int level, Random rand)
        {
            int a = rand.Next(0, 100) > 15 ? level : 0;
            int b = rand.Next(0, 100) > 15 ? level : 0;
            if (rand.Next() % 2 == 0)
            {
                OperandA = Gen(gen, a, rand);
                OperandB = Gen(gen, b, rand);
            }
            else
            {
                OperandB = Gen(gen, b, rand);
                OperandA = Gen(gen, a, rand);
            }
        }
        public override Expression GenerateInverse(Expression arg)
        {
            if (OperandA.HasVariable)       //y = x * 2
            {
                return new DivExpression()  //x = y / 2
                {
                    OperandA = arg,
                    OperandB = OperandB
                };
            }
            else if (OperandB.HasVariable)  //y = 2 * x
            {
                return new DivExpression()  //x = y / 2
                {
                    OperandA = arg,
                    OperandB = OperandA
                };
            }
            else
                throw new InvalidOperationException();
        }

        public override void VisitPostOrder(ExpressionVisitor visitor)
        {
            OperandA.VisitPostOrder(visitor);
            OperandB.VisitPostOrder(visitor);
            visitor.VisitPostOrder(this);
        }
        public override void VisitPreOrder(ExpressionVisitor visitor)
        {
            visitor.VisitPreOrder(this);
            OperandA.VisitPreOrder(visitor);
            OperandB.VisitPreOrder(visitor);
        }

        public override string ToString()
        {
            return string.Format("({0}*{1})", OperandA, OperandB);
        }
    }
}
