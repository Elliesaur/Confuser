using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Confuser.Core.Analyzers.Baml;
using System.Resources;
using System.Collections;
using System.IO;
using System.Xml.Linq;

namespace Confuser.Core.Analyzers
{
    partial class NameAnalyzer
    {
        Dictionary<AssemblyDefinition, Dictionary<string, List<IMemberDefinition>>> mems = new Dictionary<AssemblyDefinition, Dictionary<string, List<IMemberDefinition>>>();
        void PopulateMembers(TypeDefinition typeDef, Dictionary<string, List<IMemberDefinition>> mems)
        {
            foreach (var i in typeDef.NestedTypes)
                PopulateMembers(i, mems);
            foreach (var i in typeDef.Methods)
            {
                List<IMemberDefinition> p;
                if (!mems.TryGetValue(i.Name, out p))
                    p = mems[i.Name] = new List<IMemberDefinition>();
                p.Add(i);
            }
            foreach (var i in typeDef.Properties)
            {
                List<IMemberDefinition> p;
                if (!mems.TryGetValue(i.Name, out p))
                    p = mems[i.Name] = new List<IMemberDefinition>();
                p.Add(i);
            }
            foreach (var i in typeDef.Events)
            {
                List<IMemberDefinition> p;
                if (!mems.TryGetValue(i.Name, out p))
                    p = mems[i.Name] = new List<IMemberDefinition>();
                p.Add(i);
            }
            foreach (var i in typeDef.Fields)
            {
                List<IMemberDefinition> p;
                if (!mems.TryGetValue(i.Name, out p))
                    p = mems[i.Name] = new List<IMemberDefinition>();
                p.Add(i);
            }
        }
        void PopulateMembers(AssemblyDefinition asm)
        {
            if (!mems.ContainsKey(asm))
            {
                var p = new Dictionary<string, List<IMemberDefinition>>();
                foreach (var i in asm.Modules)
                    foreach (var j in i.Types)
                        PopulateMembers(j, p);
                mems[asm] = p;
            }
        }
        List<IMemberDefinition> GetMember(string name, out bool hasImport)
        {
            List<IMemberDefinition> ret = new List<IMemberDefinition>();
            hasImport = false;
            foreach (var i in mems)
            {
                List<IMemberDefinition> p;
                if (i.Value.TryGetValue(name, out p))
                {
                    if (!ivtMap.ContainsKey(i.Key))
                        hasImport = true;
                    else
                        ret.AddRange(p);
                }
            }
            return ret.Count == 0 ? null : ret;
        }

