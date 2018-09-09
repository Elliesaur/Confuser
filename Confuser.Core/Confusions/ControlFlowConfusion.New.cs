using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Confuser.Core.Poly;
using System.IO;
using Confuser.Core.Poly.Visitors;
using System.Collections.Specialized;
using Mono.Cecil.Metadata;
using Mono;
using Confuser.Core.Poly.Math;
using Confuser.Core.Poly.Strings;
using Confuser.Core.Poly.Dynamics;

namespace Confuser.Core.Confusions
{
    public class ControlFlowConfusion : StructurePhase, IConfusion
    {
        //private StringGenerator stringGen;

        enum ScopeType
        {

            /*
             *            | 00      | 01      | 10      | 11
             *            |  None   |   Try   | Handler | Filter  
             * -----------|---------|---------|---------|----------
             * 00  Only   |  0000   | 0100    | 1000    | 1100
             * 01  Body   |  0000   | 0101    | 1001    | 1101
             * 10  Start  |  0000   | 0110    | 1010    | 1110
             * 11  End    |  0000   | 0111    | 1011    | 1111
             * 
             * */
            None = 0,

            TryOnly = 4,
            TryBody = 5,
            TryStart = 6,
            TryEnd = 7,

            HandlerOnly = 8,
            HandlerBody = 9,
            HandlerStart = 10,
            HandlerEnd = 11,

            FilterOnly = 12,
            FilterBody = 13,
            FilterStart = 14,
            FilterEnd = 15
        }
        struct _Scope
        {
            public _Scope(ExceptionHandler eh, ScopeType t)
            {
                Scopes = new List<Tuple<ExceptionHandler, ScopeType>>() 
                { 
                    new Tuple<ExceptionHandler, ScopeType>(eh, t)
                };
            }

            public List<Tuple<ExceptionHandler, ScopeType>> Scopes;

            public static bool operator ==(_Scope a, _Scope b)
            {
                if (a.Scopes.Count != b.Scopes.Count)
                    return false;

                for (int i = 0; i < a.Scopes.Count; i++)
                    if (a.Scopes[i].Item1 != b.Scopes[i].Item1 ||
                        a.Scopes[i].Item2 != b.Scopes[i].Item2)
                        return false;
                return true;
            }

            public static bool operator !=(_Scope a, _Scope b)
            {
                if (a.Scopes.Count != b.Scopes.Count)
                    return true;

                for (int i = 0; i < a.Scopes.Count; i++)
                    if (a.Scopes[i].Item1 != b.Scopes[i].Item1 ||
                        a.Scopes[i].Item2 != b.Scopes[i].Item2)
                        return true;
                return false;
            }

            public static _Scope operator +(_Scope a, _Scope b)
            {
                _Scope ret = new _Scope();
                ret.Scopes = new List<Tuple<ExceptionHandler, ScopeType>>();
                if (a.Scopes != null)
                    ret.Scopes.AddRange(a.Scopes);
                if (b.Scopes != null)
                    ret.Scopes.AddRange(b.Scopes);
                return ret;
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                return (obj is _Scope) && ((_Scope)obj) == this;
            }

            public override string ToString()
            {
                StringBuilder ret = new StringBuilder();
                for (int i = 0; i < Scopes.Count; i++)
                {
                    if (i != 0) ret.Append(",");
                    ret.Append((Scopes[i].Item1 == null ? "00000000" : Scopes[i].Item1.GetHashCode().ToString("X8")) + "_" + Scopes[i].Item2.ToString());
                } return ret.ToString();
            }
        }
        class Scope
        {
            public _Scope Type;
            public Instruction[] Instructions;
        }
        class ScopeDetector
        {
            public static bool IsOnly(ScopeType type)
            {
                return ((int)type & 3) == 0;
            }
            public static bool IsBody(ScopeType type)
            {
                return ((int)type & 3) == 1;
            }
            public static bool IsStart(ScopeType type)
            {
                return ((int)type & 3) == 2;
            }
            public static bool IsEnd(ScopeType type)
            {
                return ((int)type & 3) == 3;
            }

            public static bool IsTry(ScopeType type)
            {
                return ((int)type & 12) == 4;
            }
            public static bool IsHandler(ScopeType type)
            {
                return ((int)type & 12) == 8;
            }
            public static bool IsFilter(ScopeType type)
            {
                return ((int)type & 12) == 12;
            }

            static void ProcessScope(List<Scope> scopes, ExceptionHandler eh, Instruction s, Instruction e, ScopeType mask)
            {
                int start = scopes.FindIndex(_ => _.Instructions[0] == s);
                int end = scopes.FindIndex(start, _ => _.Instructions[0] == e);
                if (end - start > 1)
                {
                    for (int i = start; i < end; i++)
                    {
                        if (i == start)
                            scopes[i].Type += new _Scope(eh, mask | (ScopeType)2);
                        else if (i == end - 1)
                            scopes[i].Type += new _Scope(eh, mask | (ScopeType)3);
                        else
                            scopes[i].Type += new _Scope(eh, mask | (ScopeType)1);
                    }
                }
                else
                    scopes[start].Type += new _Scope(eh, mask | (ScopeType)0);
            }

