using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Confuser.Core.Poly;
using System.IO;
using Mono.Cecil.Cil;
using System.IO.Compression;
using Confuser.Core.Poly.Visitors;
using System.Collections.Specialized;
using Mono.Cecil.Metadata;
using System.Security.Cryptography;
using Confuser.Core.Poly.Math;
using Confuser.Core.Poly.Strings;

namespace Confuser.Core.Confusions
{
    public class ConstantConfusion : IConfusion
    {
        class Phase1 : StructurePhase
        {
            public Phase1(ConstantConfusion cc) { this.cc = cc; }
            ConstantConfusion cc;
            public override IConfusion Confusion
            {
                get { return cc; }
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
                cc.txts[mod] = new _Context();

                cc.txts[mod].dats = new List<byte[]>();
                cc.txts[mod].idx = 0;
                cc.txts[mod].dict = new Dictionary<object, int>();
            }

            public override void DeInitialize()
            {
                //
            }

            ModuleDefinition mod;
            public override void Process(ConfusionParameter parameter)
            {
                Database.AddEntry("Const", "Type", parameter.GlobalParameters["type"] ?? "normal");
                if (parameter.GlobalParameters["type"] != "dynamic" &&
                    parameter.GlobalParameters["type"] != "native")
                {
                    ProcessSafe(parameter); return;
                }
                _Context txt = cc.txts[mod];
                txt.isNative = parameter.GlobalParameters["type"] == "native";
                txt.isDyn = parameter.GlobalParameters["type"] == "dynamic";

                TypeDefinition modType = mod.GetType("<Module>");

                FieldDefinition constTbl = new FieldDefinition(
                    ObfuscationHelper.GetRandomName(),
                    FieldAttributes.Static | FieldAttributes.CompilerControlled,
                    mod.Import(typeof(Dictionary<uint, object>)));
                modType.Fields.Add(constTbl);
                AddHelper(constTbl, HelperAttribute.NoInjection);

                Database.AddEntry("Const", "ConstTbl", constTbl.FullName);

                FieldDefinition constBuffer = new FieldDefinition(
                    ObfuscationHelper.GetRandomName(),
                    FieldAttributes.Static | FieldAttributes.CompilerControlled,
                    mod.Import(typeof(byte[])));
                modType.Fields.Add(constBuffer);
                AddHelper(constBuffer, HelperAttribute.NoInjection);
                Database.AddEntry("Const", "ConstBuffer", constBuffer.FullName);


                if (txt.isNative)
                {
                    txt.nativeDecr = new MethodDefinition(
                        ObfuscationHelper.GetRandomName(),
                        MethodAttributes.Abstract | MethodAttributes.CompilerControlled |
                        MethodAttributes.ReuseSlot | MethodAttributes.Static,
                        mod.TypeSystem.Int32);
                    txt.nativeDecr.ImplAttributes = MethodImplAttributes.Native;
                    txt.nativeDecr.Parameters.Add(new ParameterDefinition(mod.TypeSystem.Int32));
                    modType.Methods.Add(txt.nativeDecr);
                    Database.AddEntry("Const", "NativeDecr", txt.nativeDecr.FullName);
                }

                var expGen = new ExpressionGenerator(Random.Next(), mod);
                int seed = expGen.Seed;
                if (txt.isNative)
                {
                    do
                    {
                        txt.exp = new ExpressionGenerator(Random.Next(), mod).Generate(6);
                        txt.invExp = ExpressionInverser.InverseExpression(txt.exp);
                    } while ((txt.visitor = new x86Visitor(txt.invExp, null)).RegisterOverflowed);
                }
                else
                {
                    txt.exp = expGen.Generate(10);
                    txt.invExp = ExpressionInverser.InverseExpression(txt.exp);
                }
                Database.AddEntry("Const", "Exp", txt.exp);
                Database.AddEntry("Const", "InvExp", txt.invExp);

                txt.consters = CreateConsters(txt, Confuser.Random, "Initialize", constTbl, constBuffer);
            }
            private void ProcessSafe(ConfusionParameter parameter)
            {
                _Context txt = cc.txts[mod];
                //txt.isNative = true;

                TypeDefinition modType = mod.GetType("<Module>");

                FieldDefinition constTbl = new FieldDefinition(
                    ObfuscationHelper.GetRandomName(),
                    FieldAttributes.Static | FieldAttributes.CompilerControlled,
                    mod.Import(typeof(Dictionary<uint, object>)));
                modType.Fields.Add(constTbl);
                AddHelper(constTbl, HelperAttribute.NoInjection);
                Database.AddEntry("Const", "ConstTbl", constTbl.FullName);

                FieldDefinition constBuffer = new FieldDefinition(
                    ObfuscationHelper.GetRandomName(),
                    FieldAttributes.Static | FieldAttributes.CompilerControlled,
                    mod.Import(typeof(byte[])));
                modType.Fields.Add(constBuffer);
                AddHelper(constBuffer, HelperAttribute.NoInjection);
                Database.AddEntry("Const", "ConstBuffer", constBuffer.FullName);

                txt.consters = CreateConsters(txt, Random, "InitializeSafe", constTbl, constBuffer);
            }
            Conster[] CreateConsters(_Context txt, Random rand, string injectName,
                                     FieldDefinition constTbl, FieldDefinition constBuffer)
            {
                AssemblyDefinition injection = AssemblyDefinition.ReadAssembly(typeof(Iid).Assembly.Location);
                injection.MainModule.ReadSymbols();
                MethodDefinition method = injection.MainModule.GetType("Encryptions").Methods.FirstOrDefault(mtd => mtd.Name == "Constants");
                List<Conster> ret = new List<Conster>();

                TypeDefinition lzma = mod.GetType("Lzma" + mod.GetHashCode());
                if (lzma == null)
                {
                    lzma = CecilHelper.Inject(mod, injection.MainModule.GetType("Lzma"));
                    lzma.IsNotPublic = true;
                    lzma.Name = "Lzma" + mod.GetHashCode();
                    mod.Types.Add(lzma);
                }

                rand.NextBytes(txt.keyBuff);
                for (int i = 0; i < txt.keyBuff.Length; i++)
                    txt.keyBuff[i] &= 0x7f;
                txt.keyBuff[0] = 7; txt.keyBuff[1] = 0;
                txt.resKey = (rand.Next(0x20, 0x80) << 24) | (rand.Next(0x20, 0x80) << 32) |
                             (rand.Next(0x20, 0x80) << 16) | (rand.Next(0x20, 0x80) << 0 );
                txt.resId = Encoding.UTF8.GetString(BitConverter.GetBytes(txt.resKey));
                txt.key = (uint)rand.Next();

                Database.AddEntry("Const", "KeyBuff", txt.keyBuff);
                Database.AddEntry("Const", "ResKey", txt.resKey);
                Database.AddEntry("Const", "ResId", txt.resId);
                Database.AddEntry("Const", "Key", txt.key);


                Mutator mutator = new Mutator();
                MethodDefinition init = injection.MainModule.GetType("Encryptions").Methods.FirstOrDefault(mtd => mtd.Name == injectName);
                {
                    MethodDefinition cctor = mod.GetType("<Module>").GetStaticConstructor();
                    MethodDefinition m = CecilHelper.Inject(mod, init);
                    Instruction placeholder = null;
                    mutator.IntKeys = new int[] { txt.resKey };
                    mutator.Module = mod;
                    mutator.Mutate(Random, m.Body, mod);
                    txt.keyInst = mutator.Delayed0;
                    placeholder = mutator.Placeholder;
                    foreach (Instruction inst in m.Body.Instructions)
                    {
                        if (inst.Operand is FieldReference)
                        {
                            if ((inst.Operand as FieldReference).Name == "constTbl")
                                inst.Operand = constTbl;
                            else if ((inst.Operand as FieldReference).Name == "constBuffer")
                                inst.Operand = constBuffer;
                        }
                        else if (inst.Operand is MethodReference &&
                            (inst.Operand as MethodReference).DeclaringType.Name == "LzmaDecoder")
                            inst.Operand = lzma.NestedTypes
                                .Single(_ => _.Name == "LzmaDecoder").Methods
                                .Single(_ => _.Name == (inst.Operand as MethodReference).Name);
                    }
                    foreach (var i in m.Body.Variables)
                        if (i.VariableType.Name == "LzmaDecoder")
                            i.VariableType = lzma.NestedTypes.Single(_ => _.Name == "LzmaDecoder");

                    if (txt.isNative)
                        CecilHelper.Replace(m.Body, placeholder, new Instruction[]
                        {
                            Instruction.Create(OpCodes.Call, txt.nativeDecr)
                        });
                    else if (txt.isDyn)
                    {
                        Instruction ldloc = placeholder.Previous;
                        m.Body.Instructions.Remove(placeholder.Previous);   //ldloc
                        CecilHelper.Replace(m.Body, placeholder, new CecilVisitor(txt.invExp, new Instruction[]
                        {
                            ldloc
                        }).GetInstructions());
                    }

                    ILProcessor psr = cctor.Body.GetILProcessor();
                    Instruction begin = cctor.Body.Instructions[0];
                    for (int i = m.Body.Instructions.Count - 1; i >= 0; i--)
                    {
                        if (m.Body.Instructions[i].OpCode != OpCodes.Ret)
                            psr.InsertBefore(0, m.Body.Instructions[i]);
                    }
                    cctor.Body.InitLocals = true;
                    foreach (var i in m.Body.Variables)
                        cctor.Body.Variables.Add(i);
                }

                byte[] n = new byte[0x10];
                int typeDefCount = rand.Next(1, 10);
                for (int i = 0; i < typeDefCount; i++)
                {
                    TypeDefinition typeDef = new TypeDefinition(
                        "", ObfuscationHelper.GetRandomName(),
                        TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.NotPublic | TypeAttributes.Sealed,
                        mod.TypeSystem.Object);
                    mod.Types.Add(typeDef);
                    int methodCount = rand.Next(1, 5);
                    Database.AddEntry("Const", "ConsterTypes", typeDef.FullName);

                    for (int j = 0; j < methodCount; j++)
                    {
                        MethodDefinition mtd = CecilHelper.Inject(mod, method);
                        mtd.Name = ObfuscationHelper.GetRandomName();
                        mtd.IsCompilerControlled = true;

                        AddHelper(mtd, HelperAttribute.NoInjection);
                        typeDef.Methods.Add(mtd);

                        Database.AddEntry("Const", "ConsterMethods", mtd.FullName);

                        Conster conster = new Conster();
                        conster.key0 = (long)rand.Next() * rand.Next();
                        conster.key1 = (long)rand.Next() * rand.Next();
                        conster.key2 = (long)rand.Next() * rand.Next();
                        conster.key3 = rand.Next();
                        conster.conster = mtd;
                        Database.AddEntry("Const", mtd.FullName, string.Format("{0:X}, {1:X}, {2:X}, {3:X}", conster.key0, conster.key1, conster.key2, conster.key3));

                        mutator = new Mutator();
                        mutator.LongKeys = new long[]
                        {
                            conster.key0,
                            conster.key1,
                            conster.key2
                        };
                        mutator.IntKeys = new int[] { conster.key3 };
                        mutator.Mutate(Random, mtd.Body, mod);
                        foreach (Instruction inst in mtd.Body.Instructions)
                            if (inst.Operand is FieldReference)
                            {
                                if ((inst.Operand as FieldReference).Name == "constTbl")
                                    inst.Operand = constTbl;
                                else if ((inst.Operand as FieldReference).Name == "constBuffer")
                                    inst.Operand = constBuffer;
                            }
                        conster.keyInst = mutator.Delayed0;
                        ret.Add(conster);
                    }
                }
                return ret.ToArray();
            }
        }
        class Phase3 : StructurePhase, IProgressProvider
        {
            public Phase3(ConstantConfusion cc) { this.cc = cc; }
            ConstantConfusion cc;
            public override IConfusion Confusion
            {
                get { return cc; }
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
                _Context txt = cc.txts[mod];
                byte[] final;

                MemoryStream str = new MemoryStream();
                using (BinaryWriter wtr = new BinaryWriter(str))
                    foreach (byte[] dat in txt.dats)
                        wtr.Write(dat);
                byte[] buff = XorCrypt(str.ToArray(), txt.key);

                if (txt.isDyn || txt.isNative)
                {
                    byte[] e = Encrypt(buff, txt.exp);

                    int dictionary = 1 << 23;

                    Int32 posStateBits = 2;
                    Int32 litContextBits = 3; // for normal files
                    // UInt32 litContextBits = 0; // for 32-bit data
                    Int32 litPosBits = 0;
                    // UInt32 litPosBits = 2; // for 32-bit data
                    Int32 algorithm = 2;
                    Int32 numFastBytes = 128;
                    string mf = "bt4";

                    SevenZip.CoderPropID[] propIDs = 
				    {
					    SevenZip.CoderPropID.DictionarySize,
					    SevenZip.CoderPropID.PosStateBits,
					    SevenZip.CoderPropID.LitContextBits,
					    SevenZip.CoderPropID.LitPosBits,
					    SevenZip.CoderPropID.Algorithm,
					    SevenZip.CoderPropID.NumFastBytes,
					    SevenZip.CoderPropID.MatchFinder,
					    SevenZip.CoderPropID.EndMarker
				    };
                    object[] properties = 
				    {
					    (int)dictionary,
					    (int)posStateBits,
					    (int)litContextBits,
					    (int)litPosBits,
					    (int)algorithm,
					    (int)numFastBytes,
					    mf,
					    false
				    };

                    MemoryStream x = new MemoryStream();
                    var encoder = new SevenZip.Compression.LZMA.Encoder();
                    encoder.SetCoderProperties(propIDs, properties);
                    encoder.WriteCoderProperties(x);
                    Int64 fileSize;
                    MemoryStream output = new MemoryStream();
                    fileSize = e.Length;
                    for (int i = 0; i < 8; i++)
                        x.WriteByte((Byte)(fileSize >> (8 * i)));
                    encoder.Code(new MemoryStream(e), x, -1, -1, null);


                    using (var s = new CryptoStream(output,
                        new RijndaelManaged().CreateEncryptor(txt.keyBuff, MD5.Create().ComputeHash(txt.keyBuff))
                        , CryptoStreamMode.Write))
                        s.Write(x.ToArray(), 0, (int)x.Length);

                    final = output.ToArray();
                }
                else
                {
                    // hits here
                    int dictionary = 1 << 23;

                    Int32 posStateBits = 2;
                    Int32 litContextBits = 3; // for normal files
                    // UInt32 litContextBits = 0; // for 32-bit data
                    Int32 litPosBits = 0;
                    // UInt32 litPosBits = 2; // for 32-bit data
                    Int32 algorithm = 2;
                    Int32 numFastBytes = 128;
                    string mf = "bt4";

                    SevenZip.CoderPropID[] propIDs = 
				    {
					    SevenZip.CoderPropID.DictionarySize,
					    SevenZip.CoderPropID.PosStateBits,
					    SevenZip.CoderPropID.LitContextBits,
					    SevenZip.CoderPropID.LitPosBits,
					    SevenZip.CoderPropID.Algorithm,
					    SevenZip.CoderPropID.NumFastBytes,
					    SevenZip.CoderPropID.MatchFinder,
					    SevenZip.CoderPropID.EndMarker
				    };
                    object[] properties = 
				    {
					    (int)dictionary,
					    (int)posStateBits,
					    (int)litContextBits,
					    (int)litPosBits,
					    (int)algorithm,
					    (int)numFastBytes,
					    mf,
					    false
				    };

                    MemoryStream x = new MemoryStream();
                    var encoder = new SevenZip.Compression.LZMA.Encoder();
                    encoder.SetCoderProperties(propIDs, properties);
                    encoder.WriteCoderProperties(x);
                    Int64 fileSize;
                    fileSize = buff.Length;
                    for (int i = 0; i < 8; i++)
                        x.WriteByte((Byte)(fileSize >> (8 * i)));
                    encoder.Code(new MemoryStream(buff), x, -1, -1, null);

                    MemoryStream output = new MemoryStream();
                    using (var s = new CryptoStream(output,
                        new RijndaelManaged().CreateEncryptor(txt.keyBuff, MD5.Create().ComputeHash(txt.keyBuff))
                        , CryptoStreamMode.Write))
                        s.Write(x.ToArray(), 0, (int)x.Length);

                    final = EncryptSafe(output.ToArray(), BitConverter.ToUInt32(txt.keyBuff, 0xc) * (uint)txt.resKey);
                }
                mod.Resources.Add(new EmbeddedResource(txt.resId, ManifestResourceAttributes.Private, final));
            }

