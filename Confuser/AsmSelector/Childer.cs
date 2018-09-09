using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;

namespace Confuser.AsmSelector
{
    class Childer
    {
        public static bool HasChildren(object obj)
        {
            if (obj is TypeDefinition)
            {
                TypeDefinition typeDef = (TypeDefinition)obj;
                return typeDef.HasNestedTypes || typeDef.HasMethods || typeDef.HasFields || typeDef.HasProperties || typeDef.HasEvents;
            }
            else if (obj is AssemblyDefinition)
            {
                return true;
            }
            else if (obj is ModuleDefinition)
            {
                return true;
            }
            else if (obj is Namespace)
            {
                return (obj as Namespace).Count > 0;
            }
            else if (obj is PropertyDefinition)
            {
                PropertyDefinition propDef = (PropertyDefinition)obj;
                return propDef.GetMethod != null || propDef.SetMethod != null || propDef.HasOtherMethods;
            }
            else if (obj is EventDefinition)
            {
                EventDefinition evtDef = (EventDefinition)obj;
                return evtDef.AddMethod != null || evtDef.RemoveMethod != null || evtDef.InvokeMethod != null || evtDef.HasOtherMethods;
            }
            else
                return false;
        }

        static readonly object NS = new object();
        static IEnumerable<Namespace> GetNamespaces(ModuleDefinition mod)
        {
            if ((mod as IAnnotationProvider).Annotations.Contains(NS))
                return (mod as IAnnotationProvider).Annotations[NS] as IEnumerable<Namespace>;
            else
            {
                SortedDictionary<string, Namespace> ns = new SortedDictionary<string, Namespace>(StringComparer.Ordinal);
                foreach (var i in mod.Types)
                {
                    Namespace n;
                    if (!ns.TryGetValue(i.Namespace, out n))
                        ns.Add(i.Namespace, n = new Namespace() { Module = mod, Name = i.Namespace });
                    n.Add(i);
                }
                return ((mod as IAnnotationProvider).Annotations[NS] = ns.Values.ToArray()) as IEnumerable<Namespace>;
            }
        }
        public static Namespace GetNamespace(ModuleDefinition mod, string ns)
        {
            return GetNamespaces(mod).Single(_ => _.Name == ns);
        }
        public static void ResetNamespaces(ModuleDefinition mod)
        {
            if ((mod as IAnnotationProvider).Annotations.Contains(NS))
                (mod as IAnnotationProvider).Annotations.Remove(NS);
        }

        public static void AddChildren(object obj, IList<AsmTreeModel> children)
        {
            if (obj is TypeDefinition)
            {
                TypeDefinition typeDef = (TypeDefinition)obj;

                foreach (TypeDefinition nested in from TypeDefinition x in typeDef.NestedTypes orderby x.Name select x)
                    children.Add(new AsmTreeModel(nested));
                foreach (MethodDefinition method in from MethodDefinition x in typeDef.Methods
                                                    orderby x.Name
                                                    orderby x.Name == ".cctor" ? 0 : (x.Name == ".ctor" ? 1 : 2)
                                                    where x.SemanticsAttributes == MethodSemanticsAttributes.None
                                                    select x)
                    children.Add(new AsmTreeModel(method));
                foreach (PropertyDefinition prop in from PropertyDefinition x in typeDef.Properties orderby x.Name select x)
                    children.Add(new AsmTreeModel(prop));
                foreach (EventDefinition evt in from EventDefinition x in typeDef.Events orderby x.Name select x)
                    children.Add(new AsmTreeModel(evt));
                foreach (FieldDefinition field in from FieldDefinition x in typeDef.Fields orderby x.Name select x)
                    children.Add(new AsmTreeModel(field));
            }
            else if (obj is AssemblyDefinition)
            {
                foreach (ModuleDefinition mod in from ModuleDefinition x in (obj as AssemblyDefinition).Modules orderby x.Name select x)
                    children.Add(new AsmTreeModel(mod));
            }
            else if (obj is ModuleDefinition)
            {
                foreach (var i in GetNamespaces(obj as ModuleDefinition))
                    children.Add(new AsmTreeModel(i));
            }
            else if (obj is Namespace)
            {
                foreach (var i in obj as Namespace)
                    children.Add(new AsmTreeModel(i));
            }
            else if (obj is PropertyDefinition)
            {
                PropertyDefinition propDef = (PropertyDefinition)obj;
                if (propDef.GetMethod != null) children.Add(new AsmTreeModel(propDef.GetMethod));
                if (propDef.SetMethod != null) children.Add(new AsmTreeModel(propDef.SetMethod));
                if (propDef.HasOtherMethods)
                    foreach (var i in propDef.OtherMethods)
                        children.Add(new AsmTreeModel(i));
            }
            else if (obj is EventDefinition)
            {
                EventDefinition evtDef = (EventDefinition)obj;
                if (evtDef.AddMethod != null) children.Add(new AsmTreeModel(evtDef.AddMethod));
                if (evtDef.RemoveMethod != null) children.Add(new AsmTreeModel(evtDef.RemoveMethod));
                if (evtDef.InvokeMethod != null) children.Add(new AsmTreeModel(evtDef.InvokeMethod));
                if (evtDef.HasOtherMethods)
                    foreach (var i in evtDef.OtherMethods)
                        children.Add(new AsmTreeModel(i));
            }
        }