        string GetBamlAssemblyFullName(AssemblyNameReference asmName)
        {
            if (asmName.PublicKeyToken == null || asmName.PublicKeyToken.Length == 0)
                return string.Format("{0}, Version={1}", asmName.Name, asmName.Version);
            else
            {
                string token = BitConverter.ToString(asmName.PublicKeyToken).Replace("-", "").ToLower();
                return string.Format("{0}, Version={1}, PublicKeyToken={2}", asmName.Name, asmName.Version, token);
            }
        }
        static void SplitClrNsUri(string xmlNamespace, out string clrNamespace, out string assembly)
        {
            clrNamespace = null;
            assembly = null;
            int index = xmlNamespace.IndexOf("clr-namespace:", StringComparison.Ordinal);
            if (index >= 0)
            {
                index += "clr-namespace:".Length;
                if (index < xmlNamespace.Length)
                {
                    int startIndex = xmlNamespace.IndexOf(";assembly=", StringComparison.Ordinal);
                    if (startIndex < index)
                    {
                        clrNamespace = xmlNamespace.Substring(index);
                    }
                    else
                    {
                        clrNamespace = xmlNamespace.Substring(index, startIndex - index);
                        startIndex += ";assembly=".Length;
                        if (startIndex < xmlNamespace.Length)
                        {
                            assembly = xmlNamespace.Substring(startIndex);
                        }
                    }
                }
            }
        }
        static TypeDefinition ResolveXmlns(string xmlNamespace, string typeName, AssemblyDefinition context, IEnumerable<AssemblyDefinition> asms)
        {
            typeName = typeName.Replace('+', '/');
            if (xmlNamespace.StartsWith("clr-namespace:"))
            {
                string ns, asmName;
                SplitClrNsUri(xmlNamespace, out ns, out asmName);

                AssemblyDefinition asm;
                if (asmName == null)
                    asm = context;
                else
                {
                    asmName = AssemblyNameReference.Parse(asmName).Name;
                    if (asmName == context.Name.Name)
                        asm = context;
                    else
                        asm = asms.SingleOrDefault(_ => _.Name.Name == asmName);
                }

                if (asm != null)
                {
                    if (string.IsNullOrEmpty(ns))
                        return asm.MainModule.GetType(typeName);
                    else
                        return asm.MainModule.GetType(string.Format("{0}.{1}", ns, typeName));
                }
            }
            else
            {
                foreach (var i in asms)
                {
                    foreach (var attr in i.CustomAttributes.Where(_ => _.AttributeType.FullName == "System.Windows.Markup.XmlnsDefinitionAttribute"))
                    {
                        string uri = attr.ConstructorArguments[0].Value as string;
                        string clrNs = attr.ConstructorArguments[1].Value as string;
                        if (uri == xmlNamespace)
                        {
                            TypeDefinition typeDef;
                            if (string.IsNullOrEmpty(clrNs))
                                typeDef = i.MainModule.GetType(typeName);
                            else
                                typeDef = i.MainModule.GetType(string.Format("{0}.{1}", clrNs, typeName));
                            if (typeDef != null)
                                return typeDef;
                        }
                    }
                }
            }
            return null;
        }

