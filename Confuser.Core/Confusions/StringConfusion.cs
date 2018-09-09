using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Collections;
using System.Security.Cryptography;
using System.IO;
using System.Diagnostics;
using System.IO.Compression;
using Confuser.Core.Poly;
using Confuser.Core.Poly.Visitors;

namespace Confuser.Core.Confusions
{
    public class StringConfusion : IConfusion
    {
        class Phase1 : StructurePhase
        {
            public Phase1(StringConfusion sc) { this.sc = sc; }
            StringConfusion sc;
            public override IConfusion Confusion
            {
                get { return sc; }
            }

            public override int PhaseID
            {
                get { return 1; }
            }

            public override Priority Priority
            {
                get { return Priority.CodeLevel; }
            }

            public override bool WholeRun
            {
                get { return true; }
            }

            public override void Initialize(ModuleDefinition mod)
            {
                this.mod = mod;

                sc.dats = new List<byte[]>();
                sc.idx = 0;
                sc.dict = new Dictionary<string, int>();
            }

            public override void DeInitialize()
            {
                //
            }

            ModuleDefinition mod;
            public override void Process(ConfusionParameter parameter)
            {
                if (Array.IndexOf(parameter.GlobalParameters.AllKeys, "dynamic") == -1)
                {
                    ProcessSafe(parameter); return;
                }

                Random rand = new Random();
                TypeDefinition modType = mod.GetType("<Module>");

                AssemblyDefinition id = AssemblyDefinition.ReadAssembly(typeof(Iid).Assembly.Location);
                sc.strer = id.MainModule.GetType("Encryptions").Methods.FirstOrDefault(mtd => mtd.Name == "Strings");
                sc.strer = CecilHelper.Inject(mod, sc.strer);
                modType.Methods.Add(sc.strer);
                byte[] n = new byte[0x10]; rand.NextBytes(n);
                sc.strer.Name = Encoding.UTF8.GetString(n);
                sc.strer.IsAssembly = true;
                AddHelper(sc.strer, HelperAttribute.NoInjection);

                sc.key0 = (int)(rand.NextDouble() * int.MaxValue);
                sc.key1 = (int)(rand.NextDouble() * int.MaxValue);

                rand.NextBytes(n);
                byte[] dat = new byte[0x10];
                rand.NextBytes(dat);

                sc.strer.Body.SimplifyMacros();
                for (int i = 0; i < sc.strer.Body.Instructions.Count; i++)
                {
                    Instruction inst = sc.strer.Body.Instructions[i];
                    if ((inst.Operand as string) == "PADDINGPADDINGPADDING")
                        inst.Operand = Encoding.UTF8.GetString(n);
                    if ((inst.Operand as string) == "PADDINGPADDINGPADDINGPADDING")
                        inst.Operand = Encoding.UTF8.GetString(dat);
                    else if (inst.Operand is int && (int)inst.Operand == 12345678)
                        inst.Operand = sc.key0;
                    else if (inst.Operand is int && (int)inst.Operand == 87654321)
                        inst.Operand = sc.key1;
                }

                sc.resId = Encoding.UTF8.GetString(n);
            }
            private void ProcessSafe(ConfusionParameter parameter)
            {
                Random rand = new Random();
                TypeDefinition modType = mod.GetType("<Module>");

                AssemblyDefinition i = AssemblyDefinition.ReadAssembly(typeof(Iid).Assembly.Location);
                sc.strer = i.MainModule.GetType("Encryptions").Methods.FirstOrDefault(mtd => mtd.Name == "SafeStrings");
                sc.strer = CecilHelper.Inject(mod, sc.strer);
                modType.Methods.Add(sc.strer);
                byte[] n = new byte[0x10]; rand.NextBytes(n);
                sc.strer.Name = Encoding.UTF8.GetString(n);
                sc.strer.IsAssembly = true;

                sc.key0 = (int)(rand.NextDouble() * int.MaxValue);
                sc.key1 = (int)(rand.NextDouble() * int.MaxValue);
                sc.key2 = (int)(rand.NextDouble() * int.MaxValue);

                rand.NextBytes(n);
                byte[] dat = new byte[0x10];
                rand.NextBytes(dat);

                sc.strer.Body.SimplifyMacros();
                foreach (Instruction inst in sc.strer.Body.Instructions)
                {
                    if ((inst.Operand as string) == "PADDINGPADDINGPADDING")
                        inst.Operand = Encoding.UTF8.GetString(n);
                    if ((inst.Operand as string) == "PADDINGPADDINGPADDINGPADDING")
                        inst.Operand = Encoding.UTF8.GetString(dat);
                    else if (inst.Operand is int && (int)inst.Operand == 12345678)
                        inst.Operand = sc.key0;
                    else if (inst.Operand is int && (int)inst.Operand == 87654321)
                        inst.Operand = sc.key1;
                    else if (inst.Operand is int && (int)inst.Operand == 88888888)
                        inst.Operand = sc.key2;
                }
                sc.strer.Body.OptimizeMacros();
                sc.strer.Body.ComputeOffsets();

                sc.resId = Encoding.UTF8.GetString(n);
            }
        }
        class Phase3 : StructurePhase, IProgressProvider
        {
            public Phase3(StringConfusion sc) { this.sc = sc; }
            StringConfusion sc;
            public override IConfusion Confusion
            {
                get { return sc; }
            }

