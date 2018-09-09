using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Confuser.Core.Poly.Expressions
{
    public class VariableExpression : Expression
    {
        public override IEnumerable<Expression> Children { get { return Enumerable.Empty<Expression>(); } }
        public override void Generate(ExpressionGenerator gen, int level, Random rand)
        {
        }
        public override Expression GenerateInverse(Expression arg)
        {
            return arg;
        }

        public override Expression GetVariableExpression() { return this; }
        public override bool HasVariable { get { return true; } }

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
            return "Var";
        }
    }
}
