using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Collections.Generic;
using Mono.Cecil.Cil;

namespace Confuser.Core.Analyzers
{
    partial class NameAnalyzer
    {
        void AnalyzeCodes(MethodDefinition mtd)
        {
            for (int i = 0; i < mtd.Body.Instructions.Count; i++)
            {
                Instruction inst = mtd.Body.Instructions[i];
                if (inst.OpCode.Code == Code.Ldtoken && inst.Operand is IMemberDefinition)
                {
                    Confuser.Database.AddEntry(DB_SRC, inst.Operand.ToString(), string.Format("ldtoken @ {0} => Not renamed", mtd));
                    (inst.Operand as IAnnotationProvider).Annotations[RenOk] = false;
                }

                if (inst.Operand is MethodReference ||
                    inst.Operand is FieldReference)
                {
                    if ((inst.Operand as MemberReference).DeclaringType is TypeSpecification && ((inst.Operand as MemberReference).DeclaringType as TypeSpecification).GetElementType() is TypeDefinition)
                    {
                        IMemberDefinition memDef;
                        if (inst.Operand is MethodReference && (memDef = (inst.Operand as MethodReference).GetElementMethod().Resolve()) != null)
                            ((memDef as IAnnotationProvider).Annotations[RenRef] as List<IReference>).Add(new SpecificationReference(inst.Operand as MemberReference));
                        else if (inst.Operand is FieldReference && (memDef = (inst.Operand as FieldReference).Resolve()) != null)
                            ((memDef as IAnnotationProvider).Annotations[RenRef] as List<IReference>).Add(new SpecificationReference(inst.Operand as MemberReference));
                    }
                    else if (inst.Operand is MethodReference)
                    {
                        MethodReference refer = inst.Operand as MethodReference;
                        string id = refer.DeclaringType.FullName + "::" + refer.Name;
                        if (Database.Reflections.ContainsKey(id))
                        {
                            ReflectionMethod Rmtd = Database.Reflections[id];
                            Instruction memInst;
                            MemberReference mem = StackTrace(i, mtd.Body, Rmtd, mtd.Module, out memInst);
                            if (mem != null)
                                ((mem as IAnnotationProvider).Annotations[RenRef] as List<IReference>).Add(new ReflectionReference(memInst));
                        }
                    }
                    if (ivtRefs.ContainsKey((inst.Operand as MemberReference).MetadataToken))
                    {
                        IMemberDefinition memDef;
                        if (inst.Operand is TypeReference && (memDef = (inst.Operand as TypeReference).Resolve()) != null)
                            ((memDef as IAnnotationProvider).Annotations[RenRef] as List<IReference>).Add(new IvtMemberReference(inst.Operand as MemberReference));
                        else if (inst.Operand is MethodReference && (memDef = (inst.Operand as MethodReference).Resolve()) != null)
                            ((memDef as IAnnotationProvider).Annotations[RenRef] as List<IReference>).Add(new IvtMemberReference(inst.Operand as MemberReference));
                        else if (inst.Operand is FieldReference && (memDef = (inst.Operand as FieldReference).Resolve()) != null)
                            ((memDef as IAnnotationProvider).Annotations[RenRef] as List<IReference>).Add(new IvtMemberReference(inst.Operand as MemberReference));
                    }
                }
            }
        }

