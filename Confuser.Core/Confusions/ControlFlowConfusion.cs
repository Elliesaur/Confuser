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

namespace Confuser.Core.Confusions
{
    public class ControlFlowConfusion : StructurePhase, IConfusion
    {
        private enum LevelType
        {
            None = 1,
            Try = 2,
            TryStart = 3,
            TryEnd = 4,
            Handler = 5,
            HandlerStart = 6,
            HandlerEnd = 7,
            Filter = 8,
            FilterStart = 9,
            FilterEnd = 10
        }
        private struct Level
        {
            public Level(ExceptionHandler eh, LevelType t)
            {
                Handler = new List<ExceptionHandler>() { eh };
                Type = new List<LevelType>() { t };
            }

            public List<ExceptionHandler> Handler;
            public List<LevelType> Type;

            public int GetEndOffset()
            {
                int ret = -1;
                foreach (ExceptionHandler eh in Handler)
                {
                    if (eh.TryEnd.Offset > ret) ret = eh.TryEnd.Offset;
                    if (eh.HandlerEnd.Offset > ret) ret = eh.HandlerEnd.Offset;
                    if (eh.FilterStart != null && eh.HandlerStart.Offset > ret) ret = eh.HandlerStart.Offset;
                }
                return ret;
            }
            public LevelType GetOnlyLevelType()
            {
                if (Type.Count != 1) return 0;
                return Type[0];
            }

            public static bool operator ==(Level a, Level b)
            {
                if (a.Handler.Count != b.Handler.Count ||
                    a.Type.Count != b.Type.Count)
                    return false;

                for (int i = 0; i < a.Handler.Count; i++)
                    if (a.Handler[i] != b.Handler[i])
                        return false;
                for (int i = 0; i < a.Type.Count; i++)
                    if (a.Type[i] != b.Type[i])
                        return false;
                return true;
            }

            public static bool operator !=(Level a, Level b)
            {
                if (a.Handler.Count != b.Handler.Count ||
                    b.Type.Count != b.Type.Count)
                    return true;

                for (int i = 0; i < a.Handler.Count; i++)
                    if (a.Handler[i] == b.Handler[i])
                        return false;
                for (int i = 0; i < a.Type.Count; i++)
                    if (a.Type[i] == b.Type[i])
                        return false;
                return true;
            }

            public static Level operator +(Level a, Level b)
            {
                Level ret = new Level();
                ret.Handler = new List<ExceptionHandler>();
                ret.Handler.AddRange(a.Handler);
                ret.Handler.AddRange(b.Handler);
                ret.Type = new List<LevelType>();
                ret.Type.AddRange(a.Type);
                ret.Type.AddRange(b.Type);
                return ret;
            }

            public override int GetHashCode()
            {
                int hash = base.GetHashCode();
                foreach (ExceptionHandler eh in Handler)
                    hash ^= eh.GetHashCode();
                foreach (LevelType t in Type)
                    hash ^= t.GetHashCode();
                return hash;
            }

            public override bool Equals(object obj)
            {
                return (obj is Level) && ((Level)obj) == this;
            }

            public override string ToString()
            {
                StringBuilder ret = new StringBuilder();
                for (int i = 0; i < Handler.Count; i++)
                {
                    if (i != 0) ret.Append(",");
                    ret.Append((Handler[i] == null ? "00000000" : Handler[i].GetHashCode().ToString("X8")) + "_" + Type[i].ToString());
                } return ret.ToString();
            }
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
            get { return false; }
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
            rand = new Random();
            if (mod.Architecture != TargetArchitecture.I386)
                Log("Junk code is not supported on target architecture, it won't generated.");
        }
        public override void DeInitialize()
        {
            //
        }

        public void Init() { }
        public void Deinit() { }