        BamlDocument doc;
        string docName;
        void ProcessProperty(BamlRecord rec, string path)
        {
            int idx = -1;
            List<BamlPathReference> refers = new List<BamlPathReference>();
            string prev = null;
            char prevSym = '.';
            for (int i = 0; i < path.Length; i++)
            {
                if (char.IsLetterOrDigit(path[i]) || path[i] == '_')
                {
                    if (idx == -1)
                        idx = i;
                }
                else if (i - idx > 0 && idx != -1 && (path[i] == '.' || path[i] == ')'))
                {
                    string name = path.Substring(idx, i - idx);
                    bool hasImport;
                    var ms = GetMember(name, out hasImport);
                    if (ms != null)
                        foreach (var m in ms)
                        {
                            Confuser.Database.AddEntry(DB_SRC, m.FullName, string.Format(
                                "BAML Prop Ref @ {0}:{1}:{2} => Not renamed", docName, rec.Position, path));
                            (m as IAnnotationProvider).Annotations[RenOk] = false;
                        }
                    //if (p != null && prevSym == '.')
                    //{
                    //    var specProp = p.SingleOrDefault(_ => _.DeclaringType.Name == prev);
                    //    if (specProp != null)
                    //    {
                    //        BamlPathReference refer = new BamlPathReference(rec, idx, i - 1);
                    //        refers.Add(refer);
                    //        ((specProp as IAnnotationProvider).Annotations[RenRef] as List<IReference>).Add(refer);
                    //    }
                    //    else
                    //        foreach (var prop in p)
                    //        {
                    //            BamlPathReference refer = new BamlPathReference(rec, idx, i - 1);
                    //            refers.Add(refer);
                    //            ((prop as IAnnotationProvider).Annotations[RenRef] as List<IReference>).Add(refer);
                    //        }
                    //}

                    idx = -1;
                    prev = name;
                    prevSym = path[i];
                }
                else
                {
                    prevSym = path[i];
                    idx = -1;
                }
            }
            if (idx != -1)
            {
                string name = path.Substring(idx);
                bool hasImport;
                var ms = GetMember(name, out hasImport);
                //if (p != null)
                //    foreach (var prop in p)
                //    {
                //        BamlPathReference refer = new BamlPathReference(rec, idx, path.Length - 1);
                //        refers.Add(refer);
                //        ((prop as IAnnotationProvider).Annotations[RenRef] as List<IReference>).Add(refer);
                //    }
                if (ms != null)
                    foreach (var m in ms)
                    {
                        Confuser.Database.AddEntry(DB_SRC, m.FullName, string.Format(
                            "BAML Prop Ref @ {0}:{1}:{2} => Not renamed", docName, rec.Position, path));
                        (m as IAnnotationProvider).Annotations[RenOk] = false;
                    }
            }
            else
            {
                string name = path;
                bool hasImport;
                var ms = GetMember(name, out hasImport);
                //if (p != null)
                //    foreach (var prop in p)
                //    {
                //        BamlPathReference refer = new BamlPathReference(rec, idx, path.Length - 1);
                //        refers.Add(refer);
                //        ((prop as IAnnotationProvider).Annotations[RenRef] as List<IReference>).Add(refer);
                //    }
                if (ms != null)
                    foreach (var m in ms)
                    {
                        Confuser.Database.AddEntry(DB_SRC, m.FullName, string.Format(
                            "BAML Prop Ref @ {0}:{1}:{2} => Not renamed", docName, rec.Position, path));
                        (m as IAnnotationProvider).Annotations[RenOk] = false;
                    }
            }

            foreach (var i in refers)
                i.refers = refers;
        }
        void AddTypeRenRefs(TypeReference type, TypeReference root, TypeInfoRecord rec)
        {
            if (type is TypeSpecification)
            {
                AddTypeRenRefs((type as TypeSpecification).ElementType, root, rec);
                if (type is RequiredModifierType)
                    AddTypeRenRefs((type as RequiredModifierType).ModifierType, root, rec);
                else if (type is OptionalModifierType)
                    AddTypeRenRefs((type as OptionalModifierType).ModifierType, root, rec);
                else if (type is GenericInstanceType)
                    foreach (var i in (type as GenericInstanceType).GenericArguments)
                        AddTypeRenRefs(i, root, rec);
            }
            else
            {
                TypeDefinition typeDef = type.Resolve();
                if (typeDef != null && (typeDef as IAnnotationProvider).Annotations[RenRef] != null)
                {
                    type.Scope = typeDef.Scope;
                    ((typeDef as IAnnotationProvider).Annotations[RenRef] as List<IReference>).Add(new BamlTypeReference(rec, type, root));
                }
            }
        }

        void AnalyzeResource(ModuleDefinition mod, int resId)
        {
            EmbeddedResource res = mod.Resources[resId] as EmbeddedResource;
            ResourceReader resRdr = new ResourceReader(res.GetResourceStream());
            Dictionary<string, object> ress;
            Dictionary<string, BamlDocument> bamls;
            (res as IAnnotationProvider).Annotations["Gresources"] = ress = new Dictionary<string, object>();
            (res as IAnnotationProvider).Annotations["Gbamls"] = bamls = new Dictionary<string, BamlDocument>();
            int cc = 0;
            int n = 0;
            var reses = resRdr.OfType<DictionaryEntry>().ToArray();
            foreach (DictionaryEntry entry in reses)
            {
                Stream stream = null;
                if (entry.Value is Stream)
                {
                    byte[] buff = new byte[(entry.Value as Stream).Length];
                    (entry.Value as Stream).Position = 0;
                    (entry.Value as Stream).Read(buff, 0, buff.Length);
                    ress.Add(entry.Key as string, stream = new MemoryStream(buff));
                }
                else
                    ress.Add(entry.Key as string, entry.Value);
                if (stream != null)
                {
                    if ((entry.Key as string).EndsWith(".baml"))
                    {
                        ProcessBaml(mod, bamls, ref cc, entry, stream);
                    }
                    else if ((entry.Key as string).EndsWith(".xaml"))
                    {
                        ProcessXaml(mod, ref cc, entry, stream);
                    }
                }
                n++;
                Logger._Progress(n, reses.Length);
            }
            if (cc != 0)
                ((res as IAnnotationProvider).Annotations[RenRef] as List<IReference>).Add(new SaveWpfResReference(mod, resId));
        }

