using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Confuser.Core.Poly.Expressions
{
    // DO NOT USE THIS CLASS IT IS NOT CORRECT AND SHOULD NOT BE USED!!!




















    public class AbsExpression : Expression
    {
        public Expression OperandA { get; set; }
        public Expression OperandB { get; set; }
        public MethodReference AbsFunction;
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
           // if (AbsFunction == null)
           //     AbsFunction = gen.Module.Import(typeof(Math).GetMethod("Abs", new Type[] { typeof(Double) }));

            int a = rand.Next(0, 100) > 15 ? level : 0;
            int b = rand.Next(0, 100) > 15 ? level : 0;

            // Random choice based on equal or not
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
            if (OperandA.HasVariable)       //y = x + 1
            {
                return new SubExpression()  //x = y - 1
                {
                    OperandA = arg,
                    OperandB = OperandB
                };
            }
            else if (OperandB.HasVariable)  //y = 1 + x
            {
                return new SubExpression()  //x = y - 1
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
            return string.Format("({0} + Math.Abs({1}))", OperandA, OperandB);
        }
    }
}
