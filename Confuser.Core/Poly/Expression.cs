using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Confuser.Core.Poly
{
    public abstract class Expression
    {
        public abstract void VisitPostOrder(ExpressionVisitor visitor);
        public abstract void VisitPreOrder(ExpressionVisitor visitor);
        public abstract void Generate(ExpressionGenerator gen, int level, Random rand);
        protected Expression Gen(ExpressionGenerator gen, int level, Random rand)
        {
            return gen.Generate(this, level, rand);
        }
        public abstract Expression GenerateInverse(Expression arg);

        public Expression Parent { get; set; }
        public abstract IEnumerable<Expression> Children { get; }

        public virtual Expression GetVariableExpression()
        {
            Expression ret = null;
            foreach (var i in Children)
            {
                ret = i.GetVariableExpression();
                if (ret != null) return ret;
            } return ret;
        }
        public virtual bool HasVariable
        {
            get
            {
                foreach (var i in Children)
                    if (i.HasVariable)
                        return true;
                return false;
            }
        }

        public abstract override string ToString();

        Dictionary<object, object> dict = new Dictionary<object, object>();
        public Dictionary<object, object> Annotations { get { return dict; } }
    }
}