        Random rand;
        bool genJunk;
        MethodDefinition method;
        public override void Process(ConfusionParameter parameter)
        {
            method = parameter.Target as MethodDefinition;
            if (!method.HasBody) return;

            int slv = 5;
            if (Array.IndexOf(parameter.Parameters.AllKeys, "level") != -1)
            {
                if (!int.TryParse(parameter.Parameters["level"], out slv) && (slv <= 0 || slv > 10))
                {
                    Log("Invaild level, 5 will be used.");
                    slv = 5;
                }
            }
            genJunk = false;
            if (method.Module.Architecture != TargetArchitecture.I386)
                genJunk = false;
            else if (Array.IndexOf(parameter.Parameters.AllKeys, "genjunk") != -1)
            {
                if (!bool.TryParse(parameter.Parameters["genjunk"], out genJunk))
                {
                    Log("Invaild junk code parameter, junk code would not generated.");
                    genJunk = false;
                }
            }
            double trueLv = slv / 10.0;

            MethodBody body = method.Body;
            body.ComputeHeader();
            body.MaxStackSize += 5;
            Dictionary<Instruction, Level> Ids = GetIds(body);
            Level[] lvs = GetLvs(Ids);
            List<Instruction[]> scopes = new List<Instruction[]>();
            foreach (Level lv in lvs)
                scopes.Add(GetInstructionsByLv(lv, Ids));

            body.Instructions.Clear();
            Dictionary<Instruction, Instruction> HdrTbl = new Dictionary<Instruction, Instruction>();
            for (int i = 0; i < scopes.Count; i++)
            {
                Instruction[] scope = scopes[i];
                Instruction[][] blocks;
                blocks = BranchesSplit(scope, trueLv);
                AddBranches(body, ref blocks);
                Reorder(ref blocks);

                HdrTbl.Add(scope[0], blocks[0][0]);

                foreach (Instruction[] iblk in blocks)
                {
                    body.Instructions.Add(iblk[0]);
                    for (int ii = 1; ii < iblk.Length; ii++)
                    {
                        Instruction tmp;
                        if (iblk[ii].Operand is Instruction)
                        {
                            if (HdrTbl.TryGetValue(iblk[ii].Operand as Instruction, out tmp) && tmp != blocks[0][0])
                                iblk[ii].Operand = tmp;
                        }
                        else if (iblk[ii].Operand is Instruction[])
                        {
                            Instruction[] op = iblk[ii].Operand as Instruction[];
                            for (int iii = 0; iii < op.Length; iii++)
                                if (HdrTbl.TryGetValue(op[iii], out tmp) && tmp != blocks[0][0])
                                    op[iii] = tmp;
                            iblk[ii].Operand = op;
                        }
                        body.Instructions.Add(iblk[ii]);
                    }
                }
                SetLvHandler(lvs[i], body, blocks);
            }

            foreach (ExceptionHandler eh in body.ExceptionHandlers)
            {
                eh.TryEnd = eh.TryEnd.Next;
                eh.HandlerEnd = eh.HandlerEnd.Next;
                //if ((eh.HandlerType & ExceptionHandlerType.Filter) == ExceptionHandlerType.Filter)
                //{
                //    eh.FilterEnd = eh.FilterEnd.Next;
                //}
            }

            body.ComputeOffsets();
            body.PreserveMaxStackSize = true;
        }