        MemberReference StackTrace(int idx, MethodBody body, ReflectionMethod mtd, ModuleDefinition scope, out Instruction memInst)
        {
            memInst = null;
            var insts = body.Instructions;
            int count = ((insts[idx].Operand as MethodReference).HasThis ? 1 : 0) + (insts[idx].Operand as MethodReference).Parameters.Count;
            if (insts[idx].OpCode.Code == Code.Newobj)
                count--;
            int c = 0;
            for (idx--; idx >= 0; idx--)
            {
                if (count == c) break;
                Instruction inst = insts[idx];
                switch (inst.OpCode.Code)
                {
                    case Code.Ldc_I4:
                    case Code.Ldc_I8:
                    case Code.Ldc_R4:
                    case Code.Ldc_R8:
                    case Code.Ldstr:
                        c++; break;
                    case Code.Call:
                    case Code.Callvirt:
                        {
                            MethodReference target = (inst.Operand as MethodReference);
                            c -= (target.HasThis ? 1 : 0) + target.Parameters.Count;
                            if (target.ReturnType.FullName != "System.Void")
                                c++;
                            break;
                        }
                    case Code.Newobj:
                        {
                            MethodReference target = (inst.Operand as MethodReference);
                            c -= target.Parameters.Count - 1;
                            break;
                        }
                    case Code.Pop:
                        c--; break;
                    case Code.Ldarg:
                        c++; break;
                    case Code.Ldfld:
                        c++; break;
                    case Code.Ldloc:
                        c++; break;
                    case Code.Ldnull:
                        c++; break;
                    case Code.Starg:
                    case Code.Stfld:
                    case Code.Stloc:
                        c--; break;
                    case Code.Ldtoken:
                        c++; break;
                    default:
                        FollowStack(inst.OpCode, ref c); break;
                }
            }

            return StackTrace2(idx + 1, count, body, mtd, scope, out memInst);
        }
        MemberReference StackTrace2(int idx, int c, MethodBody body, ReflectionMethod mtd, ModuleDefinition scope, out Instruction memInst)
        {
            memInst = null;
            int count = c;
            Stack<object> stack = new Stack<object>();
            for (int i = idx; ; i++)
            {
                if (stack.Count == count) break;
                Instruction inst = body.Instructions[i];
                switch (inst.OpCode.Code)
                {
                    case Code.Ldc_I4:
                    case Code.Ldc_I8:
                    case Code.Ldc_R4:
                    case Code.Ldc_R8:
                    case Code.Ldstr:
                        stack.Push(inst.Operand); break;
                    case Code.Call:
                    case Code.Callvirt:
                        {
                            MethodReference target = (inst.Operand as MethodReference);
                            if (target.Name == "GetTypeFromHandle" && target.DeclaringType.FullName == "System.Type")
                                break;
                            int cc = -(target.HasThis ? 1 : 0) - target.Parameters.Count;
                            for (int ii = cc; ii != 0; ii++)
                                stack.Pop();
                            if (target.ReturnType.FullName != "System.Void")
                                stack.Push(target.ReturnType);
                            break;
                        }
                    case Code.Newobj:
                        {
                            MethodReference target = (inst.Operand as MethodReference);
                            for (int ii = -target.Parameters.Count; ii != 0; ii++)
                                stack.Pop();
                            stack.Push(target.DeclaringType);
                            break;
                        }
                    case Code.Pop:
                        stack.Pop(); break;
                    case Code.Ldarg:
                        stack.Push((inst.Operand as ParameterReference).ParameterType); break;
                    case Code.Ldfld:
                        stack.Push((inst.Operand as FieldReference).FieldType); break;
                    case Code.Ldloc:
                        stack.Push((inst.Operand as VariableReference).VariableType); break;
                    case Code.Ldnull:
                        stack.Push(null); break;
                    case Code.Starg:
                    case Code.Stfld:
                    case Code.Stloc:
                        stack.Pop(); break;
                    case Code.Ldtoken:
                        stack.Push(inst.Operand); break;
                    default:
                        FollowStack(inst.OpCode, stack); break;
                }
            }

            object[] objs = stack.ToArray();
            Array.Reverse(objs);

            string mem = null;
            TypeDefinition type = null;
            Resource res = null;
            for (int i = 0; i < mtd.paramLoc.Length; i++)
            {
                if (mtd.paramLoc[i] >= objs.Length) return null;
                object param = objs[mtd.paramLoc[i]];
                switch (mtd.paramType[i])
                {
                    case "Target":
                        if ((mem = param as string) == null) return null;
                        memInst = StackTrace3(idx, c, body.Instructions, mtd.paramLoc[i]);
                        break;
                    case "Type":
                    case "This":
                        if (param as TypeDefinition != null)
                            type = param as TypeDefinition;
                        else
                            type = body.Method.DeclaringType;
                        break;
                    case "TargetType":
                        if (!(param is string)) return null;
                        type = scope.GetType(param as string);
                        break;
                    case "TargetResource":
                        if (!(param is string)) return null;
                        res = scope.Resources.FirstOrDefault((r) => (r.Name == param as string + ".resources"));
                        memInst = StackTrace3(idx, c, body.Instructions, mtd.paramLoc[i]);
                        break;
                }
            }
            if (mem == null && type == null && res == null) return null;

            if (res != null)
            {
                ((res as IAnnotationProvider).Annotations[RenRef] as List<IReference>).Add(new ResourceNameReference(memInst));
                return null;
            }

            if (mem != null && type != null)
            {
                foreach (FieldDefinition fld in type.Fields)
                    if (fld.Name == mem)
                        return fld;
                foreach (MethodDefinition mtd1 in type.Methods)
                    if (mtd1.Name == mem)
                        return mtd1;
                foreach (PropertyDefinition prop in type.Properties)
                    if (prop.Name == mem)
                        return prop;
                foreach (EventDefinition evt in type.Events)
                    if (evt.Name == mem)
                        return evt;
            }
            else if (type != null)
            {
                memInst = StackTrace3(idx, c, body.Instructions, mtd.paramLoc[Array.IndexOf(mtd.paramType, "TargetType")]);
                return type;
            }
            return null;
        }
        Instruction StackTrace3(int idx, int count, Collection<Instruction> insts, int c)
        {
            c = count - c;
            for (; ; idx++)
            {
                if (count < c) break;
                Instruction inst = insts[idx];
                switch (inst.OpCode.Code)
                {
                    case Code.Ldc_I4:
                    case Code.Ldc_I8:
                    case Code.Ldc_R4:
                    case Code.Ldc_R8:
                    case Code.Ldstr:
                        count--; break;
                    case Code.Call:
                    case Code.Callvirt:
                        {
                            MethodReference target = (inst.Operand as MethodReference);
                            count += (target.HasThis ? 1 : 0) + target.Parameters.Count;
                            if (target.ReturnType.FullName != "System.Void")
                                count--;
                            break;
                        }
                    case Code.Newobj:
                        {
                            MethodReference target = (inst.Operand as MethodReference);
                            c += target.Parameters.Count - 1;
                            break;
                        }
                    case Code.Pop:
                        count++; break;
                    case Code.Ldarg:
                        count--; break;
                    case Code.Ldfld:
                        count--; break;
                    case Code.Ldloc:
                        count--; break;
                    case Code.Ldnull:
                        count--; break;
                    case Code.Starg:
                    case Code.Stfld:
                    case Code.Stloc:
                        count++; break;
                    case Code.Ldtoken:
                        count--; break;
                    default:
                        int cc = count;
                        FollowStack(inst.OpCode, ref count);
                        count -= count - cc;
                        break;
                }
            }
            return insts[idx - 1];
        }

