using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Mono.Cecil;
using Confuser.Core.Analyzers.Xaml;
using System.IO;
using System.Xml;

namespace Confuser.Core.Analyzers
{
    partial class NameAnalyzer
    {
        void LoadDependencies(AssemblyDefinition asmDef, List<AssemblyDefinition> l, Action<AssemblyDefinition> cb)
        {
            foreach (var i in asmDef.Modules.SelectMany(_ => _.AssemblyReferences))
            {
                if (!l.Any(_ => _.Name.Name == i.Name))
                {
                    var asm = GlobalAssemblyResolver.Instance.Resolve(i);
                    l.Add(asm);
                    cb(asm);
                    LoadDependencies(asm, l, cb);
                }
            }
        }

        void AnalyzeXaml(AssemblyDefinition asm, Stream stream)
        {
            string s = new StreamReader(stream).ReadToEnd();
            var doc = XDocument.Parse(s, LoadOptions.SetLineInfo);

            XamlContext txt = new XamlContext()
            {
                context = asm,
                xaml = s,
                lines = s.Split('\n')
            };
            LoadDependencies(asm, txt.asms = new List<AssemblyDefinition>(), i =>
            {
                foreach (var attr in i.CustomAttributes.Where(_ =>
                    _.AttributeType.FullName == "System.Windows.Markup.XmlnsDefinitionAttribute"
                    ))
                {
                    List<XmlNsDef> map;
                    if (!txt.uri2nsDef.TryGetValue((string)attr.ConstructorArguments[0].Value, out map))
                        map = txt.uri2nsDef[(string)attr.ConstructorArguments[0].Value] = new List<XmlNsDef>();

                    var asmNameProp = attr.Properties
                        .Cast<CustomAttributeNamedArgument?>()
                        .FirstOrDefault(_ => _.Value.Name == "AssemblyName");
                    map.Add(new XmlNsDef()
                    {
                        ClrNamespace = (string)attr.ConstructorArguments[1].Value,
                        Assembly = asmNameProp == null ? i : GlobalAssemblyResolver.Instance.Resolve((string)asmNameProp.Value.Argument.Value)
                    });
                }
            });

            foreach (var i in doc.Elements())
                AnalyzeElement(txt, i);
            FinalizeReferences(txt);
        }

        void FinalizeReferences(XamlContext txt)
        {
            IGrouping<int, XamlRef>[] refers = txt.refers
                .GroupBy(_ => _.lineNum)
                .OrderBy(_ => _.Key).ToArray();

            object[][] lines = new object[txt.lines.Length][];
            foreach (var i in refers)
            {
                int lineNum = i.Key;
                string line = txt.lines[lineNum];
                List<object> segs = new List<object>();
                int prev = 0;
                foreach (var j in i.OrderBy(_ => _.index))    //No overlapping
                {
                    if (j.index != prev)
                        segs.Add(line.Substring(prev, j.index - prev));
                    segs.Add(j.name);
                    j.refer.Context = txt;
                    j.refer.Line = lineNum;
                    j.refer.Segment = segs.Count - 1;
                    prev = j.index + j.length;
                }
                if (line.Length != prev)
                    segs.Add(line.Substring(prev, line.Length - prev));
                lines[lineNum] = segs.ToArray();
            }
            for (int i = 0; i < lines.Length; i++)
                if (lines[i] == null)
                    lines[i] = new object[] { txt.lines[i] };

            txt.segments = lines;
        }

        PropertyDefinition ResolveProperty(TypeDefinition typeDef, string name)
        {
            if (typeDef == null) return null;
            do
            {
                PropertyDefinition ret = typeDef.Properties.SingleOrDefault(_ => _.Name == name);
                if (ret != null) return ret;

                if (typeDef.BaseType != null)
                    typeDef = typeDef.BaseType.Resolve();
                else
                    typeDef = null;
            } while (typeDef != null);
            return null;
        }