            class Context
            {
                public MethodDefinition mtd;
                public ILProcessor psr;
                public Instruction str;
                public uint a;
                public ulong b;
                public Conster conster;
            }
            ModuleDefinition mod;
            bool IsNull(object obj)
            {
                if (obj is int)
                    return (int)obj == 0;
                else if (obj is long)
                    return (long)obj == 0;
                else if (obj is float)
                    return (float)obj == 0;
                else if (obj is double)
                    return (double)obj == 0;
                else if (obj is string)
                    return string.IsNullOrEmpty((string)obj);
                else
                    return true;
            }
            void ExtractData(IList<Tuple<IAnnotationProvider, NameValueCollection>> mtds,
                List<Context> txts, bool num, _Context txt)
            {
                foreach (var tuple in mtds)
                {
                    MethodDefinition mtd = tuple.Item1 as MethodDefinition;
                    if (cc.txts[mod].consters.Any(_ => _.conster == mtd) || !mtd.HasBody) continue;
                    var bdy = mtd.Body;
                    var insts = bdy.Instructions;
                    ILProcessor psr = bdy.GetILProcessor();
                    for (int i = 0; i < insts.Count; i++)
                    {
                        if (insts[i].OpCode.Code == Code.Ldstr ||
                            (num && (insts[i].OpCode.Code == Code.Ldc_I4 ||
                            insts[i].OpCode.Code == Code.Ldc_I8 ||
                            insts[i].OpCode.Code == Code.Ldc_R4 ||
                            insts[i].OpCode.Code == Code.Ldc_R8)))
                        {
                            txts.Add(new Context()
                            {
                                mtd = mtd,
                                psr = psr,
                                str = insts[i],
                                a = (uint)Random.Next(),
                                conster = txt.consters[Random.Next(0, txt.consters.Length)]
                            });
                        }
                    }
                }
            }
            byte[] GetOperand(object operand)
            {
                byte[] ret;
                if (operand is double)
                    ret = BitConverter.GetBytes((double)operand);
                else if (operand is float)
                    ret = BitConverter.GetBytes((float)operand);
                else if (operand is int)
                    ret = BitConverter.GetBytes((int)operand);
                else if (operand is long)
                    ret = BitConverter.GetBytes((long)operand);
                else
                    ret = Encoding.UTF8.GetBytes((string)operand);
                return ret;
            }
            uint GetOperandLen(object operand)
            {
                if (operand is double) return 8;
                else if (operand is float) return 4;
                else if (operand is int) return 4;
                else if (operand is long) return 8;
                else return (uint)Encoding.UTF8.GetByteCount(operand as string);
            }
            bool IsEqual(byte[] a, byte[] b)
            {
                int l = Math.Min(a.Length, b.Length);
                for (int i = 0; i < l; i++)
                    if (a[i] != b[i]) return false;
                return true;
            }
            void FinalizeBodies(List<Context> txts)
            {
                double total = txts.Count;
                int interval = 1;
                if (total > 1000)
                    interval = (int)total / 100;

                //StringGenerator sg = new StringGenerator(Random.Next(500000), txts[0].mtd.Module);


                for (int i = 0; i < txts.Count; i++)
                {
                    int idx = txts[i].mtd.Body.Instructions.IndexOf(txts[i].str);
                    Instruction now = txts[i].str;
                    if (IsNull(now.Operand)) continue;

                    TypeReference typeRef;
                    if (now.Operand is int)
                        typeRef = txts[i].mtd.Module.TypeSystem.Int32;
                    else if (now.Operand is long)
                        typeRef = txts[i].mtd.Module.TypeSystem.Int64;
                    else if (now.Operand is float)
                        typeRef = txts[i].mtd.Module.TypeSystem.Single;
                    else if (now.Operand is double)
                        typeRef = txts[i].mtd.Module.TypeSystem.Double;
                    else
                        typeRef = txts[i].mtd.Module.TypeSystem.String;

                    Instruction call = Instruction.Create(OpCodes.Call, new GenericInstanceMethod(txts[i].conster.conster)
                    {
                        GenericArguments = { typeRef }
                    });
                    call.SequencePoint = now.SequencePoint;
                    
                    txts[i].psr.InsertAfter(idx, call);

                    MethodBody A_BODY = txts[i].mtd.Body;


                    if (ObfuscationHelper.StringGen != null)
                    {
                        txts[i].psr.InsertAfter(idx, Instruction.Create(OpCodes.Ldc_I8, (long)txts[i].b));

                        Expression ex = new ExpressionGenerator(Random.Next(5000000), mod).Generate(5);

                        int evald = ExpressionEvaluator.Evaluate(ex, (int)txts[i].a);

                        Expression exR = ExpressionInverser.InverseExpression(ex);

                        CecilVisitor cv = new CecilVisitor(exR, new Instruction[] { Instruction.Create(OpCodes.Ldc_I4, evald) });

                        List<Instruction> keyA = cv.GetInstructions().ToList();
                        //List<Instruction> keyA = ObfuscationHelper.StringGen.GenerateLevels(txts[i].mtd, (int)txts[i].a, Random.Next(0, 2), 3);


                        txts[i].mtd.Body.Instructions[idx].OpCode = keyA[0].OpCode;
                        txts[i].mtd.Body.Instructions[idx].Operand = keyA[0].Operand;

                        keyA = keyA.Skip(1).Reverse().ToList();
                        foreach (Instruction a in keyA)
                        {
                            A_BODY.Instructions.Insert(idx + 1, a);
                        }
                    }
                    else
                    {
                        txts[i].psr.Replace(idx, Instruction.Create(OpCodes.Ldc_I4, (int)txts[i].a));
                        txts[i].psr.InsertAfter(idx, Instruction.Create(OpCodes.Ldc_I8, (long)txts[i].b));
                    }


                    //txts[i].psr.Replace(idx, Instruction.Create(OpCodes.Ldc_I4, (int)txts[i].a));
                    //txts[i].psr.InsertAfter(idx, Instruction.Create(OpCodes.Ldc_I8, (long)txts[i].b));

                    // I4_0 == U4 Conv
                    // I4_1 == U8 conv
                    /*List<Instruction> genInsts = sg.MathGen.GenerateLevels((int)txts[i].a, OpCodes.Ldc_I4, Random.Next(0, 3));


                    txts[i].mtd.Body.Instructions[idx].OpCode = genInsts[0].OpCode;
                    txts[i].mtd.Body.Instructions[idx].Operand = genInsts[0].Operand;

                    genInsts = genInsts.Skip(1).Reverse().ToList();
                    foreach (Instruction a in genInsts)
                    {
                        txts[i].mtd.Body.Instructions.Insert(idx + 1, a);
                    }*/


                    //Instruction tmp = null;
                    //foreach (Instruction a in genInsts)
                    //{
                    //    if (genInsts[0] == a)
                    //    {
                    //        txts[i].psr.Replace(idx, a);
                    //        tmp = a;
                    //        continue;
                    //    }

                    //    txts[i].psr.InsertAfter(tmp, a);
                    //    tmp = a;
                    //}

                    //txts[i].psr.Replace(idx, Instruction.Create(OpCodes.Ldc_I4, (int)txts[i].a));
                    //txts[i].psr.InsertAfter(idx, Instruction.Create(OpCodes.Ldc_I8, (long)txts[i].b));

                    //txts[i].psr.InsertAfter(tmp, Instruction.Create(OpCodes.Ldc_I8, (long)txts[i].b));


                    /*genInsts = mg.GenerateLevels(txts[i].b, OpCodes.Ldc_I4_1, Random.Next(0, 0), false, false);

                    foreach (Instruction a in genInsts)
                    {
                        if (tmp == null)
                        {
                            txts[i].psr.InsertAfter(idx, a);
                            tmp = a;
                            continue;
                        }
                        txts[i].psr.InsertAfter(tmp, a);
                        tmp = a;
                    }*/

                    if (i % interval == 0 || i == txts.Count - 1)
                        progresser.SetProgress(i + 1, txts.Count);
                }

                List<int> hashs = new List<int>();
                for (int i = 0; i < txts.Count; i++)
                {
                    if (hashs.IndexOf(txts[i].mtd.GetHashCode()) == -1)
                    {
                        txts[i].mtd.Body.MaxStackSize += 2;
                        hashs.Add(txts[i].mtd.GetHashCode());
                    }
                }
            }

