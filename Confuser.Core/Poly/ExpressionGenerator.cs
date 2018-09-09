using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core.Poly.Expressions;
using Mono.Cecil;

namespace Confuser.Core.Poly
{
    public class ExpressionGenerator
    {
        public int Seed { get; private set; }
        public Type[] ExpressionTypes { get; set; }
        public ModuleDefinition Module;
        public ExpressionGenerator(int seed, ModuleDefinition mod)
        {
            Seed = seed;
            Module = mod;
            ExpressionTypes = new Type[]
            {
                typeof(AddExpression),
                typeof(SubExpression),
                //typeof(MulExpression),
                //typeof(DivExpression),
                typeof(NegExpression),
                typeof(InvExpression),
                typeof(XorExpression)
            };
            
        }


        public Expression Generate(int lv)
        {
            g = false;
            return Generate(null, lv, new Random(Seed));
        }

        bool g = false;
        internal Expression Generate(Expression parent, int level, Random rand)
        {
            Expression ret = null;
            if (level == 0)
            {
                if (!g)
                {
                    ret = new VariableExpression();
                    g = true;
                }
                else
                {
                    // Generate a new random number (constant).
                    ret = new ConstantExpression();
                    ret.Generate(this, level, rand);
                }
            }
            else
            {
                // Instead generate a new expression.
                // This leads to multiple expressions in the same root expression.
                ret = (Expression)Activator.CreateInstance(ExpressionTypes[rand.Next(0, ExpressionTypes.Length)]);
                ret.Generate(this, level - 1, rand);
            }
            ret.Parent = parent;
            return ret;
        }
    }
}