            public static IEnumerable<Scope> DetectScopes(MethodBody body)
            {
                List<Scope> scopes = new List<Scope>();
                List<ExceptionHandler> ehs = body.ExceptionHandlers
                    .OrderByDescending(_ => _.TryEnd.Index - _.TryStart.Index).ToList();

                List<Instruction> split = new List<Instruction>();
                foreach (var i in ehs)
                {
                    split.Add(i.TryStart);
                    split.Add(i.TryEnd);
                    split.Add(i.HandlerStart);
                    if (i.HandlerEnd != null)
                        split.Add(i.HandlerEnd);
                    if (i.FilterStart != null)
                        split.Add(i.FilterStart);
                }
                Instruction[] s = split.Distinct().OrderBy(_ => _.Index).ToArray();
                int sI = 0;

                List<Instruction> insts = new List<Instruction>();
                for (int i = 0; i < body.Instructions.Count; i++)
                {
                    if (sI < s.Length &&
                        body.Instructions[i] == s[sI])
                    {
                        sI++;
                        if (insts.Count > 0)
                        {
                            scopes.Add(new Scope() { Instructions = insts.ToArray() });
                            insts.Clear();
                        }
                    }
                    insts.Add(body.Instructions[i]);
                }
                if (insts.Count > 0)
                    scopes.Add(new Scope() { Instructions = insts.ToArray() });

                foreach (var eh in ehs)
                {
                    ProcessScope(scopes, eh, eh.TryStart, eh.TryEnd, ScopeType.TryOnly);
                    ProcessScope(scopes, eh, eh.HandlerStart, eh.HandlerEnd, ScopeType.HandlerOnly);
                    if (eh.FilterStart != null)
                        ProcessScope(scopes, eh, eh.FilterStart, eh.HandlerStart, ScopeType.FilterOnly);
                }
                foreach (var i in scopes)
                {
                    if (i.Type.Scopes != null)
                        i.Type.Scopes.Sort((a, b) => -Comparer<bool>.Default.Compare(IsBody(a.Item2), IsBody(b.Item2)));
                    else
                        i.Type.Scopes = new List<Tuple<ExceptionHandler, ScopeType>>();
                }

                return scopes;
            }

            static void PrintStruct(MethodBody body)
            {
                StringBuilder sb = new StringBuilder();
                var scopes = DetectScopes(body);

                int index = 0;
                foreach (var i in scopes)
                {
                    string indent = "";
                    if (i.Type.Scopes != null)
                        foreach (var j in i.Type.Scopes)
                        {
                            if (IsStart(j.Item2) || IsOnly(j.Item2))
                            {
                                if (IsTry(j.Item2))
                                    sb.AppendLine(indent + "try { ");
                                if (IsHandler(j.Item2))
                                    sb.AppendLine(indent + "handler { ");
                                if (IsFilter(j.Item2))
                                    sb.AppendLine(indent + "filter { ");
                                indent += "    ";
                            }
                            else if (IsBody(j.Item2) || IsEnd(j.Item2))
                                indent += "    ";
                        }

                    sb.AppendLine(indent + "#" + index++ + ":");
                    foreach (var j in i.Instructions)
                        sb.AppendLine(indent + j.ToString());

                    if (i.Type.Scopes != null)
                        foreach (var j in i.Type.Scopes)
                        {
                            if (IsEnd(j.Item2) || IsOnly(j.Item2))
                            {
                                if (indent.Length > 0)
                                    indent = indent.Substring(4);
                                sb.AppendLine(indent + "}");
                            }
                        }
                }

                System.Diagnostics.Debug.Write(sb);
            }
        }

        enum StatementType
        {
            Normal,
            Branch
        }
        class Statement
        {
            public int BeginStack;
            public StatementType Type;
            public Instruction[] Instructions;
            public int Key;
        }

        public string Name
        {
            get { return "Control Flow Confusion"; }
        }
        public string Description
        {
            get { return "This confusion obfuscate the code in the methods so that decompilers cannot decompile the methods."; }
        }
        public string ID
        {
            get { return "ctrl flow"; }
        }
        public bool StandardCompatible
        {
            get { return true; }
        }
        public Target Target
        {
            get { return Target.Methods; }
        }
        public Preset Preset
        {
            get { return Preset.Aggressive; }
        }
        public Phase[] Phases
        {
            get { return new Phase[] { this }; }
        }
        public bool SupportLateAddition
        {
            get { return true; }
        }
        public Behaviour Behaviour
        {
            get { return Behaviour.AlterCode; }
        }

        public override Priority Priority
        {
            get { return Priority.MethodLevel; }
        }
        public override IConfusion Confusion
        {
            get { return this; }
        }
        public override int PhaseID
        {
            get { return 3; }
        }
        public override bool WholeRun
        {
            get { return false; }
        }
        public override void Initialize(ModuleDefinition mod)
        {
            if (mod.Architecture != TargetArchitecture.I386)
                Log("Junk code is not supported on target architecture, it won't be generated.");

            //stringGen = new StringGenerator(Random.Next(500000), mod);
            generator = new ExpressionGenerator(Random.Next(), mod);
        }
        public override void DeInitialize()
        {
            //
        }

        Dictionary<ModuleDefinition, Tuple<byte[], FieldDefinition>> txts = new Dictionary<ModuleDefinition, Tuple<byte[], FieldDefinition>>();
        public void Init() { txts.Clear(); }
        public void Deinit() { txts.Clear(); }

        bool genJunk;
        int level;
        bool fakeBranch;
        MethodDefinition method;
        Expression exp;
        Expression invExp;

