using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using Mono.Cecil;

namespace Confuser.AsmSelector
{
    class Namespace : Collection<TypeDefinition>, IAnnotationProvider
    {
        public ModuleDefinition Module { get; set; }
        public string Name { get; set; }

        Dictionary<object, object> annotations = new Dictionary<object, object>();
        public System.Collections.IDictionary Annotations { get { return annotations; } }
    }
}
