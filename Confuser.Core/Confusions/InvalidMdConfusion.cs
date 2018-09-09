using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Metadata;
using System.Text;
using System.Collections.Specialized;
using System.IO;
using Mono.Cecil.PE;

namespace Confuser.Core.Confusions
{
    public class InvalidMdConfusion : IConfusion
    {
        class Phase1 : MetadataPhase
        {
            InvalidMdConfusion cion;
            public Phase1(InvalidMdConfusion cion) { this.cion = cion; }
            public override Priority Priority
            {
                get { return Priority.MetadataLevel; }
            }
            public override IConfusion Confusion
            {
                get { return cion; }
            }
            public override int PhaseID
            {
                get { return 1; }
            }


            public override void Process(NameValueCollection parameters, MetadataProcessor.MetadataAccessor accessor)
            {
                accessor.TableHeap.GetTable<DeclSecurityTable>(Table.DeclSecurity).AddRow(new Row<SecurityAction, uint, uint>((SecurityAction)0xffff, 0xffffffff, 0xffffffff));
                if (Array.IndexOf(parameters.AllKeys, "hasreflection") == -1)
                {
                    char[] pad = new char[0x10000];
                    int len = 0;
                    while (accessor.StringHeap.Length + len < 0x10000)
                    {
                        for (int i = 0; i < 0x1000; i++)
                            while ((pad[len + i] = (char)Random.Next(0, 0x100)) == '\0') ;
                        len += 0x1000;
                    }
                    uint idx = accessor.StringHeap.GetStringIndex(new string(pad, 0, len));
                    accessor.TableHeap.GetTable<ManifestResourceTable>(Table.ManifestResource).AddRow(new Row<uint, ManifestResourceAttributes, uint, uint>(0xffffffff, ManifestResourceAttributes.Private, idx, 2));
                }
            }
        }
        class Phase2 : MetadataPhase
        {
            InvalidMdConfusion cion;
            public Phase2(InvalidMdConfusion cion) { this.cion = cion; }
            public override Priority Priority
            {
                get { return Priority.MetadataLevel; }
            }
            public override IConfusion Confusion
            {
                get { return cion; }
            }
            public override int PhaseID
            {
                get { return 2; }
            }

            void Randomize<T>(MetadataTable<T> tbl)
            {
                T[] t = tbl.OfType<T>().ToArray();
                for (int i = 0; i < t.Length; i++)
                {
                    T tmp = t[i];
                    int j = Random.Next(0, t.Length);
                    t[i] = t[j];
                    t[j] = tmp;
                }
                tbl.Clear();
                foreach (var i in t)
                    tbl.AddRow(i);
            }