        ExpressionGenerator generator;
        public override void Process(ConfusionParameter parameter)
        {
            method = parameter.Target as MethodDefinition;
            if (!method.HasBody || method.Body.Instructions.Count == 0) return;

            level = 10;
            if (Array.IndexOf(parameter.Parameters.AllKeys, "level") != -1)
            {
                if (!int.TryParse(parameter.Parameters["level"], out level) && (level <= 0 || level > 10))
                {
                    Log("Invalid level, 5 will be used.");
                    level = 5;
                }
            }

            genJunk = false;
            if (method.Module.Architecture != TargetArchitecture.I386)
                genJunk = false;
            else if (Array.IndexOf(parameter.Parameters.AllKeys, "genjunk") != -1)
            {
                if (!bool.TryParse(parameter.Parameters["genjunk"], out genJunk))
                {
                    Log("Invalid junk code parameter, junk code would not be generated.");
                    genJunk = false;
                }
            }

            fakeBranch = false;
            if (Array.IndexOf(parameter.Parameters.AllKeys, "fakebranch") != -1)
            {
                if (!bool.TryParse(parameter.Parameters["fakebranch"], out fakeBranch))
                {
                    Log("Invalid fake branch parameter, fake branch would not be generated.");
                    fakeBranch = false;
                }
            }

            MethodBody body = method.Body;
            body.SimplifyMacros();
            body.ComputeHeader();
            body.MaxStackSize += 0x10;

            PropagateSeqPoints(body);

            VariableDefinition stateVar = new VariableDefinition(method.Module.TypeSystem.Int32);
            body.Variables.Add(stateVar);
            body.InitLocals = true;

            //Compute stacks
            var stacks = GetStacks(body);

            Dictionary<Instruction, Instruction> ReplTbl = new Dictionary<Instruction, Instruction>();
            List<Scope> scopes = new List<Scope>();
            foreach (var scope in ScopeDetector.DetectScopes(body))
            {
                scopes.Add(scope);

                //Split statements when stack = empty
                //First statement maybe have non-empty stack because of handlers/filter
                List<Statement> sts = new List<Statement>();
                foreach (var i in SplitStatements(body, scope.Instructions, stacks))
                    sts.Add(new Statement()
                    {
                        Instructions = i,
                        Type = StatementType.Normal,
                        Key = 0,
                        BeginStack = stacks[i[0].Index]
                    });

                //Constructor fix
                if (body.Method.IsConstructor && body.Method.HasThis)
                {
                    Statement init = new Statement();
                    init.Type = StatementType.Normal;
                    List<Instruction> z = new List<Instruction>();
                    while (sts.Count != 0)
                    {
                        z.AddRange(sts[0].Instructions);
                        Instruction lastInst = sts[0].Instructions[sts[0].Instructions.Length - 1];
                        sts.RemoveAt(0);
                        if (lastInst.OpCode == OpCodes.Call &&
                            (lastInst.Operand as MethodReference).Name == ".ctor")
                            break;
                    }
                    init.Instructions = z.ToArray();
                    sts.Insert(0, init);
                }

                if (sts.Count == 1 || sts.All(st => st.BeginStack != 0)) continue;

                //Merge statements for level
                for (int i = 0; i < sts.Count - 1; i++)
                {
                    if (Random.Next(1, 10) > level || sts[i + 1].BeginStack != 0)
                    {
                        Statement newSt = new Statement();
                        newSt.Type = sts[i + 1].Type;
                        newSt.BeginStack = sts[i].BeginStack;
                        newSt.Instructions = new Instruction[sts[i].Instructions.Length + sts[i + 1].Instructions.Length];
                        Array.Copy(sts[i].Instructions, 0, newSt.Instructions, 0, sts[i].Instructions.Length);
                        Array.Copy(sts[i + 1].Instructions, 0, newSt.Instructions, sts[i].Instructions.Length, sts[i + 1].Instructions.Length);
                        sts[i] = newSt;
                        sts.RemoveAt(i + 1);
                        i--;
                    }
                }

                //Detect branches
                int k = 0;
                foreach (var st in sts)
                {
                    Instruction last = st.Instructions[st.Instructions.Length - 1];
                    if (last.Operand is Instruction &&
                        sts.Exists(_ => _.Instructions[0] == last.Operand))
                        st.Type = StatementType.Branch;
                    st.Key = k; k++;
                }

                //Shuffle the statements
                List<Instruction> insts = new List<Instruction>();
                for (int i = 1; i < sts.Count; i++)
                {
                    int j = Random.Next(1, sts.Count);
                    var tmp = sts[j];
                    sts[j] = sts[i];
                    sts[i] = tmp;
                }
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < sts.Count; i++)
                {
                    if (i != 0) sb.Append(", ");
                    sb.Append(sts[i].Key);
                }
                Database.AddEntry("CtrlFlow", method.FullName, sb);

                Instruction[] stHdrs = new Instruction[sts.Count];
                k = 0;
                for (int i = 0; i < sts.Count; i++)
                {
                    sts[i].Key = k;
                    if (sts[i].BeginStack == 0)
                        k++;

                    if (sts[i].Instructions.Length > 0)
                        stHdrs[i] = sts[i].Instructions[0];
                }
                Func<object, Statement> resolveHdr = inst =>
                {
                    int _ = Array.IndexOf(stHdrs, inst as Instruction);
                    return _ == -1 ? null : sts[_];
                };


                exp = generator.Generate(level);
                invExp = ExpressionInverser.InverseExpression(exp);
                Instruction[] ldloc = new Instruction[] { Instruction.Create(OpCodes.Ldloc, stateVar) };// new CecilVisitor(invExp, new Instruction[] { Instruction.Create(OpCodes.Ldloc, stateVar) }).GetInstructions();
                Instruction begin = ldloc[0];
                Instruction swit = Instruction.Create(OpCodes.Switch, Empty<Instruction>.Array);
                Instruction end = Instruction.Create(OpCodes.Nop);
                List<Instruction> targets = new List<Instruction>();
                Statement beginSt = resolveHdr(scope.Instructions[0]);

                //Convert branches -> switch
                bool firstSt = true;
                foreach (var st in sts)
                {
                    List<Instruction> stInsts = new List<Instruction>(st.Instructions);
                    Instruction last = st.Instructions[st.Instructions.Length - 1];
                    if (st.Type == StatementType.Branch)
                    {
                        if (last.OpCode.Code == Code.Br)  //uncond
                        {
                            int index = stInsts.Count - 1;
                            stInsts.RemoveAt(index);
                            Statement targetSt = resolveHdr(last.Operand);
                            Statement fallSt;
                            if (fakeBranch && (fallSt = resolveHdr(last.Next)) != null)
                            {
                                ReplTbl[last] = GenFakeBranch(st, targetSt, fallSt, stInsts, stateVar, begin);
                            }
                            else
                            {
                                ReplTbl[last] = EncryptNum(st.Key, stateVar, targetSt.Key, stInsts);
                                stInsts.Add(Instruction.Create(OpCodes.Br, begin));
                                stInsts.AddRange(GetJunk(stateVar));
                            }
                            stInsts[index].SequencePoint = last.SequencePoint;
                        }
                        else if (last.OpCode.Code != Code.Leave)  //cond
                        {
                            int index = stInsts.Count - 1;
                            stInsts.RemoveAt(index);
                            Statement targetSt = resolveHdr(last.Operand);
                            Statement fallSt = resolveHdr(last.Next);

                            if (fallSt == null) //fall into exception block
                            {
                                ReplTbl[last] = EncryptNum(st.Key, stateVar, targetSt.Key, stInsts);
                                stInsts.Add(Instruction.Create(last.OpCode, begin));
                                stInsts.Add(Instruction.Create(OpCodes.Br, last.Next));
                                stInsts.AddRange(GetJunk(stateVar));
                            }
                            else
                            {
                                ReplTbl[last] = EncryptNum(st.Key, stateVar, targetSt.Key, stInsts);
                                stInsts.Add(Instruction.Create(last.OpCode, begin));
                                EncryptNum(st.Key, stateVar, fallSt.Key, stInsts);
                                stInsts.Add(Instruction.Create(OpCodes.Br, begin));
                                stInsts.AddRange(GetJunk(stateVar));
                            }
                            stInsts[index].SequencePoint = last.SequencePoint;
                        }
                    }
                    else
                    {
                        Statement fallSt = resolveHdr(last.Next);
                        if (fallSt != null)
                        {
                            if (fakeBranch)
                            {
                                Statement fakeSt = sts[Random.Next(0, sts.Count)];
                                GenFakeBranch(st, fallSt, fakeSt, stInsts, stateVar, begin);
                            }
                            else
                            {
                                EncryptNum(st.Key, stateVar, fallSt.Key, stInsts);
                                stInsts.Add(Instruction.Create(OpCodes.Br, begin));
                                stInsts.AddRange(GetJunk(stateVar));
                            }
                        }
                        else
                            stInsts.Add(Instruction.Create(OpCodes.Br, end));
                    }
                    if (!firstSt)
                    {
                        targets.Add(stInsts[0]);
                        insts.AddRange(stInsts.ToArray());
                    }
                    else
                    {
                        insts.AddRange(stInsts.ToArray());
                        insts.AddRange(ldloc);
                        insts.Add(swit);
                        if (st.BeginStack == 0)
                            targets.Add(stInsts[0]);
                        firstSt = false;
                    }
                }
                swit.Operand = targets.ToArray();
                insts.Add(end);


                //fix peverify
                foreach (var i in scope.Type.Scopes)
                {
                    if (ScopeDetector.IsFilter(i.Item2) &&
                        (ScopeDetector.IsOnly(i.Item2) || ScopeDetector.IsEnd(i.Item2)))
                    {
                        insts.Add(Instruction.Create(OpCodes.Ldc_I4, Random.Next()));
                        insts.Add(Instruction.Create(OpCodes.Endfilter));
                        break;
                    }
                    else if (ScopeDetector.IsTry(i.Item2) &&
                             (ScopeDetector.IsOnly(i.Item2) || ScopeDetector.IsEnd(i.Item2)))
                    {
                        if (i.Item1.HandlerEnd == null)
                        {
                            insts.Add(Instruction.Create(OpCodes.Ldnull));
                            insts.Add(Instruction.Create(OpCodes.Throw));
                        }
                        else
                        {
                            Instruction last = scope.Instructions[scope.Instructions.Length - 1];
                            insts.Add(Instruction.Create(OpCodes.Leave, last.OpCode != OpCodes.Leave ? i.Item1.HandlerEnd : last.Operand as Instruction));
                        }
                        break;
                    }
                    else if (ScopeDetector.IsHandler(i.Item2) &&
                             (ScopeDetector.IsOnly(i.Item2) || ScopeDetector.IsEnd(i.Item2)))
                    {
                        //it must either finally/fault or catch, not both
                        if (i.Item1.HandlerType == ExceptionHandlerType.Finally ||
                            i.Item1.HandlerType == ExceptionHandlerType.Fault)
                            insts.Add(Instruction.Create(OpCodes.Endfinally));
                        else if (i.Item1.HandlerEnd == null)
                        {
                            insts.Add(Instruction.Create(OpCodes.Ldnull));
                            insts.Add(Instruction.Create(OpCodes.Throw));
                        }
                        else
                        {
                            Instruction last = i.Item1.TryEnd.Previous;
                            insts.Add(Instruction.Create(OpCodes.Leave, last.OpCode != OpCodes.Leave ? i.Item1.HandlerEnd : last.Operand as Instruction));
                        }
                        break;
                    }
                }

                ReplTbl[scope.Instructions[0]] = insts[0];
                scope.Instructions = insts.ToArray();
            }