        private void ProcessBaml(ModuleDefinition mod, Dictionary<string, BamlDocument> bamls, ref int cc, DictionaryEntry entry, Stream stream)
        {
            cc++;
            docName = entry.Key as string;
            doc = BamlReader.ReadDocument(stream);
            (mod as IAnnotationProvider).Annotations[RenMode] = NameMode.Letters;

            for (int i = 0; i < doc.Count; i++)
            {
                if (doc[i] is LineNumberAndPositionRecord || doc[i] is LinePositionRecord)
                {
                    doc.RemoveAt(i); i--;
                }
            }

            int asmId = -1;
            Dictionary<ushort, AssemblyDefinition> asms = new Dictionary<ushort, AssemblyDefinition>();

            foreach (var rec in doc.OfType<AssemblyInfoRecord>())
            {
                AssemblyNameReference nameRef = AssemblyNameReference.Parse(rec.AssemblyFullName);
                if (nameRef.Name == mod.Assembly.Name.Name)
                {
                    asmId = rec.AssemblyId;
                    rec.AssemblyFullName = GetBamlAssemblyFullName(mod.Assembly.Name);
                    asms.Add(rec.AssemblyId, mod.Assembly);
                    PopulateMembers(mod.Assembly);
                    nameRef = null;
                }
                else
                {
                    bool added = false;
                    foreach (var i in ivtMap)
                        if (i.Key.Name.Name == nameRef.Name)
                        {
                            rec.AssemblyFullName = GetBamlAssemblyFullName(i.Key.Name);
                            asms.Add(rec.AssemblyId, i.Key);
                            PopulateMembers(i.Key);
                            nameRef = null;
                            added = true;
                            break;
                        }
                    if (!added)
                    {
                        var asmRefDef = GlobalAssemblyResolver.Instance.Resolve(nameRef);
                        if (asmRefDef != null)
                        {
                            PopulateMembers(asmRefDef);
                        }
                    }
                }
            }

            Dictionary<ushort, TypeDefinition> types = new Dictionary<ushort, TypeDefinition>();
            foreach (var rec in doc.OfType<TypeInfoRecord>())
            {
                AssemblyDefinition asm;
                if (asms.TryGetValue((ushort)(rec.AssemblyId & 0xfff), out asm))
                {
                    TypeReference type = TypeParser.ParseType(asm.MainModule, rec.TypeFullName);
                    if (type != null)
                    {
                        types.Add(rec.TypeId, type.Resolve());
                        AddTypeRenRefs(type, type, rec);
                        rec.TypeFullName = TypeParser.ToParseable(type);
                    }
                }
            }

            Dictionary<string, string> xmlns = new Dictionary<string, string>();
            foreach (var rec in doc.OfType<XmlnsPropertyRecord>())
                xmlns[rec.Prefix] = rec.XmlNamespace;

            Dictionary<ushort, string> ps = new Dictionary<ushort, string>();
            foreach (var rec in doc.OfType<AttributeInfoRecord>())
            {
                if (types.ContainsKey(rec.OwnerTypeId))
                {
                    PropertyDefinition prop = types[rec.OwnerTypeId].Properties.SingleOrDefault(p => p.Name == rec.Name);
                    if (prop != null)
                    {
                        ((prop as IAnnotationProvider).Annotations[RenRef] as List<IReference>).Add(new BamlAttributeReference(rec));
                    }
                    EventDefinition evt = types[rec.OwnerTypeId].Events.SingleOrDefault(p => p.Name == rec.Name);
                    if (evt != null)
                    {
                        ((evt as IAnnotationProvider).Annotations[RenRef] as List<IReference>).Add(new BamlAttributeReference(rec));
                    }
                    FieldDefinition field = types[rec.OwnerTypeId].Fields.SingleOrDefault(p => p.Name == rec.Name);
                    if (field != null)
                    {
                        ((field as IAnnotationProvider).Annotations[RenRef] as List<IReference>).Add(new BamlAttributeReference(rec));
                    }

                    //Attached property
                    MethodDefinition getM = types[rec.OwnerTypeId].Methods.SingleOrDefault(p => p.Name == "Get" + rec.Name);
                    if (getM != null)
                    {
                        (getM as IAnnotationProvider).Annotations[RenOk] = false;
                    }
                    MethodDefinition setM = types[rec.OwnerTypeId].Methods.SingleOrDefault(p => p.Name == "Set" + rec.Name);
                    if (setM != null)
                    {
                        (setM as IAnnotationProvider).Annotations[RenOk] = false;
                    }
                }
                ps.Add(rec.AttributeId, rec.Name);
            }

            foreach (var rec in doc.OfType<PropertyWithConverterRecord>())
            {
                if (rec.ConverterTypeId == 0xfd4c || ((short)rec.AttributeId > 0 && ps[rec.AttributeId] == "TypeName"))  //TypeExtension
                {
                    string type = rec.Value;

                    string xmlNamespace;
                    if (type.IndexOf(':') != -1)
                    {
                        xmlNamespace = xmlns[type.Substring(0, type.IndexOf(':'))];
                        type = type.Substring(type.IndexOf(':') + 1, type.Length - type.IndexOf(':') - 1);
                    }
                    else
                    {
                        xmlNamespace = xmlns[""];
                    }
                    TypeDefinition typeDef;
                    if ((typeDef = ResolveXmlns(xmlNamespace, type, mod.Assembly, asms.Values)) != null)
                    {
                        ((typeDef as IAnnotationProvider).Annotations[RenRef] as List<IReference>).Add(new BamlTypeExtReference(rec, doc, typeDef.Module.Assembly.Name.Name));
                    }
                }
                if (rec.ConverterTypeId == 0xff77 || rec.ConverterTypeId == 0xfe14 || rec.ConverterTypeId == 0xfd99)
                {
                    ProcessProperty(rec, rec.Value);
                }
            }

            for (int i = 1; i < doc.Count; i++)
            {
                ElementStartRecord binding = doc[i - 1] as ElementStartRecord;
                ConstructorParametersStartRecord param = doc[i] as ConstructorParametersStartRecord;
                if (binding != null && param != null && binding.TypeId == 0xffec)//Binding
                {
                    TextRecord path = doc[i + 1] as TextRecord;
                    ProcessProperty(path, path.Value);
                }
            }

            var rootRec = doc.OfType<ElementStartRecord>().FirstOrDefault();
            if (rootRec != null && types.ContainsKey(rootRec.TypeId))
            {
                TypeDefinition root = types[rootRec.TypeId];
                Dictionary<string, IMemberDefinition> m = new Dictionary<string, IMemberDefinition>();
                foreach (PropertyDefinition prop in root.Properties)
                    m.Add(prop.Name, prop);
                foreach (EventDefinition evt in root.Events)
                    m.Add(evt.Name, evt);
                //foreach (MethodDefinition mtd in root.Methods)
                //    mems.Add(mtd.Name, mtd);

                foreach (var rec in doc.OfType<PropertyRecord>())
                {
                    if (!(rec.Value is string)) continue;
                    if (m.ContainsKey((string)rec.Value))
                    {
                        ((m[(string)rec.Value] as IAnnotationProvider).Annotations[RenRef] as List<IReference>).Add(new BamlPropertyReference(rec));
                    }
                }
            }

            bamls.Add(entry.Key as string, doc);
        }

        private void ProcessXaml(ModuleDefinition mod, ref int cc, DictionaryEntry entry, Stream stream)
        {
            cc++;
            docName = entry.Key as string;
            (mod as IAnnotationProvider).Annotations[RenMode] = NameMode.Letters;
            AnalyzeXaml(mod.Assembly, stream);
        }

    }
}