            public override int PhaseID
            {
                get { return 3; }
            }

            public override Priority Priority
            {
                get { return Priority.Safe; }
            }

            public override bool WholeRun
            {
                get { return false; }
            }

            public override void Initialize(ModuleDefinition mod)
            {
                this.mod = mod;
            }

            public override void DeInitialize()
            {
                MemoryStream str = new MemoryStream();
                using (BinaryWriter wtr = new BinaryWriter(new DeflateStream(str, CompressionMode.Compress)))
                {
                    foreach (byte[] b in sc.dats)
                        wtr.Write(b);
                }
                mod.Resources.Add(new EmbeddedResource(sc.resId, ManifestResourceAttributes.Private, str.ToArray()));
            }

            struct Context { public MethodDefinition mtd; public ILProcessor psr; public Instruction str;}
            ModuleDefinition mod;
            public override void Process(ConfusionParameter parameter)
            {
                if (Array.IndexOf(parameter.GlobalParameters.AllKeys, "dynamic") == -1)
                {
                    ProcessSafe(parameter); return;
                }

                List<Context> txts = new List<Context>();

                foreach (MethodDefinition mtd in parameter.Target as IList<IAnnotationProvider>)
                {
                    if (mtd == sc.strer || !mtd.HasBody) continue;
                    var bdy = mtd.Body;
                    bdy.SimplifyMacros();
                    var insts = bdy.Instructions;
                    ILProcessor psr = bdy.GetILProcessor();
                    for (int i = 0; i < insts.Count; i++)
                    {
                        if (insts[i].OpCode.Code == Code.Ldstr)
                            txts.Add(new Context() { mtd = mtd, psr = psr, str = insts[i] });
                    }
                }

                double total = txts.Count;
                int interval = 1;
                if (total > 1000)
                    interval = (int)total / 100;

                int[] ids;
                bool retry;
                do
                {
                    ids = new int[txts.Count];
                    retry = false;
                    sc.dict.Clear();
                    int seed;
                    sc.exp = ExpressionGenerator.Generate(5, out seed);

                    for (int i = 0; i < txts.Count; i++)
                    {
                        string val = txts[i].str.Operand as string;
                        if (val == "") continue;

                        if (sc.dict.ContainsKey(val))
                            ids[i] = (int)((sc.dict[val] + sc.key0) ^ txts[i].mtd.MetadataToken.ToUInt32());
                        else
                        {
                            ids[i] = (int)((sc.idx + sc.key0) ^ txts[i].mtd.MetadataToken.ToUInt32());
                            int len;
                            byte[] dat = Encrypt(val, sc.exp, out len);
                            try
                            {
                                if (Decrypt(dat, len, sc.exp) != val)
                                {
                                    retry = true;
                                    break;
                                }
                            }
                            catch
                            {
                                retry = true;
                                break;
                            }
                            len = (int)~(len ^ sc.key1);

                            byte[] final = new byte[dat.Length + 4];
                            Buffer.BlockCopy(dat, 0, final, 4, dat.Length);
                            Buffer.BlockCopy(BitConverter.GetBytes(len), 0, final, 0, 4);
                            sc.dats.Add(final);
                            sc.dict[val] = sc.idx;
                            sc.idx += final.Length;
                        }
                    }
                } while (retry);

                for (int i = 0; i < sc.strer.Body.Instructions.Count; i++)
                {
                    Instruction inst = sc.strer.Body.Instructions[i];
                    if (inst.Operand is MethodReference && ((MethodReference)inst.Operand).Name == "PolyStart")
                    {
                        List<Instruction> insts = new List<Instruction>();
                        int ptr = i + 1;
                        while (ptr < sc.strer.Body.Instructions.Count)
                        {
                            Instruction z = sc.strer.Body.Instructions[ptr];
                            sc.strer.Body.Instructions.Remove(z);
                            if (z.Operand is MethodReference && ((MethodReference)z.Operand).Name == "PlaceHolder")
                                break;
                            insts.Add(z);
                        }

                        Instruction[] expInsts = new CecilVisitor(sc.exp, true, insts.ToArray(), false).GetInstructions();
                        ILProcessor psr = sc.strer.Body.GetILProcessor();
                        psr.Replace(inst, expInsts[0]);
                        for (int ii = 1; ii < expInsts.Length; ii++)
                        {
                            psr.InsertAfter(expInsts[ii - 1], expInsts[ii]);
                        }
                    }
                }
                sc.strer.Body.OptimizeMacros();
                sc.strer.Body.ComputeOffsets();

                for (int i = 0; i < txts.Count; i++)
                {
                    int idx = txts[i].mtd.Body.Instructions.IndexOf(txts[i].str);
                    Instruction now = txts[i].str;
                    if (now.Operand as string == "") continue;
                    txts[i].psr.InsertAfter(idx, txts[i].psr.Create(OpCodes.Call, sc.strer));
                    txts[i].psr.Replace(idx, txts[i].psr.Create(OpCodes.Ldc_I4, ids[i]));
                    if (i % interval == 0 || i == txts.Count - 1)
                        progresser.SetProgress((i + 1) / total);
                }

                List<int> hashs = new List<int>();
                for (int i = 0; i < txts.Count; i++)
                {
                    if (hashs.IndexOf(txts[i].mtd.GetHashCode()) == -1)
                    {
                        txts[i].mtd.Body.OptimizeMacros();
                        txts[i].mtd.Body.ComputeHeader();
                        hashs.Add(txts[i].mtd.GetHashCode());
                    }
                }
            }
            void ProcessSafe(ConfusionParameter parameter)
            {
                List<Context> txts = new List<Context>();

                foreach (MethodDefinition mtd in parameter.Target as IList<IAnnotationProvider>)
                {
                    if (mtd == sc.strer || !mtd.HasBody) continue;
                    var bdy = mtd.Body;
                    bdy.SimplifyMacros();
                    var insts = bdy.Instructions;
                    ILProcessor psr = bdy.GetILProcessor();
                    for (int i = 0; i < insts.Count; i++)
                    {
                        if (insts[i].OpCode.Code == Code.Ldstr)
                            txts.Add(new Context() { mtd = mtd, psr = psr, str = insts[i] });
                    }
                }

                double total = txts.Count;
                int interval = 1;
                if (total > 1000)
                    interval = (int)total / 100;
                for (int i = 0; i < txts.Count; i++)
                {
                    int idx = txts[i].mtd.Body.Instructions.IndexOf(txts[i].str);
                    string val = txts[i].str.Operand as string;
                    if (val == "") continue;

                    int id;
                    if (sc.dict.ContainsKey(val))
                        id = (int)((sc.dict[val] + sc.key0) ^ txts[i].mtd.MetadataToken.ToUInt32());
                    else
                    {
                        byte[] dat = EncryptSafe(val, sc.key2);
                        id = (int)((sc.idx + sc.key0) ^ txts[i].mtd.MetadataToken.ToUInt32());
                        int len = (int)~(dat.Length ^ sc.key1);

                        byte[] final = new byte[dat.Length + 4];
                        Buffer.BlockCopy(dat, 0, final, 4, dat.Length);
                        Buffer.BlockCopy(BitConverter.GetBytes(len), 0, final, 0, 4);
                        sc.dats.Add(final);
                        sc.dict[val] = sc.idx;
                        sc.idx += final.Length;
                    }

                    Instruction now = txts[i].str;
                    txts[i].psr.InsertAfter(idx, txts[i].psr.Create(OpCodes.Call, sc.strer));
                    txts[i].psr.Replace(idx, txts[i].psr.Create(OpCodes.Ldc_I4, id));

                    if (i % interval == 0 || i == txts.Count - 1)
                        progresser.SetProgress((i + 1) / total);
                }

                List<int> hashs = new List<int>();
                for (int i = 0; i < txts.Count; i++)
                {
                    if (hashs.IndexOf(txts[i].mtd.GetHashCode()) == -1)
                    {
                        txts[i].mtd.Body.OptimizeMacros();
                        txts[i].mtd.Body.ComputeHeader();
                        hashs.Add(txts[i].mtd.GetHashCode());
                    }
                }
            }