            //emit
            body.Instructions.Clear();
            foreach (var scope in scopes)
                foreach (var i in scope.Instructions)
                {
                    if (i.Operand is Instruction &&
                        ReplTbl.ContainsKey(i.Operand as Instruction))
                    {
                        i.Operand = ReplTbl[i.Operand as Instruction];
                    }
                    else if (i.Operand is Instruction[])
                    {
                        Instruction[] insts = i.Operand as Instruction[];
                        for (int j = 0; j < insts.Length; j++)
                            if (ReplTbl.ContainsKey(insts[j]))
                                insts[j] = ReplTbl[insts[j]];
                    }
                }
            foreach (var scope in scopes)
            {
                SetLvHandler(scope, body, scope.Instructions);
                foreach (var i in scope.Instructions)
                    body.Instructions.Add(i);
            }

            //fix peverify
            if (!method.ReturnType.IsTypeOf("System", "Void"))
            {
                body.Instructions.Add(Instruction.Create(OpCodes.Ldnull));
                body.Instructions.Add(Instruction.Create(OpCodes.Unbox_Any, method.ReturnType));
            }
            body.Instructions.Add(Instruction.Create(OpCodes.Ret));


            foreach (ExceptionHandler eh in body.ExceptionHandlers)
            {
                eh.TryEnd = eh.TryEnd.Next;
                eh.HandlerEnd = eh.HandlerEnd.Next;
            }

