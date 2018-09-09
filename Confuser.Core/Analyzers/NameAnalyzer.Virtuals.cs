using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Collections.Generic;

namespace Confuser.Core.Analyzers
{
    class VTableSlot
    {
        public MethodReference Root;
        public MethodReference OpenRoot;
        public void ReplaceRoot(NameAnalyzer analyzer, MethodReference newRoot)
        {
            if (Root == newRoot || OpenRoot == newRoot) return;

            var renRef = (newRoot.Resolve() as IAnnotationProvider).Annotations[NameAnalyzer.RenRef] as List<IReference>;
            var root = (OpenRoot ?? Root).Resolve();
            var renId = (root as IAnnotationProvider).Annotations[NameAnalyzer.RenId] as Identifier?;
            if (analyzer.Assemblies.Contains(root.Module.Assembly))
                analyzer.Cr.Database.AddEntry(NameAnalyzer.DB_SRC, root.FullName, string.Format("Virtual override @ {0} => Not renamed", newRoot.FullName));
            (root as IAnnotationProvider).Annotations[NameAnalyzer.RenOk] = false;
            if (renRef != null && renId != null)
            {
                renRef.Add(new VirtualMethodReference(root));
            }
            Root = newRoot;
            OpenRoot = null;
        }
        public MethodReference Current;
        public MethodReference OpenCurrent;
        public void ReplaceCurrent(NameAnalyzer analyzer, MethodReference newCurrent)
        {
            if (Current == newCurrent || OpenCurrent == newCurrent) return;

            MethodReference m = (((OpenCurrent ?? Current) ?? OpenRoot) ?? Root).Resolve();    //Interface method -> Empty slot
            if (m != null)
            {
                var renRef = (m as IAnnotationProvider).Annotations[NameAnalyzer.RenRef] as List<IReference>;
                var newCurrentDef = newCurrent.Resolve();
                var renId = (newCurrentDef as IAnnotationProvider).Annotations[NameAnalyzer.RenId] as Identifier?;
                if (analyzer.Assemblies.Contains(newCurrentDef.Module.Assembly))
                    analyzer.Cr.Database.AddEntry(NameAnalyzer.DB_SRC, newCurrent.FullName, string.Format("Virtual override @ {0} => Not renamed", m.FullName));
                (newCurrentDef as IAnnotationProvider).Annotations[NameAnalyzer.RenOk] = false;
                if (renRef != null)
                {
                    renRef.Add(new VirtualMethodReference(newCurrentDef));
                }
            }
            Current = newCurrent;
            OpenCurrent = null;
        }

        public VTableSlot Clone()
        {
            return new VTableSlot()
            {
                Root = Root,
                OpenRoot = OpenRoot,
                Current = Current,
                OpenCurrent = OpenCurrent
            };
        }
    }
    struct VTable
    {
        public TypeDefinition TypeDef;
        public List<VTableSlot> Table;

