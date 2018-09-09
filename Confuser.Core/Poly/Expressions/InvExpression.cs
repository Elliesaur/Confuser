using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Confuser.Core.Poly.Expressions
{
    public class InvExpression : Expression
    {
        public Expression Value { get; set; }
        public override IEnumerable<Expression> Children { get { yield return Value; } }
        public override void Generate(ExpressionGenerator gen, int level, Random rand)
        {
            int v = rand.Next(0, 100) > 15 ? level : 0;
            Value = Gen(gen, level, rand);
        }
        public override Expression GenerateInverse(Expression arg)
        {
            return new InvExpression() { Value = arg };
        }

        public override void VisitPostOrder(ExpressionVisitor visitor)
        {
            Value.VisitPostOrder(visitor);
            visitor.VisitPostOrder(this);
        }
        public override void VisitPreOrder(ExpressionVisitor visitor)
        {
            visitor.VisitPreOrder(this);
            Value.VisitPreOrder(visitor);
        }

        public override string ToString()
        {
            return string.Format("~{0}", Value);
        }
    }
}
