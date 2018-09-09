using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.PE;
using Confuser.Core.Project;

namespace Confuser.Core
{
    public class PackerParameter
    {
        public AssemblySetting[] Assemblies { get; internal set; }
        public ModuleDefinition[] Modules { get; internal set; }
        public byte[][] PEs { get; internal set; }
        public NameValueCollection Parameters { get; internal set; }
    }

    public abstract class Packer
    {
        public abstract string ID { get; }
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract bool StandardCompatible { get; }
        Confuser cr;
        internal Confuser Confuser { get { return cr; } set { cr = value; } }
        protected void Log(string message) { cr.Log(message); }

        protected ObfuscationHelper ObfuscationHelper { get { return cr.ObfuscationHelper; } }
        protected Random Random { get { return cr.Random; } }
        protected ObfuscationDatabase Database { get { return cr.Database; } }

        internal protected virtual void ProcessModulePhase1(ModuleDefinition mod, bool isMain) { }
        internal protected virtual void ProcessModulePhase3(ModuleDefinition mod, bool isMain) { }
        internal protected virtual void ProcessMetadataPhase1(MetadataProcessor.MetadataAccessor accessor, bool isMain) { }
        internal protected virtual void ProcessMetadataPhase2(MetadataProcessor.MetadataAccessor accessor, bool isMain) { }
        internal protected virtual void ProcessImage(MetadataProcessor.ImageAccessor accessor, bool isMain) { }
        internal protected virtual void PostProcessMetadata(MetadataProcessor.MetadataAccessor accessor) { }
        internal protected virtual void PostProcessImage(MetadataProcessor.ImageAccessor accessor) { }

        protected string[] ProtectStub(AssemblyDefinition asm)
        {
            string tmp = Path.GetTempPath() + "\\" + Path.GetRandomFileName() + "\\";
            Directory.CreateDirectory(tmp);
            ModuleDefinition modDef = this.cr.settings.Single(_ => _.IsMain).Assembly.MainModule;
            asm.MainModule.TimeStamp = modDef.TimeStamp;
            byte[] mvid = new byte[0x10];
            Random.NextBytes(mvid);
            asm.MainModule.Mvid = new Guid(mvid);
            MetadataProcessor psr = new MetadataProcessor();
            Section oldRsrc = null;
            foreach (Section s in modDef.GetSections())
                if (s.Name == ".rsrc") { oldRsrc = s; break; }
            if (oldRsrc != null)
            {
                psr.ProcessImage += accessor =>
                {
                    Section sect = null;
                    foreach (Section s in accessor.Sections)
                        if (s.Name == ".rsrc") { sect = s; break; }
                    if (sect == null)
                    {
                        sect = new Section()
                        {
                            Name = ".rsrc",
                            Characteristics = 0x40000040
                        };
                        foreach (Section s in accessor.Sections)
                            if (s.Name == ".text") { accessor.Sections.Insert(accessor.Sections.IndexOf(s) + 1, sect); break; }
                    }
                    sect.VirtualSize = oldRsrc.VirtualSize;
                    sect.SizeOfRawData = oldRsrc.SizeOfRawData;
                    int idx = accessor.Sections.IndexOf(sect);
                    sect.VirtualAddress = accessor.Sections[idx - 1].VirtualAddress + ((accessor.Sections[idx - 1].VirtualSize + 0x2000U - 1) & ~(0x2000U - 1));
                    sect.PointerToRawData = accessor.Sections[idx - 1].PointerToRawData + accessor.Sections[idx - 1].SizeOfRawData;
                    for (int i = idx + 1; i < accessor.Sections.Count; i++)
                    {
                        accessor.Sections[i].VirtualAddress = accessor.Sections[i - 1].VirtualAddress + ((accessor.Sections[i - 1].VirtualSize + 0x2000U - 1) & ~(0x2000U - 1));
                        accessor.Sections[i].PointerToRawData = accessor.Sections[i - 1].PointerToRawData + accessor.Sections[i - 1].SizeOfRawData;
                    }
                    ByteBuffer buff = new ByteBuffer(oldRsrc.Data);
                    PatchResourceDirectoryTable(buff, oldRsrc, sect);
                    sect.Data = buff.GetBuffer();
                };
            }
            psr.Process(asm.MainModule, tmp + Path.GetFileName(modDef.FullyQualifiedName), new WriterParameters()
            {
                StrongNameKeyPair = this.cr.sn,
                WriteSymbols = this.cr.param.Project.Debug
            });

            Confuser cr = new Confuser();

            ConfuserProject proj = new ConfuserProject();
            proj.Seed = Random.Next().ToString();
            proj.Debug = this.cr.param.Project.Debug;
            foreach (var i in this.cr.param.Project.Rules)
                proj.Rules.Add(i);
            proj.Add(new ProjectAssembly()
            {
                Path = tmp + Path.GetFileName(modDef.FullyQualifiedName)
            });
            proj.OutputPath = tmp;
            foreach (var i in this.cr.param.Project.Plugins) proj.Plugins.Add(i);
            proj.SNKeyPath = this.cr.param.Project.SNKeyPath;

            ConfuserParameter par = new ConfuserParameter();
            par.Project = proj;
            par.ProcessMetadata = PostProcessMetadata;
            par.ProcessImage = PostProcessImage;
            cr.Confuse(par);

            return Directory.GetFiles(tmp);
        }
        public abstract string[] Pack(ConfuserParameter crParam, PackerParameter param);

        static void PatchResourceDirectoryTable(ByteBuffer resources, Section old, Section @new)
        {
            resources.Advance(12);
            int num = resources.ReadUInt16() + resources.ReadUInt16();
            for (int i = 0; i < num; i++)
            {
                PatchResourceDirectoryEntry(resources, old, @new);
            }
        }
        static void PatchResourceDirectoryEntry(ByteBuffer resources, Section old, Section @new)
        {
            resources.Advance(4);
            uint num = resources.ReadUInt32();
            int position = resources.Position;
            resources.Position = ((int)num) & 0x7fffffff;
            if ((num & 0x80000000) != 0)
            {
                PatchResourceDirectoryTable(resources, old, @new);
            }
            else
            {
                PatchResourceDataEntry(resources, old, @new);
            }
            resources.Position = position;
        }
        static void PatchResourceDataEntry(ByteBuffer resources, Section old, Section @new)
        {
            uint num = resources.ReadUInt32();
            resources.Position -= 4;
            resources.WriteUInt32(num - old.VirtualAddress + @new.VirtualAddress);
        }
    }
}