        static bool Match(MethodReference method, MethodReference target)
        {
            return Match(method, target, true);
        }
        static bool Match(MethodReference method, MethodReference target, bool checkName)
        {
            if (target == null) return false;
            if (method.DeclaringType is GenericInstanceType)
                method = Resolve(method, method.DeclaringType as GenericInstanceType);
            if (target.DeclaringType is GenericInstanceType)
                target = Resolve(target, target.DeclaringType as GenericInstanceType);

            if (method is MethodDefinition && (method as MethodDefinition).HasOverrides)
            {
                foreach (var i in (method as MethodDefinition).Overrides)
                    if (Match(i, target))
                        return true;
            }

            if ((!checkName || method.Name == target.Name) &&
                Match(method.ReturnType, target.ReturnType) &&
                method.Parameters.Count == target.Parameters.Count)
            {
                bool f = true;
                for (int i = 0; i < target.Parameters.Count; i++)
                    if (!Match(method.Parameters[i].ParameterType, target.Parameters[i].ParameterType))
                    {
                        f = false;
                        break;
                    }
                return f;
            }
            return false;
        }
        static bool Match(TypeReference type, TypeReference target)
        {
            if (!(target is TypeSpecification))
                return type.Name == target.Name && type.Namespace == target.Namespace;

            if (type.MetadataType != target.MetadataType)
                return false;

            TypeSpecification typeSpecA = type as TypeSpecification;
            TypeSpecification typeSpecB = target as TypeSpecification;
            switch (type.MetadataType)
            {
                case MetadataType.Array:
                    {
                        ArrayType arrA = type as ArrayType, arrB = target as ArrayType;
                        if (arrA.Dimensions.Count != arrB.Dimensions.Count)
                            return false;
                        for (int i = 0; i < arrA.Dimensions.Count; i++)
                            if (arrA.Dimensions[i].LowerBound != arrB.Dimensions[i].LowerBound ||
                                arrA.Dimensions[i].UpperBound != arrB.Dimensions[i].UpperBound)
                                return false;
                        return Match(typeSpecA.ElementType, typeSpecB.ElementType);
                    }
                case MetadataType.RequiredModifier:
                    {
                        RequiredModifierType modA = type as RequiredModifierType, modB = target as RequiredModifierType;
                        return Match(modA.ModifierType, modB.ModifierType) &&
                               Match(typeSpecA.ElementType, typeSpecB.ElementType);
                    }
                case MetadataType.OptionalModifier:
                    {
                        OptionalModifierType modA = type as OptionalModifierType, modB = target as OptionalModifierType;
                        return Match(modA.ModifierType, modB.ModifierType) &&
                               Match(typeSpecA.ElementType, typeSpecB.ElementType);
                    }
                case MetadataType.ByReference:
                case MetadataType.Pinned:
                case MetadataType.Pointer:
                    return Match(typeSpecA.ElementType, typeSpecB.ElementType);
                case MetadataType.GenericInstance:
                    GenericInstanceType instA = type as GenericInstanceType, instB = target as GenericInstanceType;
                    if (instA.GenericArguments.Count != instB.GenericArguments.Count)
                        return false;
                    for (int i = 0; i < instA.GenericArguments.Count; i++)
                        if (!Match(instA.GenericArguments[i], instB.GenericArguments[i]))
                            return false;
                    return Match(typeSpecA.ElementType, typeSpecB.ElementType);
                case MetadataType.FunctionPointer:  //not support
                    throw new NotSupportedException();
            }
            return false;
        }
        static TypeReference Resolve(TypeReference typeRef, Collection<TypeReference> genTypeContext)
        {
            if (!(typeRef is TypeSpecification) && !(typeRef is GenericParameter))
                return typeRef;

            TypeSpecification ret = typeRef.Clone() as TypeSpecification;
            switch (typeRef.MetadataType)
            {
                case MetadataType.RequiredModifier:
                    (ret as RequiredModifierType).ModifierType = Resolve((typeRef as RequiredModifierType).ModifierType, genTypeContext);
                    ret.ElementType = Resolve(ret.ElementType, genTypeContext);
                    break;
                case MetadataType.OptionalModifier:
                    (ret as OptionalModifierType).ModifierType = Resolve((typeRef as OptionalModifierType).ModifierType, genTypeContext);
                    ret.ElementType = Resolve(ret.ElementType, genTypeContext);
                    break;
                case MetadataType.Array:
                case MetadataType.ByReference:
                case MetadataType.Pinned:
                case MetadataType.Pointer:
                    ret.ElementType = Resolve(ret.ElementType, genTypeContext);
                    break;
                case MetadataType.GenericInstance:
                    GenericInstanceType genInst = ret as GenericInstanceType;
                    genInst.GenericArguments.Clear();
                    foreach (var i in (typeRef as GenericInstanceType).GenericArguments)
                        genInst.GenericArguments.Add(Resolve(i, genTypeContext));
                    ret.ElementType = Resolve(ret.ElementType, genTypeContext);
                    break;
                case MetadataType.MVar:
                    return typeRef;
                case MetadataType.Var:
                    if (genTypeContext == null) throw new InvalidOperationException();
                    return genTypeContext[(typeRef as GenericParameter).Position];
                case MetadataType.FunctionPointer:  //not support
                    throw new NotSupportedException();
            }
            return ret;
        }
        static MethodReference Resolve(MethodReference methodRef, GenericInstanceType type)
        {
            if (methodRef == null) return null;
            MethodReference ret = new MethodReference(methodRef.Name,
                Resolve(methodRef.ReturnType, type.GenericArguments),
                Resolve(methodRef.DeclaringType, type.GenericArguments));
            foreach (var i in methodRef.Parameters)
                ret.Parameters.Add(new ParameterDefinition(i.Name, i.Attributes,
                    Resolve(i.ParameterType, type.GenericArguments)));
            return ret;
        }