            IProgresser progresser;
            public void SetProgresser(IProgresser progresser)
            {
                this.progresser = progresser;
            }
        }


        List<byte[]> dats;
        Dictionary<string, int> dict;
        int idx = 0;

        string resId;
        int key0;
        int key1;
        int key2;
        MethodDefinition strer;

        Expression exp;

        public string ID
        {
            get { return "string encrypt"; }
        }
        public string Name
        {
            get { return "User Strings Confusion"; }
        }
        public string Description
        {
            get { return "This confusion obfuscate the strings in the code and store them in a encrypted and compressed form."; }
        }
        public Target Target
        {
            get { return Target.Methods; }
        }
        public Preset Preset
        {
            get { return Preset.Minimum; }
        }
        public bool StandardCompatible
        {
            get { return true; }
        }
        public bool SupportLateAddition
        {
            get { return true; }
        }
        public Behaviour Behaviour
        {
            get { return Behaviour.Inject | Behaviour.AlterCode | Behaviour.Encrypt; }
        }

        Phase[] ps;
        public Phase[] Phases
        {
            get
            {
                if (ps == null)
                    ps = new Phase[] { new Phase1(this), new Phase3(this) };
                return ps;
            }
        }

        static void Write7BitEncodedInt(BinaryWriter wtr, int value)
        {
            // Write out an int 7 bits at a time. The high bit of the byte,
            // when on, tells reader to continue reading more bytes.
            uint v = (uint)value; // support negative numbers
            while (v >= 0x80)
            {
                wtr.Write((byte)(v | 0x80));
                v >>= 7;
            }
            wtr.Write((byte)v);
        }
        static int Read7BitEncodedInt(BinaryReader rdr)
        {
            // Read out an int 7 bits at a time. The high bit
            // of the byte when on means to continue reading more bytes.
            int count = 0;
            int shift = 0;
            byte b;
            do
            {
                b = rdr.ReadByte();
                count |= (b & 0x7F) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);
            return count;
        }