        void FollowStack(OpCode op, Stack<object> stack)
        {
            switch (op.StackBehaviourPop)
            {
                case StackBehaviour.Pop1:
                case StackBehaviour.Pop1_pop1:
                case StackBehaviour.Popref:
                case StackBehaviour.Popi:
                    stack.Pop(); break;
                case StackBehaviour.Popi_pop1:
                case StackBehaviour.Popi_popi:
                case StackBehaviour.Popi_popi8:
                case StackBehaviour.Popi_popr4:
                case StackBehaviour.Popi_popr8:
                case StackBehaviour.Popref_pop1:
                case StackBehaviour.Popref_popi:
                    stack.Pop(); stack.Pop(); break;
                case StackBehaviour.Popi_popi_popi:
                case StackBehaviour.Popref_popi_popi:
                case StackBehaviour.Popref_popi_popi8:
                case StackBehaviour.Popref_popi_popr4:
                case StackBehaviour.Popref_popi_popr8:
                case StackBehaviour.Popref_popi_popref:
                    stack.Pop(); stack.Pop(); stack.Pop(); break;
                case StackBehaviour.PopAll:
                    stack.Clear(); break;
                case StackBehaviour.Varpop:
                    throw new InvalidOperationException();
            }
            switch (op.StackBehaviourPush)
            {
                case StackBehaviour.Push1:
                case StackBehaviour.Pushi:
                case StackBehaviour.Pushi8:
                case StackBehaviour.Pushr4:
                case StackBehaviour.Pushr8:
                case StackBehaviour.Pushref:
                    stack.Push(null); break;
                case StackBehaviour.Push1_push1:
                    stack.Push(null); stack.Push(null); break;
                case StackBehaviour.Varpush:
                    throw new InvalidOperationException();
            }
        }
        void FollowStack(OpCode op, ref int stack)
        {
            switch (op.StackBehaviourPop)
            {
                case StackBehaviour.Pop1:
                case StackBehaviour.Pop1_pop1:
                case StackBehaviour.Popref:
                case StackBehaviour.Popi:
                    stack--; break;
                case StackBehaviour.Popi_pop1:
                case StackBehaviour.Popi_popi:
                case StackBehaviour.Popi_popi8:
                case StackBehaviour.Popi_popr4:
                case StackBehaviour.Popi_popr8:
                case StackBehaviour.Popref_pop1:
                case StackBehaviour.Popref_popi:
                    stack -= 2; break;
                case StackBehaviour.Popi_popi_popi:
                case StackBehaviour.Popref_popi_popi:
                case StackBehaviour.Popref_popi_popi8:
                case StackBehaviour.Popref_popi_popr4:
                case StackBehaviour.Popref_popi_popr8:
                case StackBehaviour.Popref_popi_popref:
                    stack -= 3; break;
                case StackBehaviour.PopAll:
                    stack = 0; break;
                case StackBehaviour.Varpop:
                    throw new InvalidOperationException();
            }
            switch (op.StackBehaviourPush)
            {
                case StackBehaviour.Push1:
                case StackBehaviour.Pushi:
                case StackBehaviour.Pushi8:
                case StackBehaviour.Pushr4:
                case StackBehaviour.Pushr8:
                case StackBehaviour.Pushref:
                    stack++; break;
                case StackBehaviour.Push1_push1:
                    stack += 2; break;
                case StackBehaviour.Varpush:
                    throw new InvalidOperationException();
            }
        }
    }
}