            public override void Process(ConfusionParameter parameter)
            {
                _Context txt = cc.txts[mod];

                foreach (var i in txt.consters)
                {
                    i.keyInst.OpCode = OpCodes.Ldc_I4;
                    i.keyInst.Operand = (int)(txt.key ^ i.conster.MetadataToken.ToUInt32());
                }

                List<Context> txts = new List<Context>();
                ExtractData(
                    parameter.Target as IList<Tuple<IAnnotationProvider, NameValueCollection>>, txts,
                    /*Array.IndexOf(parameter.GlobalParameters.AllKeys, "numeric") != -1*/ true, txt);

                txt.dict.Clear();

                for (int i = 0; i < txts.Count; i++)
                {
                    object val = txts[i].str.Operand as object;
                    if (IsNull(val)) continue;

                    uint x = txts[i].conster.conster.DeclaringType.MetadataToken.ToUInt32() * txts[i].a;
                    ulong hash = ComputeHash(x,
                                (uint)txts[i].conster.key3,
                                (ulong)txts[i].conster.key0,
                                (ulong)txts[i].conster.key1,
                                (ulong)txts[i].conster.key2 );
                    uint idx, len;
                    if (txt.dict.ContainsKey(val))
                        txts[i].b = Combine(idx = (uint)txt.dict[val], len = GetOperandLen(val)) ^ hash;
                    else
                    {
                        byte[] dat = GetOperand(val);
                        txts[i].b = Combine(idx = (uint)txt.idx, len = (uint)dat.Length) ^ hash;

                        txt.dats.Add(dat);
                        txt.dict[val] = txt.idx;

                        txt.idx += dat.Length;
                    }
                    Database.AddEntry("Const", val.ToString(), string.Format("{0:X}, {1:X}, {2:X}, {3:X}", txts[i].a, txts[i].b, idx, len));
                }

                FinalizeBodies(txts);
            }

