using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using Mono.Cecil;

namespace Confuser.AsmSelector
{
    class Colorizer
    {
        public static readonly Brush Public = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xEE, 0xEE));
        public static readonly Brush NonPublic = new SolidColorBrush(Color.FromArgb(0xFF, 0xAA, 0xAA, 0xAA));

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

        public static Brush Colorize(object obj)
        {
            if (obj is Resource)
            {
                return (obj as Resource).IsPublic ? Public : NonPublic;
            }
            else if (obj is TypeDefinition)
            {
                TypeDefinition typeDef = (TypeDefinition)obj;
                switch (typeDef.Attributes & TypeAttributes.VisibilityMask)
                {
                    case TypeAttributes.NotPublic:
                    case TypeAttributes.NestedAssembly:
                    case TypeAttributes.NestedFamANDAssem:
                    case TypeAttributes.NestedPrivate:
                    case TypeAttributes.NestedFamily:
                    case TypeAttributes.NestedFamORAssem:
                        return NonPublic;
                }
                return Public;
            }
            else if (obj is TypeReference)
            {
                return Public;
            }
            else if (obj is FieldDefinition)
            {
                FieldDefinition field = (FieldDefinition)obj;
                switch (field.Attributes & FieldAttributes.FieldAccessMask)
                {
                    case FieldAttributes.CompilerControlled:
                    case FieldAttributes.Private:
                    case FieldAttributes.FamANDAssem:
                    case FieldAttributes.Assembly:
                    case FieldAttributes.Family:
                    case FieldAttributes.FamORAssem:
                        return NonPublic;
                }
                return Public;
            }
            else if (obj is FieldReference)
            {
                return Public;
            }
            else if (obj is MethodDefinition)
            {
                MethodDefinition method = (MethodDefinition)obj;
                switch (method.Attributes & MethodAttributes.MemberAccessMask)
                {
                    case MethodAttributes.CompilerControlled:
                    case MethodAttributes.Private:
                    case MethodAttributes.FamANDAssem:
                    case MethodAttributes.Assembly:
                    case MethodAttributes.Family:
                    case MethodAttributes.FamORAssem:
                        return NonPublic;
                }
                return Public;
            }
            else if (obj is MethodReference)
            {
                return Public;
            }
            else if (obj is PropertyDefinition)
            {
                PropertyDefinition prop = (PropertyDefinition)obj;
                switch (GetPropVisibility(prop))
                {
                    case MethodAttributes.CompilerControlled:
                    case MethodAttributes.Private:
                    case MethodAttributes.FamANDAssem:
                    case MethodAttributes.Assembly:
                    case MethodAttributes.Family:
                    case MethodAttributes.FamORAssem:
                        return NonPublic;
                }
                return Public;
            }
            else if (obj is PropertyReference)
            {
                return Public;
            }
            else if (obj is EventDefinition)
            {
                switch (GetEvtVisibility(obj as EventReference))
                {
                    case MethodAttributes.CompilerControlled:
                    case MethodAttributes.Private:
                    case MethodAttributes.FamANDAssem:
                    case MethodAttributes.Assembly:
                    case MethodAttributes.Family:
                    case MethodAttributes.FamORAssem:
                        return NonPublic;
                }
                return Public;
            }
            else if (obj is EventReference)
            {
                return Public;
            }
            else
                return Public;
        }
    }
}