            public override void Process(NameValueCollection parameters, MetadataProcessor.MetadataAccessor accessor)
            {
                accessor.TableHeap.GetTable<TypeDefTable>(Table.TypeDef)[0].Col2 = 0xffff;
                uint mtdLen = (uint)accessor.TableHeap.GetTable<MethodTable>(Table.Method).Length + 1;
                uint fldLen = (uint)accessor.TableHeap.GetTable<FieldTable>(Table.Field).Length + 1;
                Database.AddEntry("InvalidMd", "HasReflection", Array.IndexOf(parameters.AllKeys, "hasreflection") != -1);
                Database.AddEntry("InvalidMd", "Runtime", accessor.Module.Runtime);
                if (Array.IndexOf(parameters.AllKeys, "hasreflection") == -1)
                {
                    if (accessor.Module.Runtime != TargetRuntime.Net_4_0)
                    {
                        List<uint> nss = new List<uint>();
                        foreach (Row<TypeAttributes, uint, uint, uint, uint, uint> i in accessor.TableHeap.GetTable<TypeDefTable>(Table.TypeDef))
                            if (i == null) break; else if (!nss.Contains(i.Col3)) nss.Add(i.Col3);
                        uint nested = (uint)accessor.TableHeap.GetTable<TypeDefTable>(Table.TypeDef).AddRow(new Row<TypeAttributes, uint, uint, uint, uint, uint>(0, 0x7fffffff, 0, 0x3FFFD, fldLen, mtdLen));
                        accessor.TableHeap.GetTable<NestedClassTable>(Table.NestedClass).AddRow(new Row<uint, uint>(nested, nested));
                        foreach (uint i in nss)
                        {
                            uint type = (uint)accessor.TableHeap.GetTable<TypeDefTable>(Table.TypeDef).AddRow(new Row<TypeAttributes, uint, uint, uint, uint, uint>(0, 0x7fffffff, i, 0x3FFFD, fldLen, mtdLen));
                            accessor.TableHeap.GetTable<NestedClassTable>(Table.NestedClass).AddRow(new Row<uint, uint>(nested, type));
                        }
                        foreach (Row<ParameterAttributes, ushort, uint> r in accessor.TableHeap.GetTable<ParamTable>(Table.Param))
                            if (r != null)
                                r.Col3 = 0x7fffffff;
                    }
                }
                accessor.TableHeap.GetTable<ModuleTable>(Table.Module).AddRow(accessor.StringHeap.GetStringIndex(ObfuscationHelper.GetRandomName()));

                accessor.TableHeap.GetTable<AssemblyTable>(Table.Assembly).AddRow(new Row<AssemblyHashAlgorithm, ushort, ushort, ushort, ushort, AssemblyAttributes, uint, uint, uint>(
                    AssemblyHashAlgorithm.None, 0, 0, 0, 0, AssemblyAttributes.SideBySideCompatible, 0,
                    accessor.StringHeap.GetStringIndex(ObfuscationHelper.GetRandomName()), 0));

                for (int i = 0; i < 10; i++)
                    accessor.TableHeap.GetTable<ENCLogTable>(Table.EncLog).AddRow(new Row<uint, uint>((uint)Random.Next(), (uint)Random.Next()));
                for (int i = 0; i < 10; i++)
                    accessor.TableHeap.GetTable<ENCMapTable>(Table.EncMap).AddRow((uint)Random.Next());

                accessor.TableHeap.GetTable<AssemblyRefTable>(Table.AssemblyRef).AddRow(new Row<ushort, ushort, ushort, ushort, AssemblyAttributes, uint, uint, uint, uint>(
                    0, 0, 0, 0, AssemblyAttributes.SideBySideCompatible, 0,
                    0xffff, 0, 0xffff));


                Randomize(accessor.TableHeap.GetTable<NestedClassTable>(Table.NestedClass));
                Randomize(accessor.TableHeap.GetTable<ManifestResourceTable>(Table.ManifestResource));
                Randomize(accessor.TableHeap.GetTable<GenericParamConstraintTable>(Table.GenericParamConstraint));
            }
        }
        //class Phase3 : ImagePhase
        //{
        //    InvalidMdConfusion cion;
        //    public Phase3(InvalidMdConfusion cion) { this.cion = cion; }
        //    public override Priority Priority
        //    {
        //        get { return Priority.PELevel; }
        //    }
        //    public override IConfusion Confusion
        //    {
        //        get { return cion; }
        //    }
        //    public override int PhaseID
        //    {
        //        get { return 2; }
        //    }

        //    public override void Process(NameValueCollection parameters, MetadataProcessor.ImageAccessor accessor)
        //    {
        //        //Section text = accessor.Sections[0];

        //        //Random rand = new Random();
        //        //int newSectCount = rand.Next(2, 4);

        //        //for (int i = 0; i < newSectCount && text.VirtualSize > 0x2000; i++)
        //        //{
        //        //    uint size = 0;
        //        //    if (text.VirtualSize < 0x4000)
        //        //    {
        //        //        size = text.VirtualSize - 0x2000;
        //        //    }
        //        //    accessor.ResizeSection(text, text.VirtualSize - size, false);

        //        //    int insertIndex = 1;
        //        //    Section newSect = accessor.CreateSection(
        //        //        Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Substring(0, 6),
        //        //        size,
        //        //        text.Characteristics,
        //        //        null);
        //        //    newSect.VirtualAddress = text.VirtualAddress + text.VirtualSize;
        //        //    newSect.PointerToRawData =
        //        //        accessor.Sections[insertIndex - 1].PointerToRawData +
        //        //        accessor.Sections[insertIndex - 1].SizeOfRawData;
        //        //    accessor.Sections.Insert(insertIndex, newSect);
        //        //    for (int j = insertIndex + 1; j < accessor.Sections.Count; j++)
        //        //        accessor.Sections[j].PointerToRawData =
        //        //            accessor.Sections[j - 1].PointerToRawData +
        //        //            accessor.Sections[j - 1].SizeOfRawData;
        //        //}
        //    }
        //}
        class Phase4 : PePhase
        {
            InvalidMdConfusion cion;
            public Phase4(InvalidMdConfusion cion) { this.cion = cion; }
            public override Priority Priority
            {
                get { return Priority.PELevel; }
            }
            public override IConfusion Confusion
            {
                get { return cion; }
            }
            public override int PhaseID
            {
                get { return 2; }
            }