        public static VTable GetVTable(NameAnalyzer analyzer, TypeDefinition typeDef, Dictionary<TypeDefinition, VTable> tbls)
        {
            if (tbls.ContainsKey(typeDef))
                return tbls[typeDef];

            //Partition II 10.3, 12.2

            VTable ret = new VTable() { TypeDef = typeDef };

            TypeDefinition baseType = null;
            if (typeDef.BaseType != null)
            {
                baseType = typeDef.BaseType.Resolve();
                if (baseType != null)
                {
                    ret.Table = new List<VTableSlot>(GetVTable(analyzer, baseType, tbls).Table);
                    if (typeDef.BaseType is GenericInstanceType)
                    {
                        GenericInstanceType genInst = typeDef.BaseType as GenericInstanceType;
                        for (int i = 0; i < ret.Table.Count; i++)
                        {
                            ret.Table[i] = ret.Table[i].Clone();
                            ret.Table[i].OpenCurrent = ret.Table[i].Current;
                            ret.Table[i].Current = Resolve(ret.Table[i].OpenCurrent, genInst);
                            ret.Table[i].OpenRoot = ret.Table[i].Root;
                            ret.Table[i].Root = Resolve(ret.Table[i].OpenRoot, genInst);
                        }
                    }
                }
                else
                    ret.Table = new List<VTableSlot>();
            }
            else
                ret.Table = new List<VTableSlot>();

            if (typeDef.HasInterfaces)          //Interface methods
            {
                foreach (var i in typeDef.Interfaces)
                {
                    if (baseType != null && baseType.Interfaces.Contains(i)) continue;

                    TypeDefinition iface = i.Resolve();
                    if (iface == null) continue;
                    GenericInstanceType genInst = i as GenericInstanceType;
                    foreach (var j in iface.Methods)
                    {
                        MethodReference ifaceMethod = j;
                        if (genInst != null)
                            ifaceMethod = Resolve(ifaceMethod, genInst);


                        VTableSlot slot = null;
                        for (int k = 0; k < ret.Table.Count; k++)
                            if (Match(ifaceMethod, ret.Table[k].Root))
                            {
                                slot = ret.Table[k];
                                break;
                            }
                        if (slot == null)
                        {
                            if (genInst != null)
                                ret.Table.Add(new VTableSlot()
                                {
                                    Root = ifaceMethod,
                                    OpenRoot = j,
                                    Current = null
                                });
                            else
                                ret.Table.Add(new VTableSlot()
                                {
                                    Root = ifaceMethod,
                                    Current = null
                                });
                        }
                        else
                        {
                            slot.ReplaceRoot(analyzer, j);
                        }
                    }
                }
            }

            foreach (var i in typeDef.Methods)  //Interface virtual newslot
            {
                if (!i.IsVirtual || !i.IsNewSlot) continue;
                for (int j = 0; j < ret.Table.Count; j++)
                    if (ret.Table[j].Current == null && Match(i, ret.Table[j].Root))
                        ret.Table[j].ReplaceCurrent(analyzer, i);
            }
            foreach (var i in typeDef.Methods)  //Interface virtual
            {
                if (!i.IsVirtual) continue;
                for (int j = 0; j < ret.Table.Count; j++)
                    if (ret.Table[j].Current == null && Match(i, ret.Table[j].Root))
                        ret.Table[j].ReplaceCurrent(analyzer, i);
            }
            foreach (var i in typeDef.Methods)  //Base newslot
            {
                if (!i.IsVirtual || !i.IsNewSlot) continue;
                for (int j = 0; j < ret.Table.Count; j++)
                    if (Match(i, ret.Table[j].Current))
                    {
                        ret.Table[j].Root = i;
                        ret.Table[j].ReplaceCurrent(analyzer, i);
                    }
            }
            foreach (var i in typeDef.Methods)  //Base virtual
            {
                if (!i.IsVirtual) continue;
                for (int j = 0; j < ret.Table.Count; j++)
                    if (i != ret.Table[j].Current && Match(i, ret.Table[j].Current))
                    {
                        ret.Table[j].ReplaceCurrent(analyzer, i);
                    }
            }
            foreach (var i in typeDef.Methods)  //Remaining
            {
                if (!i.IsVirtual) continue;
                bool matched = false;
                for (int j = 0; j < ret.Table.Count; j++)
                    if (Match(i, ret.Table[j].Current))
                    {
                        matched = true;
                        break;
                    }
                if (!matched)
                    ret.Table.Add(new VTableSlot()
                    {
                        Root = i,
                        Current = i
                    });
            }

            tbls[typeDef] = ret;
            return ret;
        }
    }

    partial class NameAnalyzer
    {
        void ConstructVTable(TypeDefinition typeDef)
        {
            foreach (var i in typeDef.NestedTypes)
                ConstructVTable(i);
            VTable.GetVTable(this, typeDef, vTbls);
        }
    }
}
