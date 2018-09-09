using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;

namespace Confuser.Core.Analyzers.Xaml
{
    struct XmlNsDef
    {
        public string ClrNamespace;
        public AssemblyDefinition Assembly;
    }

    struct XamlRef
    {
        public int lineNum;
        public int index;
        public int length;
        public object name;
        public IXamlReference refer;
    }

    class XamlContext
    {
        public string xaml;
        public string[] lines;
        public object[][] segments;
        public List<XamlRef> refers = new List<XamlRef>();
        public Dictionary<string, IEnumerable<XmlNsDef>> namespaces = new Dictionary<string, IEnumerable<XmlNsDef>>();
        public Dictionary<string, List<XmlNsDef>> uri2nsDef = new Dictionary<string, List<XmlNsDef>>();
        public AssemblyDefinition context;
        public List<AssemblyDefinition> asms;
    }
}