        private Dictionary<Instruction, Level> GetIds(MethodBody body)
        {
            SortedDictionary<int, Level> lvs = new SortedDictionary<int, Level>();
            int p = -1;
            foreach (ExceptionHandler eh in body.ExceptionHandlers)
            {
                if (!lvs.ContainsKey(eh.TryStart.Offset))
                    lvs[eh.TryStart.Offset] = new Level(eh, LevelType.TryStart);
                else
                    lvs[eh.TryStart.Offset] += new Level(eh, LevelType.TryStart);

                if (!lvs.ContainsKey(eh.TryEnd.Previous.Offset))
                    lvs[eh.TryEnd.Previous.Offset] = new Level(eh, LevelType.TryEnd);
                else
                    lvs[eh.TryEnd.Previous.Offset] += new Level(eh, LevelType.TryEnd);

                if (!lvs.ContainsKey(eh.HandlerStart.Offset))
                    lvs[eh.HandlerStart.Offset] = new Level(eh, LevelType.HandlerStart);
                else
                    lvs[eh.HandlerStart.Offset] += new Level(eh, LevelType.HandlerStart);

                if (!lvs.ContainsKey(eh.HandlerEnd.Previous.Offset))
                    lvs[eh.HandlerEnd.Previous.Offset] = new Level(eh, LevelType.HandlerEnd);
                else
                    lvs[eh.HandlerEnd.Previous.Offset] += new Level(eh, LevelType.HandlerEnd);

                p = eh.HandlerEnd.Previous.Offset;
                if ((eh.HandlerType & ExceptionHandlerType.Filter) == ExceptionHandlerType.Filter)
                {
                    if (!lvs.ContainsKey(eh.FilterStart.Offset))
                        lvs[eh.FilterStart.Offset] = new Level(eh, LevelType.FilterStart);
                    else
                        lvs[eh.FilterStart.Offset] += new Level(eh, LevelType.FilterStart);

                    if (!lvs.ContainsKey(eh.HandlerStart.Previous.Offset))
                        lvs[eh.HandlerStart.Previous.Offset] = new Level(eh, LevelType.FilterEnd);
                    else
                        lvs[eh.HandlerStart.Previous.Offset] += new Level(eh, LevelType.FilterEnd);

                    p = eh.HandlerStart.Previous.Offset;
                }
            }
            if (!lvs.ContainsKey(0))
                lvs[0] = new Level(null, LevelType.None);

            List<int> ks = lvs.Keys.ToList();
            for (int i = 0; i < ks.Count; i++)
            {
                if (lvs[ks[i]].Handler.Count >= 2 &&
                    lvs[ks[i]].Handler[0] == lvs[ks[i]].Handler[1])
                {
                    if (lvs[ks[i]].Type.Contains(LevelType.TryStart) && lvs[ks[i]].Type.Contains(LevelType.TryEnd))
                    {
                        lvs[ks[i]].Handler.RemoveAt(0);
                        lvs[ks[i]].Type.Remove(LevelType.TryStart);
                        lvs[ks[i]].Type.Remove(LevelType.TryEnd);
                        lvs[ks[i]].Type.Add(LevelType.Try);
                    }
                    if (lvs[ks[i]].Type.Contains(LevelType.HandlerStart) && lvs[ks[i]].Type.Contains(LevelType.HandlerEnd))
                    {
                        lvs[ks[i]].Handler.RemoveAt(0);
                        lvs[ks[i]].Type.Remove(LevelType.HandlerStart);
                        lvs[ks[i]].Type.Remove(LevelType.HandlerEnd);
                        lvs[ks[i]].Type.Add(LevelType.Handler);
                    }
                    if (lvs[ks[i]].Type.Contains(LevelType.FilterStart) && lvs[ks[i]].Type.Contains(LevelType.FilterEnd))
                    {
                        lvs[ks[i]].Handler.RemoveAt(0);
                        lvs[ks[i]].Type.Remove(LevelType.FilterStart);
                        lvs[ks[i]].Type.Remove(LevelType.FilterEnd);
                        lvs[ks[i]].Type.Add(LevelType.Filter);
                    }
                }
                if (i != 0 &&
                    lvs[ks[i - 1]].GetOnlyLevelType().ToString().EndsWith("Start") &&
                    lvs[ks[i]].GetOnlyLevelType().ToString().EndsWith("End"))
                {
                    int o = ks[i - 1];
                    Level lv = lvs[o];
                    switch (lv.GetOnlyLevelType())
                    {
                        case LevelType.TryStart:
                            lv.Type.Clear();
                            lv.Type.Add(LevelType.Try); break;
                        case LevelType.HandlerStart:
                            lv.Type.Clear();
                            lv.Type.Add(LevelType.Handler); break;
                        case LevelType.FilterStart:
                            lv.Type.Clear();
                            lv.Type.Add(LevelType.Filter); break;
                    }
                    lvs.Remove(ks[i]);
                    lvs[o] = lv;
                    ks.Remove(ks[i]);
                    i--;
                }
                if (lvs[ks[i]].Handler[0] != null)
                {
                    int oo = lvs[ks[i]].GetEndOffset();
                    if ((lvs[ks[i]].GetOnlyLevelType() == LevelType.FilterEnd ||
                         lvs[ks[i]].GetOnlyLevelType() == LevelType.HandlerEnd ||
                         lvs[ks[i]].GetOnlyLevelType() == LevelType.Handler ||
                         lvs[ks[i]].GetOnlyLevelType() == LevelType.Filter) &&
                         !lvs.ContainsKey(oo))
                    {
                        lvs.Add(oo, new Level() { Handler = lvs[ks[i]].Handler, Type = new List<LevelType> { LevelType.None } });
                        ks.Add(oo);
                        ks.Sort();
                    }
                }
            }


            Dictionary<Instruction, Level> ret = new Dictionary<Instruction, Level>();
            int offset = 0;
            foreach (Instruction inst in body.Instructions)
            {
                if (inst.Offset >= offset && lvs.ContainsKey(inst.Offset))
                    offset = inst.Offset;
                ret.Add(inst, lvs[offset]);
            }
            return ret;
        }
        private Instruction[] GetInstructionsByLv(Level lv, Dictionary<Instruction, Level> ids)
        {
            List<Instruction> ret = new List<Instruction>();
            foreach (KeyValuePair<Instruction, Level> i in ids)
                if (i.Value == lv)
                    ret.Add(i.Key);

            return ret.ToArray();
        }
        private Level[] GetLvs(Dictionary<Instruction, Level> ids)
        {
            List<Level> ret = new List<Level>();
            foreach (Level lv in ids.Values)
                if (!ret.Contains(lv))
                    ret.Add(lv);
            return ret.ToArray();
        }
        private void SetLvHandler(Level lv, MethodBody body, Instruction[][] blocks)
        {
            for (int i = 0; i < lv.Handler.Count; i++)
            {
                if (lv.Handler[i] == null) return;
                switch (lv.Type[i])
                {
                    case LevelType.TryStart:
                        lv.Handler[i].TryStart = blocks[0][0];
                        break;
                    case LevelType.TryEnd:
                        lv.Handler[i].TryEnd = blocks[blocks.Length - 1][blocks[blocks.Length - 1].Length - 1];
                        break;
                    case LevelType.Try:
                        lv.Handler[i].TryStart = blocks[0][0];
                        lv.Handler[i].TryEnd = blocks[blocks.Length - 1][blocks[blocks.Length - 1].Length - 1];
                        break;
                    case LevelType.HandlerStart:
                        lv.Handler[i].HandlerStart = blocks[0][0];
                        break;
                    case LevelType.HandlerEnd:
                        lv.Handler[i].HandlerEnd = blocks[blocks.Length - 1][blocks[blocks.Length - 1].Length - 1];
                        break;
                    case LevelType.Handler:
                        lv.Handler[i].HandlerStart = blocks[0][0];
                        lv.Handler[i].HandlerEnd = blocks[blocks.Length - 1][blocks[blocks.Length - 1].Length - 1];
                        break;
                    case LevelType.FilterStart:
                        lv.Handler[i].FilterStart = blocks[0][0];
                        break;
                    case LevelType.FilterEnd:
                        //lv.Handler[i].FilterEnd = blks[blks.Length - 1][blks[blks.Length - 1].Length - 1];
                        break;
                    case LevelType.Filter:
                        lv.Handler[i].FilterStart = blocks[0][0];
                        //lv.Handler[i].FilterEnd = blks[blks.Length - 1][blks[blks.Length - 1].Length - 1];
                        break;
                    case LevelType.None:
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        private Instruction[][] BranchesSplit(Instruction[] insts, double factor)
        {
            List<Instruction[]> ret = new List<Instruction[]>();
            List<Instruction> block = new List<Instruction>();
            for (int i = 0; i < insts.Length; i++)
            {
                block.Add(insts[i]);
                if ((rand.NextDouble() < factor ||
                    insts[i].OpCode.Code == Code.Pop ||
                    insts[i].OpCode.Code == Code.Call ||
                    insts[i].OpCode.Code == Code.Ldloc) &&

                    insts[i].OpCode.OpCodeType != OpCodeType.Prefix &&
                    insts[i].OpCode.Code != Code.Ldftn && insts[i].OpCode.Code != Code.Ldvirtftn &&
                    (i + 1 == insts.Length || (insts[i + 1].OpCode.Code != Code.Ldftn && insts[i + 1].OpCode.Code != Code.Ldvirtftn)) &&
                    (i - 1 == -1 || (insts[i - 1].OpCode.Code != Code.Ldftn && insts[i - 1].OpCode.Code != Code.Ldvirtftn)))
                {
                    ret.Add(block.ToArray());
                    block.Clear();
                }
            }
            if (block.Count != 0)
                ret.Add(block.ToArray());
            return ret.ToArray();
        }
        private void AddBranches(MethodBody body, ref Instruction[][] blocks)
        {
            List<Instruction[]> ret = new List<Instruction[]>();
            for (int i = 0; i < blocks.Length; i++)
            {
                Instruction[] blk = blocks[i];
                List<Instruction> newBlock = new List<Instruction>(blk);

                if (i + 1 < blocks.Length)
                {
                    if (rand.Next() % 2 == 0)
                        AddJump(newBlock, blocks[i + 1][0]);
                    else
                    {
                        ConnectBlocks(newBlock, blocks[i + 1], blocks);
                        if (i + 2 < blocks.Length)
                            AddJump(newBlock, blocks[i + 2][0]);
                        i++;
                    }
                }
                ret.Add(newBlock.ToArray());
            }
            blocks = ret.ToArray();
        }
        private void Reorder(ref Instruction[][] insts)
        {
            int[] idx = new int[insts.Length];
            int[] ran = new int[insts.Length];
            Instruction[][] ret = new Instruction[insts.Length][];
            while (true)
            {
                for (int i = 0; i < insts.Length; i++)
                {
                    idx[i] = i;
                    ran[i] = rand.Next();
                }
                ran[0] = int.MinValue;
                ran[insts.Length - 1] = int.MaxValue;
                Array.Sort(ran, idx);
                bool f = true;
                for (int i = 1; i < insts.Length - 1; i++)
                    if (idx[i] == i)
                    {
                        f = false;
                        break;
                    }
                if (f || insts.Length - 2 == 1) break;
            }
            for (int i = 0; i < insts.Length; i++)
            {
                ret[idx[i]] = insts[i];
            }
            insts = ret;
        }
        static ushort[] junkCode = new ushort[]  {  0x24ff, 0x77ff, 0x78ff, 0xa6ff, 0xa7ff, 
                                                    0xa8ff, 0xa9ff, 0xaaff, 0xabff, 0xacff,
                                                    0xadff, 0xaeff, 0xafff, 0xb0ff, 0xb1ff,
                                                    0xb2ff, 0xbbff, 0xbcff, 0xbdff, 0xbeff,
                                                    0xbfff, 0xc0ff, 0xc1ff, 0xc4ff, 0xc5ff,
                                                    0xc7ff, 0xc8ff, 0xc9ff, 0xcaff, 0xcbff,
                                                    0xccff, 0xcdff, 0xceff, 0xcfff, 0x08fe,
                                                    0x19fe, 0x1bfe, 0x1ffe};
        private void ConnectBlocks(List<Instruction> insts, Instruction[] block, Instruction[][] blocks)
        {
            if (method.Module.Runtime != TargetRuntime.Net_4_0)
            {
                Instruction t = blocks[rand.Next(0, blocks.Length)][0];
                switch (rand.Next(0, 2))
                {
                    case 0:
                        insts.Add(Instruction.Create(OpCodes.Ldtoken, method.DeclaringType));
                        insts.Add(Instruction.Create(OpCodes.Brfalse, t));
                        break;
                    case 1:
                        int i = rand.Next(-1, 9);
                        insts.Add(Instruction.Create(OpCodes.Ldc_I4, i));
                        insts.Add(Instruction.Create(i == 0 ? OpCodes.Brtrue : OpCodes.Brfalse, t));
                        break;
                }

            }
            insts.AddRange(block);
        }
        private void AddJump(List<Instruction> insts, Instruction target)
        {
            if (method.Module.Runtime != TargetRuntime.Net_4_0)
            {
                switch (rand.Next(0, 2))
                {
                    case 0:
                        insts.Add(Instruction.Create(OpCodes.Ldtoken, method.DeclaringType));
                        insts.Add(Instruction.Create(OpCodes.Brtrue, target));
                        break;
                    case 1:
                        insts.Add(Instruction.Create(OpCodes.Br, target));
                        break;
                }
            }
            else
                insts.Add(Instruction.Create(OpCodes.Br, target));

            if (genJunk)
            {
                switch (rand.Next(0, 4))
                {
                    case 0:
                        insts.Add(Instruction.Create(OpCodes.Pop)); break;
                    case 1:
                        insts.Add(Instruction.Create(OpCodes.Ldc_I4, rand.Next(-1, 9))); break;
                    case 2:
                        insts.Add(Instruction.Create(OpCodes.Dup)); break;
                    case 3:
                        insts.Add(Instruction.CreateJunkCode(junkCode[rand.Next(0, junkCode.Length)])); break;
                }
            }
            else
            {
                switch (rand.Next(0, 3))
                {
                    case 0:
                        insts.Add(Instruction.Create(OpCodes.Pop)); break;
                    case 1:
                        insts.Add(Instruction.Create(OpCodes.Ldc_I4, rand.Next(-1, 9))); break;
                    case 2:
                        insts.Add(Instruction.Create(OpCodes.Dup)); break;
                }
            }
        }

    }
}