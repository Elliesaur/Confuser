using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Confuser.Core.Poly.Expressions
{
    public class ConstantExpression : Expression
    {
        int val;
        public int Value { get { return val; } set { val = value; } }
        public override IEnumerable<Expression> Children { get { return Enumerable.Empty<Expression>(); } }
        public override void Generate(ExpressionGenerator gen, int level, Random rand)
        {
            val = rand.Next(2, 10);
        }
        public override Expression GenerateInverse(Expression arg)
        {
            return this;
        }

        public override void VisitPostOrder(ExpressionVisitor visitor)
        {
            visitor.VisitPostOrder(this);
        }
        public override void VisitPreOrder(ExpressionVisitor visitor)
        {
            visitor.VisitPreOrder(this);
        }

        public override string ToString()
        {
            return val.ToString();
        }
    }
}
