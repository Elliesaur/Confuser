using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;

namespace Confuser.AsmSelector
{
    class Texter
    {
        static string GetDisplayName(TypeReference typeRef, bool full)
        {
            StringBuilder ret = new StringBuilder();
            WriteTypeReference(ret, typeRef, full);
            return ret.ToString();
        }
        static void WriteTypeReference(StringBuilder sb, TypeReference typeRef, bool full)
        {
            WriteTypeReference(sb, typeRef, false, full);
        }
        static void WriteTypeReference(StringBuilder sb, TypeReference typeRef, bool isGenericInstance, bool full)
        {
            if (typeRef is TypeSpecification)
            {
                TypeSpecification typeSpec = typeRef as TypeSpecification;
                if (typeSpec is ArrayType)
                {
                    WriteTypeReference(sb, typeSpec.ElementType, full);
                    sb.Append("[");
                    var dims = (typeSpec as ArrayType).Dimensions;
                    for (int i = 0; i < dims.Count; i++)
                    {
                        if (i != 0) sb.Append(", ");
                        if (dims[i].IsSized)
                        {
                            sb.Append(dims[i].LowerBound.HasValue ?
                                            dims[i].LowerBound.ToString() : ".");
                            sb.Append("..");
                            sb.Append(dims[i].UpperBound.HasValue ?
                                            dims[i].UpperBound.ToString() : ".");
                        }
                    }
                    sb.Append("]");
                }
                else if (typeSpec is ByReferenceType)
                {
                    WriteTypeReference(sb, typeSpec.ElementType, full);
                    sb.Append("&");
                }
                else if (typeSpec is PointerType)
                {
                    WriteTypeReference(sb, typeSpec.ElementType, full);
                    sb.Append("*");
                }
                else if (typeSpec is OptionalModifierType)
                {
                    WriteTypeReference(sb, typeSpec.ElementType, full);
                    sb.Append(" ");
                    sb.Append("modopt");
                    sb.Append("(");
                    WriteTypeReference(sb, (typeSpec as OptionalModifierType).ModifierType, full);
                    sb.Append(")");
                }
                else if (typeSpec is RequiredModifierType)
                {
                    WriteTypeReference(sb, typeSpec.ElementType, full);
                    sb.Append(" ");
                    sb.Append("modreq");
                    sb.Append("(");
                    WriteTypeReference(sb, (typeSpec as RequiredModifierType).ModifierType, full);
                    sb.Append(")");
                }
                else if (typeSpec is FunctionPointerType)
                {
                    FunctionPointerType funcPtr = typeSpec as FunctionPointerType;
                    WriteTypeReference(sb, funcPtr.ReturnType, full);
                    sb.Append(" *(");
                    for (int i = 0; i < funcPtr.Parameters.Count; i++)
                    {
                        if (i != 0) sb.Append(", ");
                        WriteTypeReference(sb, funcPtr.Parameters[i].ParameterType, full);
                    }
                    sb.Append(")");
                }
                else if (typeSpec is SentinelType)
                {
                    sb.Append("...");
                }
                else if (typeSpec is GenericInstanceType)
                {
                    WriteTypeReference(sb, typeSpec.ElementType, true);
                    sb.Append("<");
                    var args = (typeSpec as GenericInstanceType).GenericArguments;
                    for (int i = 0; i < args.Count; i++)
                    {
                        if (i != 0) sb.Append(", ");
                        WriteTypeReference(sb, args[i], full);
                    }
                    sb.Append(">");
                }
            }
            else if (typeRef is GenericParameter)
            {
                sb.Append((typeRef as GenericParameter).Name);
            }
            else
            {
                string name = typeRef.Name;
                var genParamsCount = 0;
                if (typeRef.HasGenericParameters)
                {
                    genParamsCount = typeRef.GenericParameters.Count - (typeRef.DeclaringType == null ? 0 : typeRef.DeclaringType.GenericParameters.Count);
                    string str = "`" + genParamsCount.ToString();
                    if (typeRef.Name.EndsWith(str)) name = typeRef.Name.Substring(0, typeRef.Name.Length - str.Length);
                }

                if (typeRef.IsNested)
                {
                    WriteTypeReference(sb, typeRef.DeclaringType, full);
                    sb.Append(".");
                    sb.Append(name);
                }
                else
                {
                    if (full)
                    {
                        sb.Append(typeRef.Namespace);
                        if (!string.IsNullOrEmpty(typeRef.Namespace)) sb.Append(".");
                    }
                    sb.Append(name);
                }
                if (typeRef.HasGenericParameters && genParamsCount != 0 && !isGenericInstance)
                {
                    sb.Append("<");
                    for (int i = typeRef.GenericParameters.Count - genParamsCount; i < typeRef.GenericParameters.Count; i++)
                    {
                        if (i != 0) sb.Append(", ");
                        WriteTypeReference(sb, typeRef.GenericParameters[i], full);
                    }
                    sb.Append(">");
                }
            }
        }

        public static string Text(object obj)
        {
            if (obj is AssemblyDefinition)
            {
                return (obj as AssemblyDefinition).Name.Name;
            }
            else if (obj is AssemblyNameReference)
            {
                return (obj as AssemblyNameReference).Name;
            }
            else if (obj is Resource)
            {
                return (obj as Resource).Name;
            }
            else if (obj is ModuleDefinition)
            {
                return (obj as ModuleDefinition).Name;
            }
            else if (obj is ModuleReference)
            {
                return (obj as ModuleReference).Name;
            }
            else if (obj is Namespace)
            {
                return string.IsNullOrEmpty((obj as Namespace).Name) ? "-" : (obj as Namespace).Name;
            }
            else if (obj is TypeReference)
            {
                return GetDisplayName(obj as TypeReference, false);
            }
            else if (obj is MethodReference)
            {
                var item = obj as MethodReference;
                var name = new StringBuilder();
                name.Append(item.Name);
                if (item.HasGenericParameters)
                {
                    name.Append("<");
                    for (int i = 0; i < item.GenericParameters.Count; i++)
                    {
                        if (i != 0) name.Append(", ");
                        name.Append(item.GenericParameters[i].Name);
                    }
                    name.Append(">");
                }

                name.Append("(");
                for (int i = 0; i < item.Parameters.Count; i++)
                {
                    if (i != 0) name.Append(", ");
                    name.Append(GetDisplayName(item.Parameters[i].ParameterType, false));
                }
                name.Append(")");
                if (item.Name != ".ctor" && item.Name != ".cctor")
                {
                    name.Append(" : ");
                    name.Append(GetDisplayName(item.ReturnType, false));
                }

                return name.ToString();
            }
            else if (obj is FieldReference)
            {
                var item = obj as FieldReference;
                return string.Format("{0} : {1}", item.Name, GetDisplayName(item.FieldType, false));
            }
            else if (obj is PropertyDefinition)
            {
                var item = obj as PropertyDefinition;
                StringBuilder name = new StringBuilder();
                name.Append(item.Name);
                if (item.HasParameters)
                {
                    name.Append("[");
                    for (int i = 0; i < item.Parameters.Count; i++)
                    {
                        if (i != 0) name.Append(", ");
                        name.Append(GetDisplayName(item.Parameters[i].ParameterType, false));
                    }
                    name.Append("]");
                }
                name.Append(" : ");
                name.Append(GetDisplayName(item.PropertyType, false));
                return name.ToString();
            }
            else if (obj is EventDefinition)
            {
                var item = obj as EventDefinition;
                return string.Format("{0} : {1}", item.Name, GetDisplayName(item.EventType, false));
            }
            else
                return obj.ToString();
        }
    }
}