            IProgresser progresser;
            public void SetProgresser(IProgresser progresser)
            {
                this.progresser = progresser;
            }
        }
        class MdPhase1 : MetadataPhase
        {
            public MdPhase1(ConstantConfusion cc) { this.cc = cc; }
            ConstantConfusion cc;
            public override IConfusion Confusion
            {
                get { return cc; }
            }

            public override int PhaseID
            {
                get { return 1; }
            }

            public override Priority Priority
            {
                get { return Priority.TypeLevel; }
            }

            public override void Process(NameValueCollection parameters, MetadataProcessor.MetadataAccessor accessor)
            {
                _Context txt = cc.txts[accessor.Module];

                // does standalone sig table because its able to use ResolveSignature(mdTok)
                /*int rid = accessor.TableHeap.GetTable<StandAloneSigTable>(Table.StandAloneSig).AddRow(
                    accessor.BlobHeap.GetBlobIndex(new Mono.Cecil.PE.ByteBuffer(txt.keyBuff)));*/
                uint blobIndex = accessor.BlobHeap.GetBlobIndex(new Mono.Cecil.PE.ByteBuffer(txt.keyBuff));

                //int rid = accessor.TableHeap.GetTable<FieldTable>(Table.Field).AddRow(new Row<FieldAttributes, uint, uint>(FieldAttributes.Public | FieldAttributes.Static, 0x7fffff, blobIndex));


                // update md body to not reference the field
               /* var mainModuleType = accessor.Module.Types[0];
                var ccTor = mainModuleType.Methods[0];
                var inst = ccTor.Body.Instructions[2];
                inst.Operand = mainModuleType.Fields[1];
                */
                    
                    /*accessor.TableHeap.GetTable<FileTable>(Table.File).AddRow(
                    new Row<Mono.Cecil.FileAttributes, uint, uint>(
                        Mono.Cecil.FileAttributes.ContainsMetaData, 
                    accessor.BlobHeap.GetBlobIndex(new Mono.Cecil.PE.ByteBuffer(txt.keyBuff)),
                    0));*/

                /*int token = 0x04000000 | rid;
                txt.keyInst.OpCode = OpCodes.Ldc_I4;
                // 06 = method, 04 = field
                txt.keyInst.Operand = token;//0x04000001;
                //int token =(int) txt.keyInst.Operand;
                txt.keyInst.Operand = (int)(token ^ 0x06000001);   //... -_-*/


                // TypeSpec == working
                /*int rid = accessor.TableHeap.GetTable<TypeSpecTable>(Table.TypeSpec).AddRow(blobIndex);

                int token = 0x1B000000 | rid;
                txt.keyInst.OpCode = OpCodes.Ldc_I4;
                //0x0601 == <Module>.cctor();
                txt.keyInst.Operand = (int)(token ^ 0x06000001);   //... -_-*/

                int rid = accessor.TableHeap.GetTable<MemberRefTable>(Table.MemberRef).AddRow(new Row<uint, uint, uint>(1, 1, blobIndex));

                int token = 0x0A000000 | rid;
                txt.keyInst.OpCode = OpCodes.Ldc_I4;
                //0x0601 == <Module>.cctor();
                txt.keyInst.Operand = (int)(token ^ 0x06000001);   //... -_-

                Database.AddEntry("Const", "KeyBuffToken", token);

                if (!txt.isNative) return;

                txt.nativeRange = new Range(accessor.Codebase + (uint)accessor.Codes.Position, 0);
                MemoryStream ms = new MemoryStream();
                using (BinaryWriter wtr = new BinaryWriter(ms))
                {
                    wtr.Write(new byte[] { 0x89, 0xe0 });   //   mov eax, esp
                    wtr.Write(new byte[] { 0x53 });   //   push ebx
                    wtr.Write(new byte[] { 0x57 });   //   push edi
                    wtr.Write(new byte[] { 0x56 });   //   push esi
                    wtr.Write(new byte[] { 0x29, 0xe0 });   //   sub eax, esp
                    wtr.Write(new byte[] { 0x83, 0xf8, 0x18 });   //   cmp eax, 24
                    wtr.Write(new byte[] { 0x74, 0x07 });   //   je n
                    wtr.Write(new byte[] { 0x8b, 0x44, 0x24, 0x10 });   //   mov eax, [esp + 4]
                    wtr.Write(new byte[] { 0x50 });   //   push eax
                    wtr.Write(new byte[] { 0xeb, 0x01 });   //   jmp z
                    wtr.Write(new byte[] { 0x51 });   //n: push ecx
                    x86Register ret;                                    //z: 
                    var insts = txt.visitor.GetInstructions(out ret);
                    foreach (var i in insts)
                        wtr.Write(i.Assemble());
                    if (ret != x86Register.EAX)
                        wtr.Write(
                            new x86Instruction()
                            {
                                OpCode = x86OpCode.MOV,
                                Operands = new Ix86Operand[]
                                {
                                    new x86RegisterOperand() { Register = x86Register.EAX },
                                    new x86RegisterOperand() { Register = ret }
                                }
                            }.Assemble());
                    wtr.Write(new byte[] { 0x5e });   //pop esi
                    wtr.Write(new byte[] { 0x5f });   //pop edi
                    wtr.Write(new byte[] { 0x5b });   //pop ebx
                    wtr.Write(new byte[] { 0xc3 });   //ret
                    wtr.Write(new byte[((ms.Length + 3) & ~3) - ms.Length]);
                }
                byte[] codes = ms.ToArray();
                Database.AddEntry("Const", "Native", codes);
                accessor.Codes.WriteBytes(codes);
                accessor.SetCodePosition(accessor.Codebase + (uint)accessor.Codes.Position);
                txt.nativeRange.Length = (uint)codes.Length;
            }
        }
        class MdPhase2 : MetadataPhase
        {
            public MdPhase2(ConstantConfusion cc) { this.cc = cc; }
            ConstantConfusion cc;
            public override IConfusion Confusion
            {
                get { return cc; }
            }