            body.ComputeOffsets();
            body.PreserveMaxStackSize = true;

            ReduceSeqPoints(body);

            //if (method.Name.StartsWith("DYN___"))
            {
              //  method.Name = ObfuscationHelper.GetRandomName();
            }
        }
        static void SetLvHandler(Scope scope, MethodBody body, IList<Instruction> block)
        {
            foreach (var i in scope.Type.Scopes)
            {
                if (i.Item1 == null) return;
                switch (i.Item2)
                {
                    case ScopeType.TryOnly:
                        i.Item1.TryStart = block[0];
                        i.Item1.TryEnd = block[block.Count - 1];
                        break;
                    case ScopeType.TryStart:
                        i.Item1.TryStart = block[0];
                        break;
                    case ScopeType.TryEnd:
                        i.Item1.TryEnd = block[block.Count - 1];
                        break;

                    case ScopeType.HandlerOnly:
                        i.Item1.HandlerStart = block[0];
                        i.Item1.HandlerEnd = block[block.Count - 1];
                        break;
                    case ScopeType.HandlerStart:
                        i.Item1.HandlerStart = block[0];
                        break;
                    case ScopeType.HandlerEnd:
                        i.Item1.HandlerEnd = block[block.Count - 1];
                        break;

                    case ScopeType.FilterOnly:
                    case ScopeType.FilterStart:
                        i.Item1.FilterStart = block[0];
                        break;
                }
            }
        }

        void PropagateSeqPoints(MethodBody body)
        {
            SequencePoint active = null;
            foreach (var i in body.Instructions)
            {
                if (i.SequencePoint != null &&
                    i.OpCode.FlowControl != FlowControl.Branch &&
                    i.OpCode.FlowControl != FlowControl.Cond_Branch)
                    active = i.SequencePoint;
                else if (i.OpCode.FlowControl != FlowControl.Branch &&
                    i.OpCode.FlowControl != FlowControl.Cond_Branch)
                    i.SequencePoint = active;
            }
        }

        void ReduceSeqPoints(MethodBody body)
        {
            SequencePoint active = null;
            foreach (var i in body.Instructions)
            {
                if (i.SequencePoint == active)
                    i.SequencePoint = null;
                else
                    active = i.SequencePoint;
            }
        }

