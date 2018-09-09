using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.IO.Compression;
using System.Collections.Specialized;

namespace Confuser.Core.Confusions
{
    public class ResEncryptConfusion : IConfusion
    {
        class Phase1 : StructurePhase
        {
            public Phase1(ResEncryptConfusion rc) { this.rc = rc; }
            ResEncryptConfusion rc;
            public override IConfusion Confusion
            {
                get { return rc; }
            }
            public override int PhaseID
            {
                get { return 1; }
            }
            public override Priority Priority
            {
                get { return Priority.AssemblyLevel; }
            }
            public override bool WholeRun
            {
                get { return true; }
            }

            ModuleDefinition mod;
            public override void Initialize(ModuleDefinition mod)
            {
                this.mod = mod;
                rc.txts[mod] = new _Context();
            }
            public override void DeInitialize()
            {
                //
            }
            public override void Process(ConfusionParameter parameter)
            {
                _Context txt = rc.txts[mod];
                txt.dats = new List<KeyValuePair<string, byte[]>>();

                TypeDefinition modType = mod.GetType("<Module>");

                AssemblyDefinition i = AssemblyDefinition.ReadAssembly(typeof(Iid).Assembly.Location);
                i.MainModule.ReadSymbols();
                txt.reso = i.MainModule.GetType("Encryptions").Methods.FirstOrDefault(mtd => mtd.Name == "Resources");
                txt.reso = CecilHelper.Inject(mod, txt.reso);
                modType.Methods.Add(txt.reso);
                txt.reso.Name = ObfuscationHelper.GetRandomName();
                txt.reso.IsAssembly = true;
                AddHelper(txt.reso, HelperAttribute.NoInjection);
                Database.AddEntry("ResEncrypt", "Resolver", txt.reso.FullName);

                TypeDefinition lzma = mod.GetType("Lzma" + mod.GetHashCode());
                if (lzma == null)
                {
                    lzma = CecilHelper.Inject(mod, i.MainModule.GetType("Lzma"));
                    lzma.IsNotPublic = true;
                    lzma.Name = "Lzma" + mod.GetHashCode();
                    mod.Types.Add(lzma);
                }

                FieldDefinition datAsm = new FieldDefinition(
                    ObfuscationHelper.GetRandomName(),
                    FieldAttributes.Static | FieldAttributes.CompilerControlled,
                    mod.Import(typeof(System.Reflection.Assembly)));
                modType.Fields.Add(datAsm);
                AddHelper(datAsm, HelperAttribute.NoInjection);
                Database.AddEntry("ResEncrypt", "Store", datAsm.FullName);

                txt.key0 = (byte)Random.Next(0, 0x100);
                do
                {
                    txt.key1 = (byte)Random.Next(1, 0x100);
                } while (txt.key1 == txt.key0);
                Database.AddEntry("ResEncrypt", "Key0", txt.key0);
                Database.AddEntry("ResEncrypt", "Key1", txt.key1);

                txt.resId = ObfuscationHelper.GetRandomName();
                Database.AddEntry("ResEncrypt", "ResID", txt.resId);

                Mutator mutator = new Mutator();
                mutator.StringKeys = new string[] { txt.resId };
                mutator.IntKeys = new int[] { txt.key0, txt.key1 };
                mutator.Mutate(Random, txt.reso.Body, mod);
                foreach (Instruction inst in txt.reso.Body.Instructions)
                {
                    if (inst.Operand is FieldReference && (inst.Operand as FieldReference).Name == "datAsm")
                        inst.Operand = datAsm;
                    else if (inst.Operand is TypeReference && (inst.Operand as TypeReference).FullName == "System.Exception")
                        inst.Operand = modType;
                    else if (inst.Operand is MethodReference &&
                            (inst.Operand as MethodReference).DeclaringType.Name == "LzmaDecoder")
                        inst.Operand = lzma.NestedTypes
                            .Single(_ => _.Name == "LzmaDecoder").Methods
                            .Single(_ => _.Name == (inst.Operand as MethodReference).Name);
                }
                foreach (var x in txt.reso.Body.Variables)
                    if (x.VariableType.Name == "LzmaDecoder")
                        x.VariableType = lzma.NestedTypes.Single(_ => _.Name == "LzmaDecoder");

                MethodDefinition cctor = mod.GetType("<Module>").GetStaticConstructor();
                MethodBody bdy = cctor.Body as MethodBody;
                ILProcessor psr = bdy.GetILProcessor();
                //Reverse order
                psr.InsertBefore(0, Instruction.Create(OpCodes.Callvirt, mod.Import(typeof(AppDomain).GetEvent("ResourceResolve").GetAddMethod())));
                psr.InsertBefore(0, Instruction.Create(OpCodes.Newobj, mod.Import(typeof(ResolveEventHandler).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }))));
                psr.InsertBefore(0, Instruction.Create(OpCodes.Ldftn, txt.reso));
                psr.InsertBefore(0, Instruction.Create(OpCodes.Ldnull));
                psr.InsertBefore(0, Instruction.Create(OpCodes.Call, mod.Import(typeof(AppDomain).GetProperty("CurrentDomain").GetGetMethod())));
            }
        }
        class MdPhase : MetadataPhase
        {
            public MdPhase(ResEncryptConfusion rc) { this.rc = rc; }
            ResEncryptConfusion rc;
            public override IConfusion Confusion
            {
                get { return rc; }
            }
            public override int PhaseID
            {
                get { return 1; }
            }
            public override Priority Priority
            {
                get { return Priority.MetadataLevel; }
            }
            public override void Process(NameValueCollection parameters, MetadataProcessor.MetadataAccessor accessor)
            {
                _Context txt = rc.txts[accessor.Module];

                ModuleDefinition mod = accessor.Module;
                for (int i = 0; i < mod.Resources.Count; i++)
                    if (mod.Resources[i] is EmbeddedResource)
                    {
                        txt.dats.Add(new KeyValuePair<string, byte[]>(mod.Resources[i].Name, (mod.Resources[i] as EmbeddedResource).GetResourceData()));
                        mod.Resources.RemoveAt(i);
                        i--;
                    }

                if (txt.dats.Count > 0)
                {
                    MemoryStream ms = new MemoryStream();
                    BinaryWriter wtr = new BinaryWriter(ms);

                    byte[] dat = GetAsm(mod.TimeStamp, txt.dats);
                    wtr.Write(dat.Length);
                    wtr.Write(dat);

                    ms.Position = 0;


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
                    fileSize = ms.Length;
                    for (int i = 0; i < 8; i++)
                        x.WriteByte((Byte)(fileSize >> (8 * i)));
                    ms.Position = 0;
                    encoder.Code(ms, x, -1, -1, null);

                    dat = Transform(x.ToArray(), txt.key0, txt.key1);

                    mod.Resources.Add(new EmbeddedResource(txt.resId, ManifestResourceAttributes.Private, dat));
                }
            }
            byte[] GetAsm(uint timestamp, List<KeyValuePair<string, byte[]>> dats)
            {
                AssemblyDefinition asm = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition(ObfuscationHelper.GetRandomName(), new Version()), ObfuscationHelper.GetRandomName(), ModuleKind.Dll);
                foreach (KeyValuePair<string, byte[]> i in dats)
                    asm.MainModule.Resources.Add(new EmbeddedResource(i.Key, ManifestResourceAttributes.Public, i.Value));
                asm.MainModule.TimeStamp = timestamp;
                byte[] mvid = new byte[0x10];
                Random.NextBytes(mvid);
                asm.MainModule.Mvid = new Guid(mvid);
                MemoryStream ms = new MemoryStream();
                asm.Write(ms);
                return ms.ToArray();
            }
        }

        Phase[] phases;
        public Phase[] Phases
        {
            get
            {
                if (phases == null) phases = new Phase[] { new Phase1(this), new MdPhase(this) };
                return phases;
            }
        }
        public string ID
        {
            get { return "res encrypt"; }
        }
        public string Name
        {
            get { return "Resource Encryption Confusion"; }
        }
        public string Description
        {
            get { return "This will encrypt the embededd resources in the assembly and dynamically decrypt in runtime."; }
        }
        public Target Target
        {
            get { return Target.Module; }
        }
        public Preset Preset
        {
            get { return Preset.Normal; }
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

        public void Init() { txts.Clear(); }
        public void Deinit() { txts.Clear(); }

        class _Context
        {
            public List<KeyValuePair<string, byte[]>> dats;

            public string resId;
            public byte key0;
            public byte key1;
            public MethodDefinition reso;
        }
        Dictionary<ModuleDefinition, _Context> txts = new Dictionary<ModuleDefinition, _Context>();

        static byte[] Transform(byte[] res, byte key0, byte key1)
        {
            byte[] ret = new byte[res.Length];
            byte k = key0;
            for (int i = 0; i < res.Length; i++)
            {
                ret[i] = (byte)(res[i] ^ k + 13); //CHANGED DATA
                k = (byte)((k * key1) % 0x100);
            }
            return ret;
        }
    }
}