            public override int PhaseID
            {
                get { return 2; }
            }

            public override Priority Priority
            {
                get { return Priority.TypeLevel; }
            }

            public override void Process(NameValueCollection parameters, MetadataProcessor.MetadataAccessor accessor)
            {
                _Context txt = cc.txts[accessor.Module];

                // update typedef table to have field list for <module> start at 2 instead of 1
                /*var tbl1 = accessor.TableHeap.GetTable<TypeDefTable>(Table.TypeDef);

                for (int i = 0; i < tbl1.Length; i++)//foreach (var tblRow in tbl1)
                {
                    var moduleTypeRow = tbl1[i];
                    moduleTypeRow.Col5 = moduleTypeRow.Col5 + 1;
                    tbl1[i] = moduleTypeRow;
                }*/
               
                

                if (!txt.isNative) return;

                var tbl = accessor.TableHeap.GetTable<MethodTable>(Table.Method);
                var row = tbl[(int)txt.nativeDecr.MetadataToken.RID - 1];
                row.Col2 = MethodImplAttributes.Native | MethodImplAttributes.Unmanaged | MethodImplAttributes.PreserveSig;
                row.Col3 &= ~MethodAttributes.Abstract;
                row.Col3 |= MethodAttributes.PInvokeImpl;
                row.Col1 = txt.nativeRange.Start;
                accessor.BodyRanges[txt.nativeDecr.MetadataToken] = txt.nativeRange;

                tbl[(int)txt.nativeDecr.MetadataToken.RID - 1] = row;

                //accessor.Module.Attributes &= ~ModuleAttributes.ILOnly;

            }
        }


