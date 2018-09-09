using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Confuser.Core.Analyzers.Baml;
using System.IO;
using System.Resources;
using Confuser.Core.Analyzers.Xaml;

namespace Confuser.Core.Analyzers
{
    struct Identifier
    {
        public string scope;
        public string name;
        public int hash;
    }
    interface IReference
    {
        void UpdateReference(Identifier old, Identifier @new);
        bool QueryCancellation();
    }

    class ResourceReference : IReference
    {
        public ResourceReference(Resource res) { this.res = res; }
        Resource res;
        public void UpdateReference(Identifier old, Identifier @new)
        {
            string oldN = string.IsNullOrEmpty(old.scope) ? old.name : old.scope + "." + old.name;
            string newN = string.IsNullOrEmpty(@new.scope) ? @new.name : @new.scope + "." + @new.name;
            res.Name = res.Name.Replace(oldN, newN);
        }
        public bool QueryCancellation()
        {
            return false;
        }
    }
    class ResourceNameReference : IReference
    {
        public ResourceNameReference(Instruction inst) { this.inst = inst; }
        Instruction inst;
        public void UpdateReference(Identifier old, Identifier @new)
        {
            inst.Operand = @new.scope;
        }
        public bool QueryCancellation()
        {
            return false;
        }
    }
    class SpecificationReference : IReference
    {
        public SpecificationReference(MemberReference refer) { this.refer = refer; }
        MemberReference refer;
        public void UpdateReference(Identifier old, Identifier @new)
        {
            MethodSpecification mSpec = refer as MethodSpecification;
            if (mSpec == null)//|| !(mSpec.DeclaringType.GetElementType() is TypeDefinition))
                //{
                //    TypeSpecification tSpec = refer.DeclaringType as TypeSpecification;
                //    TypeDefinition par = tSpec.GetElementType() as TypeDefinition;
                //    if (tSpec != null && par != null)
                //    {
                refer.Name = @new.name;
            //    }
            //}
            else
                (mSpec.ElementMethod).Name = @new.name;
        }
        public bool QueryCancellation()
        {
            return false;
        }
    }
    class CustomAttributeTypeReference : IReference
    {
        public CustomAttributeTypeReference(TypeReference refer) { this.refer = refer; }
        TypeReference refer;
        public void UpdateReference(Identifier old, Identifier @new)
        {
            refer.Namespace = @new.scope;
            refer.Name = @new.name;
        }
        public bool QueryCancellation()
        {
            return false;
        }
    }
    class CustomAttributeMemberReference : IReference
    {
        public CustomAttributeMemberReference(CustomAttribute attr, int idx, bool isField)
        {
            this.attr = attr;
            this.idx = idx;
            this.isField = isField;
        }
        CustomAttribute attr;
        int idx;
        bool isField;
        public void UpdateReference(Identifier old, Identifier @new)
        {
            if (isField)
                attr.Fields[idx] = new CustomAttributeNamedArgument(@new.name, attr.Fields[idx].Argument);
            else
                attr.Properties[idx] = new CustomAttributeNamedArgument(@new.name, attr.Properties[idx].Argument);
        }
        public bool QueryCancellation()
        {
            return false;
        }
    }
    class ReflectionReference : IReference
    {
        public ReflectionReference(Instruction ldstr) { this.ldstr = ldstr; }
        Instruction ldstr;
        public void UpdateReference(Identifier old, Identifier @new)
        {
            string op = (string)ldstr.Operand;
            if (op == old.name)
                ldstr.Operand = @new.name;
            else if (op == old.scope)
                ldstr.Operand = @new.scope;
            else if (op == old.scope + "." + old.name)
                ldstr.Operand = string.IsNullOrEmpty(@new.scope) ? @new.name : @new.scope + "." + @new.name;
        }
        public bool QueryCancellation()
        {
            return false;
        }
    }
    class VirtualMethodReference : IReference
    {
        public VirtualMethodReference(MethodDefinition mtdRefer) { this.mtdRefer = mtdRefer; }
        MethodDefinition mtdRefer;
        public void UpdateReference(Identifier old, Identifier @new)
        {
            if ((mtdRefer as IAnnotationProvider).Annotations[NameAnalyzer.RenOk] == null)
                return;
            mtdRefer.Name = @new.name;
            Identifier id = (Identifier)(mtdRefer as IAnnotationProvider).Annotations[NameAnalyzer.RenId];
            Identifier n = @new;
            n.scope = mtdRefer.DeclaringType.FullName;
            foreach (IReference refer in (mtdRefer as IAnnotationProvider).Annotations[NameAnalyzer.RenRef] as List<IReference>)
            {
                refer.UpdateReference(id, n);
            }
        }
        public bool QueryCancellation()
        {
            if ((mtdRefer as IAnnotationProvider).Annotations[NameAnalyzer.RenOk] == null)
                return false;
            if (!(bool)(mtdRefer as IAnnotationProvider).Annotations[NameAnalyzer.RenOk])
                return true;
            foreach (var i in (mtdRefer as IAnnotationProvider).Annotations[NameAnalyzer.RenRef] as List<IReference>)
                if (i.QueryCancellation())
                    return true;
            return false;
        }
    }
    class BamlTypeReference : IReference
    {
        public BamlTypeReference(TypeInfoRecord typeRec, TypeReference self, TypeReference root)
        {
            this.typeRec = typeRec;
            this.self = self;
            this.root = root;
        }