        int ComputeRandNum(IList<Instruction> insts)
        {
            ExpressionGenerator gen = new ExpressionGenerator(Random.Next(), method.Module);
            Expression exp = gen.Generate(2);
            int r = Random.Next(-10, 10);
            int i = ExpressionEvaluator.Evaluate(exp, r);
            foreach (var inst in new CecilVisitor(exp, new Instruction[] { Instruction.Create(OpCodes.Ldc_I4, r) }).GetInstructions())
                insts.Add(inst);
            return i;
        }
        Instruction EncryptNum(int original, VariableDefinition varDef, int num, IList<Instruction> insts)
        {
            //original = original < 0 ? 0 : original;



            //MathGenerator mg = new MathGenerator(Random.Next(500000), method.Module);
            
            //DynamicGenerator dg = new DynamicGenerator(Random.Next(500000), method.Module);



            //List<Instruction> mathInsts = mg.GenerateLevels(num, OpCodes.Ldc_I4, Random.Next(1, 6));
            //Instruction ret = mathInsts[0];

            //foreach (var x in mathInsts)
            //    insts.Add(x);

            List<Instruction> mathInsts = new List<Instruction>();

            //mathInsts = dg.Generate(num, OpCodes.Ldc_I4);
            //if (Random.Next(0, 100) % 2 == 0)
            {
                if (ObfuscationHelper.StringGen == null)
                {
                    //mathInsts.Add(Instruction.Create(OpCodes.Ldc_I4, num));
                    mathInsts = new MathGenerator(Random.Next(500000), method.Module).GenerateLevels(num, OpCodes.Ldc_I4, Random.Next(0, 5));
                }
                else
                {
                    mathInsts = ObfuscationHelper.StringGen.GenerateLevels(method, num, Random.Next(1, 3), 3);
                }
            }
            //else
            {
              //  mathInsts = mg.GenerateLevels(num, OpCodes.Ldc_I4, Random.Next(1, 6));
            }

            // TODO: Make use of one generated dynamic method more than once
            // Wasting space, and using more cpu to do things is not good.

            if (ObfuscationHelper.StringGen != null && !method.Name.StartsWith("DYN___"))
            {
                for (int i = 0; i < mathInsts.Count; i++)
                {
                    Instruction mInst = mathInsts[i];
                    if (mInst.OpCode == OpCodes.Ldc_I4 && Random.Next(0, 100) % 2 == 0)
                    {

                        DynamicGenerator.DynamicInfo dCall;
                        

                        var dInsts = ObfuscationHelper.StringGen.DynGen.Generate(method, (int)mInst.Operand, mInst.OpCode, out dCall, true);

                        /*if (dCall.Wrapper.Methods.FirstOrDefault(x => x.Name == "Initialize") == method)
                        {
                            continue;
                        }*/

                        mathInsts[i] = dInsts[0];

                        dInsts = dInsts.Skip(1).Reverse().ToList();
                        foreach (Instruction a in dInsts)
                        {
                            mathInsts.Insert(i + 1, a);
                        }
                        i += dInsts.Count;
                    }
                }
            }
            
            Instruction ret = mathInsts[0];

            foreach (var x in mathInsts)
                insts.Add(x);


            insts.Add(Instruction.Create(OpCodes.Stloc, varDef));

            /*ExpressionGenerator gen = new ExpressionGenerator(Random.Next(5000), method.Module);
            Expression exp = gen.Generate(3);
            int i = ExpressionEvaluator.Evaluate(exp, (num ^ original));
            var expR = ExpressionInverser.InverseExpression(exp);
            var ci = new CecilVisitor(expR, new Instruction[] { Instruction.Create(OpCodes.Ldc_I4, i) });*/
            //bool first = true;

            //foreach (var inst in new CecilVisitor(exp, new Instruction[] { Instruction.Create(OpCodes.Ldc_I4, i) }).GetInstructions())
            //{
            //    if (first)
            //    {
            //        ret = inst;
            //        first = false;
            //    }
            //    insts.Add(inst);
            //}
            //insts.Add(Instruction.Create(OpCodes.Ldloc, varDef));
            //insts.Add(Instruction.Create(OpCodes.Xor));
            //insts.Add(ret = Instruction.Create(OpCodes.Ldc_I4, ExpressionEvaluator.Evaluate(exp, num)));

            // new local for xork
            /*VariableDefinition xorKeyVar = new VariableDefinition(varDef.VariableType);
            method.Body.Variables.Add(xorKeyVar);
            
            int xork = Random.Next(5000);
            int addk = Random.Next(90000);
            double varKey = Random.NextDouble();

            int addd = num - addk;
            int xord = addd ^ xork;

            double encXorKey = Math.Abs(varKey);
            double encXorKeyRes = xork - encXorKey;
            
            ret = Instruction.Create(OpCodes.Ldc_I4, xord);

            // xork ^ xord + addk = num



            // Math.Abs(varKey) + encXorKey

            insts.Add(Instruction.Create(OpCodes.Ldc_R8, varKey));
            insts.Add(Instruction.Create(OpCodes.Call, method.Module.Import(typeof(Math).GetMethod("Abs", new Type[] { typeof(Double) }))));
            insts.Add(Instruction.Create(OpCodes.Ldc_R8, encXorKeyRes));
            insts.Add(Instruction.Create(OpCodes.Add));
            insts.Add(Instruction.Create(OpCodes.Conv_I4));
            insts.Add(Instruction.Create(OpCodes.Stloc, xorKeyVar));
            insts.Add(Instruction.Create(OpCodes.Ldloc, xorKeyVar));

            insts.Add(ret);
            insts.Add(Instruction.Create(OpCodes.Xor));
            insts.Add(Instruction.Create(OpCodes.Ldc_I4, addk));
            insts.Add(Instruction.Create(OpCodes.Add));
            insts.Add(Instruction.Create(OpCodes.Stloc, varDef));*/
            return ret;
        }

