using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;

namespace Confuser.Core.Analyzers
{
    partial class NameAnalyzer
    {
        static string NormalizeIva(string asmname)
        {
            string[] names = asmname.Split(',');
            if (names.Length == 1) return asmname;
            else return names[0].Trim();
        }
        static string GetIva(AssemblyNameDefinition name)
        {
            if (name.PublicKey != null) return name.Name + "," + BitConverter.ToString(name.PublicKey).Replace("-", "").ToLower();
            else return name.Name;
        }

        Dictionary<AssemblyDefinition, List<string>> ivtMap;    //also act as assembly list
        Dictionary<MetadataToken, MemberReference> ivtRefs = new Dictionary<MetadataToken, MemberReference>();

        void AnalyzeIvtMap(IEnumerable<AssemblyDefinition> asms)
        {
            ivtMap = new Dictionary<AssemblyDefinition, List<string>>();
            foreach (AssemblyDefinition asm in asms)
            {
                if (!ivtMap.ContainsKey(asm)) ivtMap.Add(asm, new List<string>());
                Dictionary<string, CustomAttribute> internalVis = new Dictionary<string, CustomAttribute>();
                foreach (CustomAttribute attr in asm.CustomAttributes)
                    if (attr.AttributeType.FullName == "System.Runtime.CompilerServices.InternalsVisibleToAttribute")
                        internalVis.Add(NormalizeIva((string)attr.ConstructorArguments[0].Value), attr);
                if (internalVis.Count != 0)
                {
                    Logger._Log("> InternalsVisibleToAttribute found in " + asm.FullName + "!");

                    List<AssemblyDefinition> refAsms = new List<AssemblyDefinition>();
                    foreach (AssemblyDefinition asmm in asms)
                    {
                        CustomAttribute attr;
                        if (internalVis.TryGetValue(asmm.Name.Name, out attr))
                        {
                            refAsms.Add(asmm);
                            attr.ConstructorArguments[0] = new CustomAttributeArgument(
                                asm.MainModule.TypeSystem.String,
                                GetIva(asmm.Name));
                        }
                    }

                    if (refAsms.Count == 0)
                        Logger._Log("> Internal assemblies NOT found!");
                    else
                        Logger._Log("> Internal assemblies found!");
                    foreach (AssemblyDefinition i in refAsms)
                    {
                        if (!ivtMap.ContainsKey(i)) ivtMap.Add(i, new List<string>());
                        ivtMap[i].Add(asm.GetVersionName());
                    }
                }
            }
        }
        void AnalyzeIvt(AssemblyDefinition asm)
        {
            List<string> ivts = ivtMap[asm];
            ivtRefs.Clear();
            AnalyzeCustomAttributes(asm);
            foreach (ModuleDefinition mod in asm.Modules)
            {
                foreach (TypeReference typeRef in mod.GetTypeReferences())
                {
                    TypeDefinition typeDef = typeRef.Resolve();
                    if (typeDef != null && ivts.Contains(typeDef.Module.Assembly.GetVersionName()))
                    {
                        Confuser.Database.AddEntry(DB_SRC, typeDef.FullName, string.Format("IVT ref @ {0} => Not renamed", asm.FullName));
                        (typeDef as IAnnotationProvider).Annotations[RenOk] = false;
                    }
                }
                foreach (MemberReference memRef in mod.GetMemberReferences())
                {
                    IMemberDefinition memDef;
                    if (memRef is MethodReference && (memDef = ((MethodReference)memRef).Resolve()) != null &&
                        ivts.Contains(((MethodDefinition)memDef).Module.Assembly.GetVersionName()))
                    {
                        Confuser.Database.AddEntry(DB_SRC, memDef.FullName, string.Format("IVT ref @ {0} => Not renamed", asm.FullName));
                        (memDef as IAnnotationProvider).Annotations[RenOk] = false;
                    }
                    if (memRef is FieldReference && (memDef = ((FieldReference)memRef).Resolve()) != null &&
                        ivts.Contains(((FieldDefinition)memDef).Module.Assembly.GetVersionName()))
                    {
                        Confuser.Database.AddEntry(DB_SRC, memDef.FullName, string.Format("IVT ref @ {0} => Not renamed", asm.FullName));
                        (memDef as IAnnotationProvider).Annotations[RenOk] = false;
                    }
                }
            }
        }
    }
}
