using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using System.Xml.Linq;

namespace Confuser.Core.Analyzers.Xaml
{
    class XamlName
    {
        public AssemblyDefinition Assembly;
        public string Xmlns;
        public string Namespace;
        public string TypeName;
        public TypeDefinition TypeDef;

        protected static IEnumerable<XmlNsDef> ParseXmlNs(string xmlns, XamlContext txt)
        {
            if (txt.uri2nsDef.ContainsKey(xmlns))
                return txt.uri2nsDef[xmlns];
            else
            {
                int colon = xmlns.IndexOf(":");
                int semicolon = xmlns.IndexOf(";");
                if (semicolon == -1)
                    return new[]{ new XmlNsDef()
                    {
                        Assembly = txt.context,
                        ClrNamespace = xmlns.Substring(colon + 1)
                    }};
                else
                {
                    string clrNs = xmlns.Substring(colon + 1, semicolon - (colon + 1));
                    string asmName = xmlns.Substring(xmlns.IndexOf("=") + 1);
                    AssemblyNameReference nameRef = AssemblyNameReference.Parse(asmName);
                    AssemblyDefinition asm = null;
                    foreach (var i in txt.asms)
                        if (i.Name.Name == nameRef.Name)
                        {
                            asm = i;
                            break;
                        }
                    return new[]{ new XmlNsDef()
                    {
                        Assembly = asm,
                        ClrNamespace = clrNs
                    }};
                }
            }
        }
        protected static TypeDefinition Resolve(IEnumerable<XmlNsDef> xmlns, string name, out XmlNsDef def)
        {
            foreach (var i in xmlns)
            {
                var type = i.Assembly.MainModule.GetType(i.ClrNamespace, name);
                if (type != null)
                {
                    def = i;
                    return type;
                }
            }
            def = xmlns.First();
            return null;
        }

        public static XamlName Parse(XName name, XamlContext txt)
        {
            var xmlns = ParseXmlNs(name.NamespaceName, txt);
            XamlName ret = new XamlName();
            ret.TypeName = name.LocalName;

            XmlNsDef nsDef;
            ret.TypeDef = Resolve(xmlns, ret.TypeName, out nsDef);
            ret.Assembly = nsDef.Assembly;
            ret.Xmlns = name.NamespaceName;
            ret.Namespace = nsDef.ClrNamespace;

            return ret;
        }

        public override string ToString()
        {
            if (Xmlns != null)
                return Xmlns + ":" + TypeName;
            else
                return TypeName;
        }
    }

    class XamlPropertyName : XamlName
    {
        public string PropertyName;

        public static XamlPropertyName Parse(TypeDefinition parent, XName name, XamlContext txt)
        {
            var xmlns = ParseXmlNs(name.NamespaceName, txt);
            XamlPropertyName ret = new XamlPropertyName();
            if (name.LocalName.Contains("."))
            {
                int idx = name.LocalName.IndexOf('.');
                ret.TypeName = name.LocalName.Substring(0, idx);
                ret.PropertyName = name.LocalName.Substring(idx + 1);

                XmlNsDef nsDef;
                ret.TypeDef = Resolve(xmlns, ret.TypeName, out nsDef);
                ret.Assembly = nsDef.Assembly;
                ret.Xmlns = name.NamespaceName;
                ret.Namespace = nsDef.ClrNamespace;
            }
            else
            {
                ret.PropertyName = name.LocalName;
                ret.TypeDef = parent;
                ret.Assembly = parent.Module.Assembly;
                ret.Xmlns = name.NamespaceName;
                ret.Namespace = parent.Namespace;
            }

            return ret;
        }

        public override string ToString()
        {
            string ret = "";
            if (Xmlns != null) ret += Xmlns + ":";
            if (TypeName != null) ret += TypeName + ".";
            ret += PropertyName;
            return ret;
        }
    }

    class XamlClrName
    {
        public string Namespace;
        public string Name;

        public override string ToString()
        {
            if (Namespace == null) return Name;
            else return Namespace + "." + Name;
        }
    }

    class XamlClrNamespace
    {
        public string Assembly;
        public string Namespace;

        public override string ToString()
        {
            if (Assembly == null)
                return "clr-namespace:" + Namespace;
            else
                return "clr-namespace:" + Namespace + ";assembly=" + Assembly;
        }
    }
}
