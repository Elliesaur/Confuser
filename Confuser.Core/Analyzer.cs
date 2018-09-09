using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;

namespace Confuser.Core
{
    public abstract class Analyzer : IProgressProvider
    {
        protected Confuser Confuser { get; private set; }
        protected Logger Logger { get; private set; }
        protected IProgresser Progresser { get; private set; }
        public abstract void Analyze(IEnumerable<AssemblyDefinition> asms);

        internal void SetConfuser(Confuser cr)
        {
            this.Confuser = cr;
            this.Logger = cr.param.Logger;
        }
        public void SetProgresser(IProgresser progresser)
        {
            this.Progresser = progresser;
        }
    }
}