            public override void Process(NameValueCollection parameters, Stream stream, MetadataProcessor.ImageAccessor accessor)
            {
                Random rand = new Random();
                BinaryReader rdr = new BinaryReader(stream);

                stream.Seek(0x3c, SeekOrigin.Begin);
                uint offset = rdr.ReadUInt32();
                stream.Seek(offset, SeekOrigin.Begin);
                stream.Seek(0x6, SeekOrigin.Current);
                uint sections = rdr.ReadUInt16();
                stream.Seek(offset = offset + 0x18, SeekOrigin.Begin);  //Optional hdr
                bool pe32 = (rdr.ReadUInt16() == 0x010b);
                stream.Seek(offset = offset + (pe32 ? 0xE0U : 0xF0U), SeekOrigin.Begin);   //sections
                stream.Seek(-0xc, SeekOrigin.Current);
                for (int i = 0; i < sections; i++)
                {
                    bool seen = false;
                    for (int j = 0; j < 8; j++)
                    {
                        byte b = rdr.ReadByte();
                        if (b == 0 & !seen)
                        {
                            seen = true;
                            stream.Seek(-1, SeekOrigin.Current);
                            stream.WriteByte(0x20);
                        }
                    }
                    uint vSize = rdr.ReadUInt32();
                    uint vLoc = rdr.ReadUInt32();
                    uint rSize = rdr.ReadUInt32();
                    uint rLoc = rdr.ReadUInt32();
                    stream.Seek(0x10, SeekOrigin.Current);
                }

                var mdPtr = accessor.ResolveVirtualAddress(accessor.GetTextSegmentRange(TextSegment.MetadataHeader).Start);
                stream.Position = mdPtr + 12;
                long verLenPos = stream.Position;
                uint verLen = rdr.ReadUInt32();
                stream.Position += verLen;
                stream.Position += 2;

                ushort streams = rdr.ReadUInt16();

                uint startOfStreams = 0xffffffff;
                for (int i = 0; i < streams; i++)
                {
                    startOfStreams = Math.Min(rdr.ReadUInt32(), startOfStreams);
                    stream.Position += 4;
                    long begin = stream.Position;

                    int c = 0;
                    string s = "";
                    byte b;
                    while ((b = rdr.ReadByte()) != 0)
                    {
                        s += (char)b;
                        c++;
                    }
                    if (s == "#~")
                    {
                        stream.Position = begin + 1;
                        stream.WriteByte((byte)'-');
                    }
                    stream.Position = (stream.Position + 3) & ~3;
                }

                uint m = startOfStreams - (uint)(stream.Position - mdPtr);
                uint size = (uint)(stream.Position - (verLenPos + 4));
                stream.Position = verLenPos;
                stream.Write(BitConverter.GetBytes(verLen + m), 0, 4);
                byte[] x = new byte[verLen];
                stream.Read(x, 0, (int)verLen);
                byte[] t = new byte[size - verLen];
                stream.Read(t, 0, (int)t.Length);
                stream.Position = verLenPos + 4;
                stream.Write(x, 0, x.Length);
                stream.Write(new byte[m], 0, (int)m);
                stream.Write(t, 0, t.Length);
            }
        }

        public string Name
        {
            get { return "Invalid Metadata Confusion"; }
        }
        public string Description
        {
            get { return "This confusion adds invalid metadata into assembly to prevent disassembler/decompiler to open the assembly."; }
        }
        public string ID
        {
            get { return "invalid md"; }
        }
        public bool StandardCompatible
        {
            get { return false; }
        }
        public Target Target
        {
            get { return Target.Module; }
        }
        public Preset Preset
        {
            get { return Preset.Maximum; }
        }
        public bool SupportLateAddition
        {
            get { return true; }
        }
        public Behaviour Behaviour
        {
            get { return Behaviour.AlterStructure; }
        }

        Phase[] phases;
        public Phase[] Phases
        {
            get
            {
                if (phases == null) phases = new Phase[] { new Phase1(this), new Phase2(this), new Phase4(this) };
                return phases;
            }
        }

        public void Init() { }
        public void Deinit() { }
    }
}