        void AnalyzePropertyAttr(TypeDefinition typeDef, XamlContext txt, XAttribute attr)
        {
            XamlPropertyName name = XamlPropertyName.Parse(typeDef, attr.Name, txt);
            name.Xmlns = attr.Parent.GetPrefixOfNamespace(name.Xmlns);
            var prop = ResolveProperty(name.TypeDef, name.PropertyName);
            if (prop != null && Assemblies.Contains(prop.DeclaringType.Module.Assembly))
            {
                IXmlLineInfo li = attr as IXmlLineInfo;
                string line = txt.lines[li.LineNumber - 1];
                int end = line.IndexOf('=', li.LinePosition - 1);
                string str = line.Substring(li.LinePosition - 1, end - li.LinePosition + 1);
                var r = new XamlPropertyNameReference() { Name = name };
                txt.refers.Add(new XamlRef()
                {
                    lineNum = li.LineNumber - 1,
                    index = li.LinePosition - 1,
                    length = end - li.LinePosition + 1,
                    name = name,
                    refer = r
                });
                ((prop as IAnnotationProvider).Annotations[RenRef] as List<IReference>).Add(r);
            }

            if (name.Xmlns == "x" && name.PropertyName == "Class")
            {
                IXmlLineInfo li = attr as IXmlLineInfo;
                string line = txt.lines[li.LineNumber - 1];
                int start = line.IndexOf('=', li.LinePosition - 1) + 2;
                int end = line.IndexOf(line[start - 1], start);
                string fullName = line.Substring(start, end - start);

                TypeDefinition type = txt.context.MainModule.GetType(fullName);
                if (type != null)
                {
                    var n = new XamlClrName() { Namespace = type.Namespace, Name = type.Name };
                    var r = new XamlClrNameReference() { Name = n };
                    txt.refers.Add(new XamlRef()
                    {
                        lineNum = li.LineNumber - 1,
                        index = start,
                        length = end - start,
                        name = n,
                        refer = r
                    });
                    ((type as IAnnotationProvider).Annotations[RenRef] as List<IReference>).Add(r);
                }
            }
        }

        void AnalyzePropertyElem(TypeDefinition typeDef, XamlContext txt, XElement elem)
        {
            XamlPropertyName name = XamlPropertyName.Parse(typeDef, elem.Name, txt);
            name.Xmlns = elem.GetPrefixOfNamespace(name.Xmlns);
            var prop = ResolveProperty(name.TypeDef, name.PropertyName);
            if (prop != null && Assemblies.Contains(prop.DeclaringType.Module.Assembly))
            {
                IXmlLineInfo li = elem as IXmlLineInfo;
                string line = txt.lines[li.LineNumber - 1];
                int end = line.IndexOf('>', li.LinePosition - 1);
                string str = line.Substring(li.LinePosition - 1, end - li.LinePosition + 1);
                var r = new XamlPropertyNameReference() { Name = name };
                txt.refers.Add(new XamlRef()
                {
                    lineNum = li.LineNumber - 1,
                    index = li.LinePosition - 1,
                    length = end - li.LinePosition + 1,
                    name = name,
                    refer = r
                });
                ((prop as IAnnotationProvider).Annotations[RenRef] as List<IReference>).Add(r);
            }

            foreach (var i in elem.Elements())
            {
                if (i.Name.LocalName.Contains("."))
                    AnalyzePropertyElem(name.TypeDef, txt, i);
                else
                    AnalyzeElement(txt, i);
            }
        }

        IEnumerable<XmlNsDef> ParseXmlNs(string xmlns, XamlContext txt)
        {
            if (txt.uri2nsDef.ContainsKey(xmlns))
                return txt.uri2nsDef[xmlns];
            else
            {
                if (!xmlns.StartsWith("clr-namespace:")) return Enumerable.Empty<XmlNsDef>();
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

        void AnalyzeElement(XamlContext txt, XElement elem)
        {
            foreach (var i in elem.Attributes())
                if (i.Name.NamespaceName == "http://www.w3.org/2000/xmlns/")
                    txt.namespaces[i.Name.LocalName] = ParseXmlNs(i.Value, txt);
                else if (i.Name.LocalName == "xmlns")
                    txt.namespaces[""] = ParseXmlNs(i.Value, txt);

            XamlName name = XamlName.Parse(elem.Name, txt);
            name.Xmlns = elem.GetPrefixOfNamespace(name.Xmlns);
            if (name.TypeDef == null)
                Logger._Log("> Could not resolve '" + elem.Name.ToString() + "'!");
            else if (Assemblies.Contains(name.TypeDef.Module.Assembly))
            {
                IXmlLineInfo li = elem as IXmlLineInfo;
                string line = txt.lines[li.LineNumber - 1];
                int end = line.IndexOf(' ', li.LinePosition - 1);
                if (end == -1)
                    end = line.IndexOf('>', li.LinePosition - 1);
                string str = line.Substring(li.LinePosition - 1, end - li.LinePosition + 1);
                var r = new XamlNameReference() { Name = name };
                txt.refers.Add(new XamlRef()
                {
                    lineNum = li.LineNumber - 1,
                    index = li.LinePosition - 1,
                    length = end - li.LinePosition + 1,
                    name = name,
                    refer = r
                });
                ((name.TypeDef as IAnnotationProvider).Annotations[RenRef] as List<IReference>).Add(r);
            }

            foreach (var i in elem.Attributes())
                if (i.Name.NamespaceName != "http://www.w3.org/2000/xmlns/" &&
                    i.Name.LocalName != "xmlns")
                    AnalyzePropertyAttr(name.TypeDef, txt, i);

            foreach (var i in elem.Elements())
            {
                if (i.Name.LocalName.Contains("."))
                    AnalyzePropertyElem(name.TypeDef, txt, i);
                else
                    AnalyzeElement(txt, i);
            }
        }
    }
}