        struct Conster
        {
            public MethodDefinition conster;
            public long key0;
            public long key1;
            public long key2;
            public int key3;
            public Instruction keyInst;
        }
        class _Context
        {
            public List<byte[]> dats;
            public Dictionary<object, int> dict;
            public int idx = 0;
            public uint key;
            public byte[] keyBuff = new byte[32];

            public int resKey;
            public string resId;
            public Conster[] consters;
            public MethodDefinition nativeDecr;

            public Instruction keyInst;

            public bool isDyn;
            public bool isNative;
            public Expression exp;
            public Expression invExp;
            public x86Visitor visitor;
            public Range nativeRange;
        }
        Dictionary<ModuleDefinition, _Context> txts = new Dictionary<ModuleDefinition, _Context>();

        public string ID
        {
            get { return "const encrypt"; }
        }
        public string Name
        {
            get { return "Constants Confusion"; }
        }
        public string Description
        {
            get { return "This confusion obfuscate the constants in the code and store them in a encrypted and compressed form."; }
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
                    ps = new Phase[] { new Phase1(this), new Phase3(this), new MdPhase1(this), new MdPhase2(this) };
                return ps;
            }
        }

        public void Init() { txts.Clear(); }
        public void Deinit() { txts.Clear(); }

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

        private static byte[] Encrypt(byte[] bytes, Expression exp)
        {
            MemoryStream ret = new MemoryStream();
            using (BinaryWriter wtr = new BinaryWriter(ret))
            {
                for (int i = 0; i < bytes.Length; i++)
                {
                    int en = (int)ExpressionEvaluator.Evaluate(exp, bytes[i]);
                    Write7BitEncodedInt(wtr, en);
                }
            }

            return ret.ToArray();
        }
        private static byte[] EncryptSafe(byte[] bytes, uint key)
        {
            Random rng = new Random((int)key);

            byte[] rBytes = new byte[bytes.Length];

            for (int i = 0; i < bytes.Length; i++)
            {
                rBytes[i] = (byte)((byte)bytes[i] ^ (byte)rng.Next(256));
            }

            return rBytes;

            /*ushort _m = (ushort)(key >> 16);
            ushort _c = (ushort)(key & 0xffff);
            ushort m = _c; ushort c = _m;
            byte[] ret = (byte[])bytes.Clone();
            for (int i = 0; i < ret.Length; i++)
            {
                ret[i] ^= (byte)((key * m + c) % 0x100);
                m = (ushort)((key * m + _m) % 0x10000);
                c = (ushort)((key * c + _c) % 0x10000);
            }
            return ret;*/
        }
        private static byte[] XorCrypt(byte[] bytes, uint key)
        {
            byte[] ret = new byte[bytes.Length];
            byte[] keyBuff = BitConverter.GetBytes(key);
            for (int i = 0; i < ret.Length; i++)
                ret[i] = (byte)(bytes[i] ^ keyBuff[i % 4]);
            return ret;
        }

        static ulong Combine(uint high, uint low)
        {
            return (((ulong)high) << 32) | (ulong)low;
        }
        static ulong ComputeHash(uint x, uint key, ulong init0, ulong init1, ulong init2)
        {
            ulong h = init0 * x;
            ulong h1 = init1;
            ulong h2 = init2;
            h1 = h1 * h;
            h2 = h2 * h;
            h = h * h;

            ulong hash = 0xCBF29CE484222325;
            while (h != 0)
            {
                hash *= 0x100000001B3;
                hash = (hash ^ h) + (h1 ^ h2) * key;
                h1 *= 0x811C9DC5;
                h2 *= 0xA2CEBAB2;
                h >>= 8;
            }
            return hash;
        }
    }
}