        static void PopStack(MethodBody body, Instruction inst, ref int stack)
        {
            switch (inst.OpCode.StackBehaviourPop)
            {
                case StackBehaviour.Pop0:
                    break;
                case StackBehaviour.Pop1:
                case StackBehaviour.Popi:
                case StackBehaviour.Popref:
                    stack--; break;
                case StackBehaviour.Pop1_pop1:
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
                case StackBehaviour.Varpop:
                    switch (inst.OpCode.Code)
                    {
                        case Code.Newobj:
                            stack++;
                            goto case Code.Call;
                        case Code.Call:
                        case Code.Calli:
                        case Code.Callvirt:
                            IMethodSignature sig = inst.Operand as IMethodSignature;
                            if (!sig.ExplicitThis && sig.HasThis)
                                stack--;
                            stack -= sig.Parameters.Count;
                            break;
                        case Code.Ret:
                            if (body.Method.ReturnType.MetadataType != MetadataType.Void)
                                stack--;
                            if (stack != 0)
                                throw new InvalidOperationException();
                            break;
                    } break;
                case StackBehaviour.PopAll:
                    stack = 0; break;
            }
        }
        static void PushStack(MethodBody body, Instruction inst, ref  int stack)
        {
            switch (inst.OpCode.StackBehaviourPush)
            {
                case StackBehaviour.Push0:
                    break;
                case StackBehaviour.Pushi:
                case StackBehaviour.Push1:
                case StackBehaviour.Pushi8:
                case StackBehaviour.Pushr4:
                case StackBehaviour.Pushr8:
                case StackBehaviour.Pushref:
                    stack++; break;
                case StackBehaviour.Push1_push1:
                    stack += 2; break;
                case StackBehaviour.Varpush:
                    IMethodSignature sig = inst.Operand as IMethodSignature;
                    if (sig.ReturnType.MetadataType != MetadataType.Void)
                        stack++;
                    break;
            }
        }
        static int[] GetStacks(MethodBody body)
        {
            int[] stacks = new int[body.Instructions.Count];

            List<Instruction> catchStarts = body.ExceptionHandlers.
                Where(_ => _.HandlerType == ExceptionHandlerType.Catch)
                .Select(_ => _.HandlerStart).ToList();
            List<Instruction> finallyStarts = body.ExceptionHandlers.
                Where(_ => _.HandlerType == ExceptionHandlerType.Finally)
                .Select(_ => _.HandlerStart).ToList();
            List<Instruction> filterStarts = body.ExceptionHandlers.
                Where(_ => _.FilterStart != null)
                .Select(_ => _.HandlerStart).ToList();

            Queue<int> ps = new Queue<int>();
            for (int i = 0; i < stacks.Length; i++)
            {
                if (catchStarts.Contains(body.Instructions[i]) ||
                    filterStarts.Contains(body.Instructions[i]))
                {
                    ps.Enqueue(i);
                    stacks[i] = 1;
                }
                else if (i == 0 ||
                    finallyStarts.Contains(body.Instructions[i]))
                {
                    ps.Enqueue(i);
                    stacks[i] = 0;
                }
                else
                    stacks[i] = int.MinValue;
            }

            do
            {
                bool br = false;
                for (int now = ps.Dequeue(); now < body.Instructions.Count && !br; now++)
                {
                    Instruction inst = body.Instructions[now];
                    int stack = stacks[now];
                    PopStack(body, inst, ref stack);
                    PushStack(body, inst, ref stack);
                    switch (inst.OpCode.FlowControl)
                    {
                        case FlowControl.Branch:
                            {
                                int targetIdx = (inst.Operand as Instruction).Index;
                                if (stacks[targetIdx] != stack)
                                {
                                    ps.Enqueue(targetIdx);
                                    stacks[targetIdx] = stack;
                                }
                                br = true;
                            } break;
                        case FlowControl.Cond_Branch:
                            {
                                int targetIdx;
                                if (inst.OpCode.Code == Code.Switch)
                                {
                                    foreach (var i in inst.Operand as Instruction[])
                                    {
                                        targetIdx = i.Index;
                                        if (stacks[targetIdx] != stack)
                                        {
                                            ps.Enqueue(targetIdx);
                                            stacks[targetIdx] = stack;
                                        }
                                    }
                                }
                                else
                                {
                                    targetIdx = (inst.Operand as Instruction).Index;
                                    if (stacks[targetIdx] != stack)
                                    {
                                        ps.Enqueue(targetIdx);
                                        stacks[targetIdx] = stack;
                                    }
                                }
                                targetIdx = now + 1;
                                if (targetIdx < body.Instructions.Count && stacks[targetIdx] != stack)
                                {
                                    ps.Enqueue(targetIdx);
                                    stacks[targetIdx] = stack;
                                }
                                br = true;
                            } break;
                        case FlowControl.Return:
                        case FlowControl.Throw:
                            br = true;
                            break;
                        default:
                            if (now + 1 < body.Instructions.Count)
                                stacks[now + 1] = stack;
                            break;
                    }
                }
            } while (ps.Count != 0);
            return stacks;
        }
        static IList<Instruction[]> SplitStatements(MethodBody body, Instruction[] scope, int[] stacks)
        {
            //scope is continuous in order
            int baseIndex = body.Instructions.IndexOf(scope[0]);
            List<Instruction[]> ret = new List<Instruction[]>();
            List<Instruction> insts = new List<Instruction>();
            for (int i = 0; i < scope.Length; i++)
            {
                if (stacks[baseIndex + i] == 0 && insts.Count != 0 &&
                    (i == 0 || scope[i - 1].OpCode.OpCodeType != OpCodeType.Prefix))
                {
                    ret.Add(insts.ToArray());
                    insts.Clear();
                }
                insts.Add(scope[i]);
            }
            if (insts.Count != 0)
                ret.Add(insts.ToArray());
            return ret;
        }

