using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using Mono.Cecil;

namespace Confuser.AsmSelector
{
    class IconRenderer
    {
        static readonly Dictionary<string, object> res = new Dictionary<string, object>();
        static T GetResource<T>(string name) where T : class
        {
            if (res.ContainsKey(name)) return res[name] as T;

            T ret = App.Current.FindResource(name) as T;
            res[name] = ret;
            return ret;
        }

        static bool IsDelegate(TypeReference typeRef)
        {
            if (typeRef == null) return false;
            TypeDefinition typeDecl = typeRef.Resolve();
            if (typeDecl == null || typeDecl.BaseType == null) return false;
            return typeDecl.BaseType.Name == "MulticastDelegate" && typeDecl.BaseType.Namespace == "System";
        }
        static MethodAttributes GetPropVisibility(PropertyReference prop)
        {
            MethodAttributes ret = MethodAttributes.Public;
            PropertyDefinition Definition = prop.Resolve();
            if (Definition != null)
            {
                MethodReference setMethod = Definition.SetMethod;
                MethodDefinition setDecl = (setMethod == null) ? null : setMethod.Resolve();
                MethodReference getMethod = Definition.GetMethod;
                MethodDefinition getDecl = (getMethod == null) ? null : getMethod.Resolve();
                if ((setDecl != null) && (getDecl != null))
                {
                    if ((getDecl.Attributes & MethodAttributes.MemberAccessMask) == (setDecl.Attributes & MethodAttributes.MemberAccessMask))
                    {
                        ret = getDecl.Attributes & MethodAttributes.MemberAccessMask;
                    }
                    return ret;
                }
                if (setDecl != null)
                {
                    return setDecl.Attributes & MethodAttributes.MemberAccessMask;
                }
                if (getDecl != null)
                {
                    ret = getDecl.Attributes & MethodAttributes.MemberAccessMask;
                }
            }
            return ret;
        }
        static MethodAttributes GetEvtVisibility(EventReference evt)
        {
            MethodAttributes ret = MethodAttributes.Public;
            EventDefinition Definition = evt.Resolve();
            if (Definition != null)
            {
                MethodReference addMethod = Definition.AddMethod;
                MethodDefinition addDecl = (addMethod == null) ? null : addMethod.Resolve();
                MethodReference removeMethod = Definition.RemoveMethod;
                MethodDefinition removeDecl = (removeMethod == null) ? null : removeMethod.Resolve();
                MethodReference invokeMethod = Definition.InvokeMethod;
                MethodDefinition invokeDecl = (invokeMethod == null) ? null : invokeMethod.Resolve();
                if (((addDecl != null) && (removeDecl != null)) && (invokeDecl != null))
                {
                    if (((addDecl.Attributes & MethodAttributes.MemberAccessMask) == (removeDecl.Attributes & MethodAttributes.MemberAccessMask)) && ((addDecl.Attributes & MethodAttributes.MemberAccessMask) == (invokeDecl.Attributes & MethodAttributes.MemberAccessMask)))
                    {
                        return addDecl.Attributes & MethodAttributes.MemberAccessMask;
                    }
                }
                else if ((addDecl != null) && (removeDecl != null))
                {
                    if ((addDecl.Attributes & MethodAttributes.MemberAccessMask) == (removeDecl.Attributes & MethodAttributes.MemberAccessMask))
                    {
                        return addDecl.Attributes & MethodAttributes.MemberAccessMask;
                    }
                }
                else if ((addDecl != null) && (invokeDecl != null))
                {
                    if ((addDecl.Attributes & MethodAttributes.MemberAccessMask) == (invokeDecl.Attributes & MethodAttributes.MemberAccessMask))
                    {
                        return addDecl.Attributes & MethodAttributes.MemberAccessMask;
                    }
                }
                else if ((removeDecl != null) && (invokeDecl != null))
                {
                    if ((removeDecl.Attributes & MethodAttributes.MemberAccessMask) == (invokeDecl.Attributes & MethodAttributes.MemberAccessMask))
                    {
                        return removeDecl.Attributes & MethodAttributes.MemberAccessMask;
                    }
                }
                else
                {
                    if (addDecl != null)
                    {
                        return addDecl.Attributes & MethodAttributes.MemberAccessMask;
                    }
                    if (removeDecl != null)
                    {
                        return removeDecl.Attributes & MethodAttributes.MemberAccessMask;
                    }
                    if (invokeDecl != null)
                    {
                        return invokeDecl.Attributes & MethodAttributes.MemberAccessMask;
                    }
                }

            }
            return ret;
        }
        static bool IsStatic(EventReference prop)
        {
            bool flag = false;
            EventDefinition Definition = prop.Resolve();
            if (Definition != null)
            {
                MethodReference addMethod = Definition.AddMethod;
                MethodDefinition addDecl = (addMethod == null) ? null : addMethod.Resolve();
                MethodReference removeMethod = Definition.RemoveMethod;
                MethodDefinition removeDecl = (removeMethod == null) ? null : removeMethod.Resolve();
                MethodReference invokeMethod = Definition.InvokeMethod;
                MethodDefinition invokeDecl = (invokeMethod == null) ? null : invokeMethod.Resolve();
                flag |= (addDecl != null) && addDecl.IsStatic;
                flag |= (removeDecl != null) && removeDecl.IsStatic;
                flag |= (invokeDecl != null) && invokeDecl.IsStatic;
            }
            return flag;
        }
        static bool IsStatic(PropertyReference prop)
        {
            bool flag = false;
            PropertyDefinition Definition = prop.Resolve();
            if (Definition != null)
            {
                MethodReference setMethod = Definition.SetMethod;
                MethodDefinition addDecl = (setMethod == null) ? null : setMethod.Resolve();
                MethodReference getMethod = Definition.GetMethod;
                MethodDefinition getDecl = (getMethod == null) ? null : getMethod.Resolve();
                flag |= (addDecl != null) && addDecl.IsStatic;
                flag |= (getDecl != null) && getDecl.IsStatic;
            }
            return flag;
        }