        private static byte[] Encrypt(string str, Expression exp, out int len)
        {
            byte[] bs = Encoding.Unicode.GetBytes(str);
            byte[] tmp = new byte[(bs.Length + 7) & ~7];
            Buffer.BlockCopy(bs, 0, tmp, 0, bs.Length);
            len = bs.Length;

            MemoryStream ret = new MemoryStream();
            using (BinaryWriter wtr = new BinaryWriter(ret))
            {
                for (int i = 0; i < tmp.Length; i++)
                {
                    int en = (int)LongExpressionEvaluator.Evaluate(exp, tmp[i]);
                    Write7BitEncodedInt(wtr, en);
                }
            }

            return ret.ToArray();
        }
        private static string Decrypt(byte[] bytes, int len, Expression exp)
        {
            byte[] ret = new byte[(len + 7) & ~7];

            using (BinaryReader rdr = new BinaryReader(new MemoryStream(bytes)))
            {
                for (int i = 0; i < ret.Length; i++)
                {
                    int r = Read7BitEncodedInt(rdr);
                    ret[i] = (byte)LongExpressionEvaluator.Evaluate(exp, r);
                }
            }

            return Encoding.Unicode.GetString(ret, 0, len);
        }

        private static byte[] EncryptSafe(string str, int key)
        {
            ushort _m = (ushort)(key >> 16);
            ushort _c = (ushort)(key & 0xffff);
            ushort m = _c; ushort c = _m;
            byte[] z = new byte[b.Length];
            for (int i = 0; i < k.Length; i++)
            {
                z[i] = (byte)((key * m + c) % 0x100);
                m = (ushort)((key * m + _m) % 0x10000);
                c = (ushort)((key * c + _c) % 0x10000);
            }

            byte[] bs = Encoding.UTF8.GetBytes(str);

            int k = 0;
            for (int i = 0; i < bs.Length; i++)
            {
                bs[i] = (byte)(bs[i] ^ (k / z[i]));
                k += bs[i];
            }

            return bs;
        }
        private static string DecryptSafe(byte[] bytes, int key)
        {
            Random rand = new Random(key);

            int k = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte o = bytes[i];
                bytes[i] = (byte)(bytes[i] ^ (rand.Next() & k));
                k += o;
            }
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