        static ushort[] junkCode = new ushort[]  {  0x24ff, 0x77ff, 0x78ff, 0xa6ff, 0xa7ff, 
                                                    0xa8ff, 0xa9ff, 0xaaff, 0xabff, 0xacff,
                                                    0xadff, 0xaeff, 0xafff, 0xb0ff, 0xb1ff,
                                                    0xb2ff, 0xbbff, 0xbcff, 0xbdff, 0xbeff,
                                                    0xbfff, 0xc0ff, 0xc1ff, 0xc4ff, 0xc5ff,
                                                    0xc7ff, 0xc8ff, 0xc9ff, 0xcaff, 0xcbff,
                                                    0xccff, 0xcdff, 0xceff, 0xcfff, 0x08fe,
                                                    0x19fe, 0x1bfe, 0x1ffe};
        IEnumerable<Instruction> GetJunk_(VariableDefinition stateVar)
        {
            TypeDefinition type = method.DeclaringType;
            var fields = type.Fields.Where(f => !f.IsLiteral).ToArray();
            if (type.Methods.Count > 0 && Random.Next() % 2 == 0)
                yield return Instruction.Create(OpCodes.Ldtoken, type.Methods[Random.Next(0, type.Methods.Count)]);
            else if (fields.Length > 0 && Random.Next() % 2 == 0)
                yield return Instruction.Create(OpCodes.Ldtoken, fields[Random.Next(0, fields.Length)]);
            else
                yield return Instruction.Create(OpCodes.Ldtoken, type);
            if (genJunk)
            {
                switch (Random.Next(0, 5))
                {
                    case 0:
                        yield return Instruction.Create(OpCodes.Pop);
                        break;
                    case 1:
                        yield return Instruction.Create(OpCodes.Ldc_I4, Random.Next(0, 9));
                        yield return Instruction.Create(OpCodes.Stloc, stateVar);
                        yield return Instruction.Create(OpCodes.Pop);
                        break;
                    case 2:
                        Instruction inst = Instruction.Create(OpCodes.Pop);
                        yield return Instruction.Create(OpCodes.Ldc_I4, Random.Next(0, 9));
                        yield return Instruction.Create(OpCodes.Ldc_I4, Random.Next(0, 9));
                        switch (Random.Next(0, 4))
                        {
                            case 0:
                                yield return Instruction.Create(OpCodes.Bne_Un, inst);
                                break;
                            case 1:
                                yield return Instruction.Create(OpCodes.Beq, inst);
                                break;
                            case 2:
                                yield return Instruction.Create(OpCodes.Bgt, inst);
                                break;
                            case 3:
                                yield return Instruction.Create(OpCodes.Ble, inst);
                                break;
                        }
                        Instruction e = Instruction.Create(OpCodes.Break);
                        yield return Instruction.Create(OpCodes.Pop);
                        yield return Instruction.Create(OpCodes.Br, e);
                        yield return inst;
                        yield return e;
                        break;
                    case 3:
                        yield return Instruction.Create(OpCodes.Ldc_I4, Random.Next(0, 9));
                        yield return Instruction.Create(OpCodes.Ldc_I4, Random.Next(0, 9));
                        switch (Random.Next(0, 4))
                        {
                            case 0:
                                yield return Instruction.Create(OpCodes.Add);
                                break;
                            case 1:
                                yield return Instruction.Create(OpCodes.Sub);
                                break;
                            case 2:
                                yield return Instruction.Create(OpCodes.Mul);
                                break;
                            case 3:
                                yield return Instruction.Create(OpCodes.Xor);
                                break;
                        }
                        yield return Instruction.Create(OpCodes.Stloc, stateVar);
                        yield return Instruction.Create(OpCodes.Pop);
                        break;
                    case 4:
                        yield return Instruction.CreateJunkCode(junkCode[Random.Next(0, junkCode.Length)]);
                        break;
                }
            }
            else
            {
                switch (Random.Next(0, 4))
                {
                    case 0:
                        yield return Instruction.Create(OpCodes.Pop);
                        break;
                    case 1:
                        yield return Instruction.Create(OpCodes.Ldc_I4, Random.Next(-1, 9));
                        yield return Instruction.Create(OpCodes.Stloc, stateVar);
                        yield return Instruction.Create(OpCodes.Pop);
                        break;
                    case 2:
                        Instruction inst = Instruction.Create(OpCodes.Pop);
                        yield return Instruction.Create(OpCodes.Ldc_I4, Random.Next(0, 9));
                        yield return Instruction.Create(OpCodes.Ldc_I4, Random.Next(0, 9));
                        switch (Random.Next(0, 4))
                        {
                            case 0:
                                yield return Instruction.Create(OpCodes.Bne_Un, inst);
                                break;
                            case 1:
                                yield return Instruction.Create(OpCodes.Beq, inst);
                                break;
                            case 2:
                                yield return Instruction.Create(OpCodes.Bgt, inst);
                                break;
                            case 3:
                                yield return Instruction.Create(OpCodes.Ble, inst);
                                break;
                        }
                        Instruction e = Instruction.Create(OpCodes.Break);
                        yield return Instruction.Create(OpCodes.Pop);
                        yield return Instruction.Create(OpCodes.Br, e);
                        yield return inst;
                        yield return e;
                        break;
                    case 3:
                        yield return Instruction.Create(OpCodes.Ldc_I4, Random.Next());
                        yield return Instruction.Create(OpCodes.Ldc_I4, Random.Next());
                        switch (Random.Next(0, 4))
                        {
                            case 0:
                                yield return Instruction.Create(OpCodes.Add);
                                break;
                            case 1:
                                yield return Instruction.Create(OpCodes.Sub);
                                break;
                            case 2:
                                yield return Instruction.Create(OpCodes.Mul);
                                break;
                            case 3:
                                yield return Instruction.Create(OpCodes.Xor);
                                break;
                        }
                        yield return Instruction.Create(OpCodes.Stloc, stateVar);
                        yield return Instruction.Create(OpCodes.Pop);
                        break;
                }
            }
        }
        IEnumerable<Instruction> GetJunk(VariableDefinition stateVar)
        {
            //return new Instruction[0];
            return GetJunk_(stateVar);
        }

        Instruction GenFakeBranch(Statement self, Statement target, Statement fake, IList<Instruction> insts,
            VariableDefinition stateVar, Instruction begin)
        {
            Instruction ret;
            int num = ComputeRandNum(insts);
            switch (Random.Next(0, 4))
            {
                case 0: //if (r == r) goto target; else goto fake;
                    insts.Add(ret = Instruction.Create(OpCodes.Ldc_I4, num));
                    EncryptNum(self.Key, stateVar, target.Key, insts);
                    insts.Add(Instruction.Create(OpCodes.Beq, begin));
                    EncryptNum(self.Key, stateVar, fake.Key, insts);
                    insts.Add(Instruction.Create(OpCodes.Br, begin));
                    break;
                case 1: //if (r == r + x) goto fake; else goto target;
                    insts.Add(ret = Instruction.Create(OpCodes.Ldc_I4, num + (Random.Next() % 2 == 0 ? -1 : 1)));
                    EncryptNum(self.Key, stateVar, fake.Key, insts);
                    insts.Add(Instruction.Create(OpCodes.Beq, begin));
                    EncryptNum(self.Key, stateVar, target.Key, insts);
                    insts.Add(Instruction.Create(OpCodes.Br, begin));
                    break;
                case 2: //if (r != r) goto fake; else goto target;
                    insts.Add(ret = Instruction.Create(OpCodes.Ldc_I4, num));
                    EncryptNum(self.Key, stateVar, fake.Key, insts);
                    insts.Add(Instruction.Create(OpCodes.Bne_Un, begin));
                    EncryptNum(self.Key, stateVar, target.Key, insts);
                    insts.Add(Instruction.Create(OpCodes.Br, begin));
                    break;
                case 3: //if (r != r + x) goto target; else goto fake;
                    insts.Add(ret = Instruction.Create(OpCodes.Ldc_I4, num + (Random.Next() % 2 == 0 ? -1 : 1)));
                    EncryptNum(self.Key, stateVar, target.Key, insts);
                    insts.Add(Instruction.Create(OpCodes.Bne_Un, begin));
                    EncryptNum(self.Key, stateVar, fake.Key, insts);
                    insts.Add(Instruction.Create(OpCodes.Br, begin));
                    break;
                default:
                    throw new InvalidOperationException();
            }
            foreach (var i in GetJunk(stateVar))
                insts.Add(i);
            return ret;
        }
    }
}