        public static void DrawIcon(object obj, DrawingContext g, Rect bound)
        {
            ImageSource ico = null;
            ImageSource ovr = null;
            ImageSource vis = null;
            if (obj is AssemblyDefinition || obj is AssemblyNameReference)
            {
                ico = GetResource<BitmapSource>("assembly");
            }
            else if (obj is ModuleReference)
            {
                ico = GetResource<BitmapSource>("module");
            }
            else if (obj is Resource)
            {
                ico = GetResource<BitmapSource>("file");
            }
            else if (obj is Namespace)
            {
                ico = GetResource<BitmapSource>("namespace");
            }
            else if (obj is TypeDefinition)
            {
                TypeDefinition typeDef = (TypeDefinition)obj;
                ico = GetResource<BitmapSource>("type");
                if (typeDef.IsInterface)
                {
                    ico = GetResource<BitmapSource>("interface");
                }
                else if (typeDef.BaseType != null)
                {
                    if (typeDef.IsEnum)
                    {
                        ico = GetResource<BitmapSource>("enum");
                    }
                    else if (typeDef.IsValueType && !typeDef.IsAbstract)
                    {
                        ico = GetResource<BitmapSource>("valuetype");
                    }
                    else if (IsDelegate(typeDef))
                    {
                        ico = GetResource<BitmapSource>("delegate");
                    }
                }
                switch (typeDef.Attributes & TypeAttributes.VisibilityMask)
                {
                    case TypeAttributes.NotPublic:
                    case TypeAttributes.NestedAssembly:
                    case TypeAttributes.NestedFamANDAssem:
                        vis = GetResource<BitmapSource>("internal");
                        break;
                    case TypeAttributes.Public:
                    case TypeAttributes.NestedPublic:
                        vis = null;
                        break;
                    case TypeAttributes.NestedPrivate:
                        vis = GetResource<BitmapSource>("private");
                        break;
                    case TypeAttributes.NestedFamily:
                        vis = GetResource<BitmapSource>("protected");
                        break;
                    case TypeAttributes.NestedFamORAssem:
                        vis = GetResource<BitmapSource>("famasm");
                        break;
                }
            }
            else if (obj is TypeReference)
            {
                ico = GetResource<BitmapSource>("type");
            }
            else if (obj is FieldDefinition)
            {
                FieldDefinition field = (FieldDefinition)obj;
                ico = GetResource<BitmapSource>("field");
                if (field.IsStatic)
                {
                    if (field.DeclaringType.IsEnum)
                        ico = GetResource<BitmapSource>("constant");
                    else
                        ovr = GetResource<BitmapSource>("static");
                }

                switch (field.Attributes & FieldAttributes.FieldAccessMask)
                {
                    case FieldAttributes.CompilerControlled:
                    case FieldAttributes.Private:
                        vis = GetResource<BitmapSource>("private");
                        break;
                    case FieldAttributes.FamANDAssem:
                    case FieldAttributes.Assembly:
                        vis = GetResource<BitmapSource>("internal");
                        break;
                    case FieldAttributes.Family:
                        vis = GetResource<BitmapSource>("protected");
                        break;
                    case FieldAttributes.FamORAssem:
                        vis = GetResource<BitmapSource>("famasm");
                        break;
                    case FieldAttributes.Public:
                        vis = null;
                        break;
                }
            }
            else if (obj is FieldReference)
            {
                ico = GetResource<BitmapSource>("field");
            }
            else if (obj is MethodDefinition)
            {
                ico = GetResource<BitmapSource>("method");
                if (!((obj as MethodReference).DeclaringType is ArrayType))
                {
                    MethodDefinition method = (MethodDefinition)obj;
                    string name = method.Name;
                    if ((name == ".ctor") || (name == ".cctor"))
                    {
                        ico = GetResource<BitmapSource>("constructor");
                    }
                    else if (method.IsVirtual && !method.IsAbstract)
                    {
                        ico = GetResource<BitmapSource>("omethod");
                    }
                    if (method.IsStatic)
                    {
                        ovr = GetResource<BitmapSource>("static");
                    }
                    switch (method.Attributes & MethodAttributes.MemberAccessMask)
                    {
                        case MethodAttributes.CompilerControlled:
                        case MethodAttributes.Private:
                            vis = GetResource<BitmapSource>("private");
                            break;
                        case MethodAttributes.FamANDAssem:
                        case MethodAttributes.Assembly:
                            vis = GetResource<BitmapSource>("internal");
                            break;
                        case MethodAttributes.Family:
                            vis = GetResource<BitmapSource>("protected");
                            break;
                        case MethodAttributes.FamORAssem:
                            vis = GetResource<BitmapSource>("famasm");
                            break;
                        case MethodAttributes.Public:
                            vis = null;
                            break;
                    }
                }
            }
            else if (obj is MethodReference)
            {
                ico = GetResource<BitmapSource>("method");
            }
            else if (obj is PropertyDefinition)
            {
                PropertyDefinition prop = (PropertyDefinition)obj;
                MethodReference getMethod = prop.GetMethod;
                MethodDefinition getDecl = (getMethod == null) ? null : getMethod.Resolve();
                MethodReference setMethod = prop.SetMethod;
                MethodDefinition setDecl = (setMethod == null) ? null : setMethod.Resolve();
                ico = GetResource<BitmapSource>("property");
                if (getDecl != null && setDecl == null)
                {
                    ico = GetResource<BitmapSource>("propget");
                }
                else if (setDecl != null && getDecl == null)
                {
                    ico = GetResource<BitmapSource>("propset");
                }
                if (IsStatic(prop))
                {
                    ovr = GetResource<BitmapSource>("static");
                }
                switch (GetPropVisibility(prop))
                {
                    case MethodAttributes.CompilerControlled:
                    case MethodAttributes.Private:
                        vis = GetResource<BitmapSource>("private");
                        break;
                    case MethodAttributes.FamANDAssem:
                    case MethodAttributes.Assembly:
                        vis = GetResource<BitmapSource>("internal");
                        break;
                    case MethodAttributes.Family:
                        vis = GetResource<BitmapSource>("protected");
                        break;
                    case MethodAttributes.FamORAssem:
                        vis = GetResource<BitmapSource>("famasm");
                        break;
                    case MethodAttributes.Public:
                        vis = null;
                        break;
                }
            }
            else if (obj is PropertyReference)
            {
                ico = GetResource<BitmapSource>("property");
            }
            else if (obj is EventDefinition)
            {
                ico = GetResource<BitmapSource>("event");
                if (IsStatic(obj as EventReference))
                {
                    ovr = GetResource<BitmapSource>("static");
                }
                switch (GetEvtVisibility(obj as EventReference))
                {
                    case MethodAttributes.CompilerControlled:
                    case MethodAttributes.Private:
                        vis = GetResource<BitmapSource>("private");
                        break;
                    case MethodAttributes.FamANDAssem:
                    case MethodAttributes.Assembly:
                        vis = GetResource<BitmapSource>("internal");
                        break;
                    case MethodAttributes.Family:
                        vis = GetResource<BitmapSource>("protected");
                        break;
                    case MethodAttributes.FamORAssem:
                        vis = GetResource<BitmapSource>("famasm");
                        break;
                    case MethodAttributes.Public:
                        vis = null;
                        break;
                }
            }
            else if (obj is EventReference)
            {
                ico = GetResource<BitmapSource>("event");
            }
            else
                throw new NotSupportedException();

            if (ico != null) g.DrawImage(ico, bound);
            if (ovr != null) g.DrawImage(ovr, bound);
            if (vis != null) g.DrawImage(vis, bound);
        }
    }
}