        TypeInfoRecord typeRec;
        TypeReference self;
        TypeReference root;

        public void UpdateReference(Identifier old, Identifier @new)
        {
            self.Namespace = @new.scope;
            self.Name = @new.name;
            typeRec.TypeFullName = TypeParser.ToParseable(root);
        }
        public bool QueryCancellation()
        {
            return false;
        }
    }
    class BamlTypeExtReference : IReference
    {
        public BamlTypeExtReference(PropertyWithConverterRecord rec, BamlDocument doc, string assembly)
        {
            this.rec = rec;
            this.doc = doc;
            this.assembly = assembly;
        }

        PropertyWithConverterRecord rec;
        string assembly;
        BamlDocument doc;

        public void UpdateReference(Identifier old, Identifier @new)
        {
            string prefix = rec.Value.Substring(0, rec.Value.IndexOf(':'));
            if (old.scope != @new.scope)
            {
                string xmlNamespace = "clr-namespace:" + @new.scope;
                if (@new.scope == null || !string.IsNullOrEmpty(assembly))
                    xmlNamespace += ";assembly=" + assembly;

                for (int i = 0; i < doc.Count; i++)
                {
                    XmlnsPropertyRecord xmlns = doc[i] as XmlnsPropertyRecord;
                    if (xmlns != null)
                    {
                        if (xmlns.XmlNamespace == xmlNamespace)
                        {
                            prefix = xmlns.Prefix;
                            break;
                        }
                        else if (xmlns.Prefix == prefix)
                        {
                            XmlnsPropertyRecord r = new XmlnsPropertyRecord();
                            r.AssemblyIds = xmlns.AssemblyIds;
                            r.Prefix = prefix = ObfuscationHelper.Instance.GetNewName(xmlns.Prefix, NameMode.Letters);
                            r.XmlNamespace = xmlNamespace;
                            doc.Insert(i, r);
                            break;
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(prefix))
                rec.Value = prefix + ":" + @new.name;
            else
                rec.Value = @new.name;
        }
        public bool QueryCancellation()
        {
            return false;
        }
    }
    class BamlAttributeReference : IReference
    {
        public BamlAttributeReference(AttributeInfoRecord attrRec) { this.attrRec = attrRec; }

        AttributeInfoRecord attrRec;

        public void UpdateReference(Identifier old, Identifier @new)
        {
            attrRec.Name = @new.name;
        }
        public bool QueryCancellation()
        {
            return false;
        }
    }
    class BamlPropertyReference : IReference
    {
        public BamlPropertyReference(PropertyRecord propRec) { this.propRec = propRec; }

        PropertyRecord propRec;

        public void UpdateReference(Identifier old, Identifier @new)
        {
            propRec.Value = @new.name;
        }
        public bool QueryCancellation()
        {
            return false;
        }
    }
    class BamlPathReference : IReference
    {
        public BamlPathReference(BamlRecord rec, int startIdx, int endIdx)
        {
            this.rec = rec;
            this.startIdx = startIdx;
            this.endIdx = endIdx;
        }

        BamlRecord rec;
        int startIdx;
        int endIdx;
        internal List<BamlPathReference> refers;

        public void UpdateReference(Identifier old, Identifier @new)
        {
            StringBuilder sb;
            if (rec is TextRecord)
                sb = new StringBuilder((rec as TextRecord).Value);
            else
                sb = new StringBuilder((rec as PropertyWithConverterRecord).Value);
            sb.Remove(startIdx, endIdx - startIdx + 1);
            sb.Insert(startIdx, @new.name);
            if (rec is TextRecord)
                (rec as TextRecord).Value = sb.ToString();
            else
                (rec as PropertyWithConverterRecord).Value = sb.ToString();
            int oEndIdx = endIdx;
            endIdx = startIdx + @new.name.Length - 1;
            foreach (var i in refers)
                if (this != i)
                {
                    if (i.startIdx > this.startIdx)
                        i.startIdx = i.startIdx + (endIdx - oEndIdx);
                    if (i.endIdx > this.startIdx)
                        i.endIdx = i.endIdx + (endIdx - oEndIdx);
                }
        }
        public bool QueryCancellation()
        {
            return false;
        }
    }

    interface IXamlReference : IReference
    {
        XamlContext Context { get; set; }
        int Line { get; set; }
        int Segment { get; set; }
    }
    class XamlNameReference : IXamlReference
    {
        public XamlContext Context { get; set; }
        public int Line { get; set; }
        public int Segment { get; set; }
        public XamlName Name { get; set; }

        public void UpdateReference(Identifier old, Identifier @new)
        {
        }
        public bool QueryCancellation()
        {
            return false;
        }
    }
    class XamlPropertyNameReference : IXamlReference
    {
        public XamlContext Context { get; set; }
        public int Line { get; set; }
        public int Segment { get; set; }
        public XamlPropertyName Name { get; set; }

        public void UpdateReference(Identifier old, Identifier @new)
        {
        }
        public bool QueryCancellation()
        {
            return false;
        }
    }
    class XamlClrNameReference : IXamlReference
    {
        public XamlContext Context { get; set; }
        public int Line { get; set; }
        public int Segment { get; set; }
        public XamlClrName Name { get; set; }

        public void UpdateReference(Identifier old, Identifier @new)
        {
        }
        public bool QueryCancellation()
        {
            return false;
        }
    }


    class SaveWpfResReference : IReference
    {
        public SaveWpfResReference(ModuleDefinition mod, int resId) { this.mod = mod; this.resId = resId; }

        ModuleDefinition mod;
        int resId;

        public void UpdateReference(Identifier old, Identifier @new)
        {
            EmbeddedResource res = mod.Resources[resId] as EmbeddedResource;
            foreach (KeyValuePair<string, BamlDocument> pair in (res as IAnnotationProvider).Annotations["Gbamls"] as Dictionary<string, BamlDocument>)
            {
                Stream dst = new MemoryStream();
                BamlWriter.WriteDocument(pair.Value, dst);
                ((res as IAnnotationProvider).Annotations["Gresources"] as Dictionary<string, object>)[pair.Key] = dst;
            }
            MemoryStream newRes = new MemoryStream();
            ResourceWriter wtr = new ResourceWriter(newRes);
            foreach (KeyValuePair<string, object> pair in (res as IAnnotationProvider).Annotations["Gresources"] as Dictionary<string, object>)
                wtr.AddResource(pair.Key, pair.Value);
            wtr.Generate();
            mod.Resources[resId] = new EmbeddedResource(res.Name, res.Attributes, newRes.ToArray());
        }
        public bool QueryCancellation()
        {
            return false;
        }
    }
    class IvtMemberReference : IReference
    {
        public IvtMemberReference(MemberReference memRef) { this.memRef = memRef; }

        MemberReference memRef;

        public void UpdateReference(Identifier old, Identifier @new)
        {
            if (memRef is MethodReference || memRef is FieldReference)
            {
                memRef.Name = @new.name;
            }
            else if (memRef is TypeReference)
            {
                memRef.Name = @new.name;
                ((TypeReference)memRef).Namespace = @new.scope;
            }
        }
        public bool QueryCancellation()
        {
            return false;
        }

    }
}