        public static IEnumerable<IAnnotationProvider> GetChildren(IAnnotationProvider obj)
        {
            if (obj is TypeDefinition)
            {
                TypeDefinition typeDef = (TypeDefinition)obj;

                foreach (TypeDefinition nested in from TypeDefinition x in typeDef.NestedTypes orderby x.Name select x)
                    yield return nested;
                foreach (MethodDefinition method in from MethodDefinition x in typeDef.Methods
                                                    orderby x.Name
                                                    orderby x.Name == ".cctor" ? 0 : (x.Name == ".ctor" ? 1 : 2)
                                                    where x.SemanticsAttributes == MethodSemanticsAttributes.None
                                                    select x)
                    yield return method;
                foreach (PropertyDefinition prop in from PropertyDefinition x in typeDef.Properties orderby x.Name select x)
                    yield return prop;
                foreach (EventDefinition evt in from EventDefinition x in typeDef.Events orderby x.Name select x)
                    yield return evt;
                foreach (FieldDefinition field in from FieldDefinition x in typeDef.Fields orderby x.Name select x)
                    yield return field;
            }
            else if (obj is AssemblyDefinition)
            {
                foreach (ModuleDefinition mod in from ModuleDefinition x in (obj as AssemblyDefinition).Modules orderby x.Name select x)
                    yield return mod;
            }
            else if (obj is ModuleDefinition)
            {
                foreach (var i in GetNamespaces(obj as ModuleDefinition))
                    yield return i;
            }
            else if (obj is Namespace)
            {
                foreach (var i in obj as Namespace)
                    yield return i;
            }
            else if (obj is PropertyDefinition)
            {
                PropertyDefinition propDef = (PropertyDefinition)obj;
                if (propDef.GetMethod != null) yield return propDef.GetMethod;
                if (propDef.SetMethod != null) yield return propDef.SetMethod;
                if (propDef.HasOtherMethods)
                    foreach (var i in propDef.OtherMethods)
                        yield return i;
            }
            else if (obj is EventDefinition)
            {
                EventDefinition evtDef = (EventDefinition)obj;
                if (evtDef.AddMethod != null) yield return evtDef.AddMethod;
                if (evtDef.RemoveMethod != null) yield return evtDef.RemoveMethod;
                if (evtDef.InvokeMethod != null) yield return evtDef.InvokeMethod;
                if (evtDef.HasOtherMethods)
                    foreach (var i in evtDef.OtherMethods)
                        yield return i;
            }
        }
    }
}
