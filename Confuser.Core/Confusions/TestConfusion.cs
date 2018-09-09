using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using System.Collections.Specialized;
using Mono.Cecil.Metadata;
using Mono.Cecil.PE;
using System.IO;

namespace Confuser.Core.Confusions
{
#if TESTING
    class TestConfusion : IConfusion
    {
        class Phase1 : StructurePhase, IProgressProvider
        {
            TestConfusion c;
            public Phase1(TestConfusion c) { this.c = c; }
            public override IConfusion Confusion
            {
                get { return c; }
            }

            public override int PhaseID
            {
                get { return 1; }
            }

            public override Priority Priority
            {
                get { return Priority.TypeLevel; }
            }

            public override bool WholeRun
            {
                get { return true; }
            }

            ModuleDefinition mod;
            public override void Initialize(ModuleDefinition mod)
            {
                this.mod = mod;
            }
            public override void DeInitialize()
            {
            }

            public override void Process(ConfusionParameter parameter)
            {
                MethodDefinition t = null;
                foreach (var i in mod.GetType("Confuser.Test.Program").Methods)
                    if (i.Name == "T")
                    {
                        t = i;
                        break;
                    }

                t.IsStatic = true;
                t.HasThis = false;

                t.DeclaringType.Methods.Remove(t);
                t.DeclaringType = null;
                mod.GetType("<Module>").Methods.Add(t);
            }

            IProgresser progresser;
            void IProgressProvider.SetProgresser(IProgresser progresser)
            {
                this.progresser = progresser;
            }
        }
        class Phase2 : StructurePhase, IProgressProvider
        {
            TestConfusion c;
            public Phase2(TestConfusion c) { this.c = c; }
            public override IConfusion Confusion
            {
                get { return c; }
            }

            public override int PhaseID
            {
                get { return 2; }
            }

            public override Priority Priority
            {
                get { return Priority.TypeLevel; }
            }

            public override bool WholeRun
            {
                get { return true; }
            }

            ModuleDefinition mod;
            public override void Initialize(ModuleDefinition mod)
            {
                this.mod = mod;
            }
            public override void DeInitialize()
            {
            }

            public override void Process(ConfusionParameter parameter)
            {
                MethodDefinition t = null;
                MethodDefinition x = null;
                foreach (var i in mod.GetType("<Module>").Methods)
                    if (i.Name == "T")
                        t = i;
                foreach (var i in mod.GetType("Confuser.Test.Program").Methods)
                    if (i.IsConstructor)
                        x = i;

                t.IsAbstract = true;
                t.IsPublic = true;
                c.rid = (int)t.MetadataToken.RID;
                c.xrid = (int)x.MetadataToken.RID;
            }

            IProgresser progresser;
            void IProgressProvider.SetProgresser(IProgresser progresser)
            {
                this.progresser = progresser;
            }
        }
        class MdPhase : MetadataPhase
        {
            TestConfusion c;
            public MdPhase(TestConfusion c) { this.c = c; }
            public override IConfusion Confusion
            {
                get { return c; }
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
                var tbl = accessor.TableHeap.GetTable<MethodTable>(Table.Method);
                var row = tbl[c.rid - 1];
                row.Col2 = MethodImplAttributes.Native | MethodImplAttributes.Unmanaged | MethodImplAttributes.PreserveSig;
                row.Col3 &= ~MethodAttributes.Abstract;
                row.Col3 |= MethodAttributes.PInvokeImpl;

                accessor.Codes.Position = (accessor.Codes.Length + 15) & ~15;
                row.Col1 = c.codeRva = accessor.Codebase + (uint)accessor.Codes.Position;
                accessor.Codes.WriteBytes(new byte[] { 0xff, 0x25, 0x12, 0x34, 0x56, 0x78 });

                tbl[c.rid - 1] = row;
            }
        }
        class ImgPhase : ImagePhase
        {
            TestConfusion c;
            public ImgPhase(TestConfusion c) { this.c = c; }
            public override IConfusion Confusion
            {
                get { return c; }
            }

            public override int PhaseID
            {
                get { return 1; }
            }

            public override Priority Priority
            {
                get { return Priority.TypeLevel; }
            }

            public override void Process(NameValueCollection parameters, MetadataProcessor.ImageAccessor accessor)
            {
                accessor.Module.Attributes &= ~ModuleAttributes.ILOnly;
                MemoryStream ms = new MemoryStream();
                BinaryWriter wtr = new BinaryWriter(ms);
                {
                    //VTable
                    wtr.Write(0x06000000 | (uint)c.xrid);
                    //fixup table
                    wtr.Write(0x12345678U);   //rva
                    wtr.Write((ushort)1);                   //size
                    wtr.Write((ushort)1);                   //type
                    wtr.Write(new byte[0x200]);
                }
                Section sect = accessor.CreateSection("XD", 4 + 4 + 2 + 2, 0xC0000040, accessor.Sections[accessor.Sections.Count - 1]);
                accessor.Sections.Add(sect);
                ms.Position = 4;
                ms.Write(BitConverter.GetBytes(sect.VirtualAddress), 0, 4);
                sect.Data = ms.ToArray();
                c.vTblRva = sect.VirtualAddress + 4;
            }
        }
        class PEPhase : PePhase
        {
            TestConfusion c;
            public PEPhase(TestConfusion c) { this.c = c; }
            public override IConfusion Confusion
            {
                get { return c; }
            }

            public override int PhaseID
            {
                get { return 1; }
            }

            public override Priority Priority
            {
                get { return Priority.TypeLevel; }
            }

            public override void Process(NameValueCollection parameters, Stream stream, ModuleDefinition mod)
            {
                stream.Position = 0;
                var img = ImageReader.ReadImageFrom(stream);
                uint codePos = img.ResolveVirtualAddress(c.codeRva);
                stream.Position = codePos + 2;
                stream.Write(BitConverter.GetBytes(c.vTblRva - 4 + 0x00400000), 0, 4);
                stream.Position = img.ResolveVirtualAddress(img.CLIHeader.VirtualAddress) + 0x30;
                stream.Write(BitConverter.GetBytes(c.vTblRva), 0, 4);
                stream.Write(BitConverter.GetBytes(0x8U), 0, 4);
            }
        }


        public string ID
        {
            get { return "_________________________"; }
        }
        public string Name
        {
            get { return "Test Confusion"; }
        }
        public string Description
        {
            get { return "Nothing."; }
        }
        public Target Target
        {
            get { return Target.Methods; }
        }
        public Preset Preset
        {
            get { return Preset.None; }
        }
        public bool StandardCompatible
        {
            get { return false; }
        }
        public bool SupportLateAddition
        {
            get { return false; }
        }
        public Behaviour Behaviour
        {
            get { return Behaviour.Inject | Behaviour.AlterCode | Behaviour.AlterStructure; }
        }

        int rid = 0;
        int xrid = 0;
        uint codeRva = 0;
        uint vTblRva = 0;

        Phase[] ps;
        public Phase[] Phases
        {
            get
            {
                if (ps == null) ps = new Phase[] { new Phase1(this), new Phase2(this), new MdPhase(this), new ImgPhase(this), new PEPhase(this) };
                return ps;
            }
        }

        public void Init() { }
        public void Deinit() { }
    }
#endif
}
