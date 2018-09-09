using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;
using Mono.Cecil.Cil;

namespace Confuser.Core.Confusions
{
    public class AntiDebugConfusion : StructurePhase, IConfusion
    {
        public string Name
        {
            get { return "Anti Debug Confusion"; }
        }
        public string Description
        {
            get { return "This confusion prevent the assembly from debugging/profiling."; }
        }
        public string ID
        {
            get { return "anti debug"; }
        }
        public bool StandardCompatible
        {
            get { return true; }
        }
        public Target Target
        {
            get { return Target.Module; }
        }
        public Preset Preset
        {
            get { return Preset.Normal; }
        }
        public Phase[] Phases
        {
            get { return new Phase[] { this }; }
        }
        public bool SupportLateAddition
        {
            get { return false; }
        }
        public Behaviour Behaviour
        {
            get { return Behaviour.Inject; }
        }

        public override Priority Priority
        {
            get { return Priority.AssemblyLevel; }
        }
        public override IConfusion Confusion
        {
            get { return this; }
        }
        public override int PhaseID
        {
            get { return 1; }
        }
        public override bool WholeRun
        {
            get { return true; }
        }

        public override void Initialize(ModuleDefinition mod)
        {
            this.mod = mod;
        }
        public override void DeInitialize()
        {
            //
        }

        ModuleDefinition mod;
        public override void Process(ConfusionParameter parameter)
        {
            AssemblyDefinition self = AssemblyDefinition.ReadAssembly(typeof(Iid).Assembly.Location);
            Database.AddEntry("AntiDebug", "Win32", Array.IndexOf(parameter.GlobalParameters.AllKeys, "win32") != -1);
            if (true)//(Array.IndexOf(parameter.GlobalParameters.AllKeys, "win32") != -1)
            {
                TypeDefinition type = CecilHelper.Inject(mod, self.MainModule.GetType("AntiDebugger"));
                type.Methods.Remove(type.Methods.FirstOrDefault(mtd => mtd.Name == "AntiDebugSafe"));
                type.Methods.Remove(type.Methods.FirstOrDefault(mtd => mtd.Name == "InitializeSafe"));
                mod.Types.Add(type);
                TypeDefinition modType = mod.GetType("<Module>");
                ILProcessor psr = modType.GetStaticConstructor().Body.GetILProcessor();
                psr.InsertBefore(psr.Body.Instructions.Count - 1, Instruction.Create(OpCodes.Call, type.Methods.FirstOrDefault(mtd => mtd.Name == "Initialize")));

                type.Name = ObfuscationHelper.GetRandomName();
                type.Namespace = "";
                AddHelper(type, HelperAttribute.NoInjection);
                foreach (MethodDefinition mtdDef in type.Methods)
                {
                    mtdDef.Name = ObfuscationHelper.GetRandomName();
                    AddHelper(mtdDef, HelperAttribute.NoInjection);
                }
                Database.AddEntry("AntiDebug", "Helper", type.FullName);
            }
            else
            {
                TypeDefinition type = CecilHelper.Inject(mod, self.MainModule.GetType("AntiDebugger"));
                type.Methods.Remove(type.Methods.FirstOrDefault(mtd => mtd.Name == "AntiDebug"));
                type.Methods.Remove(type.Methods.FirstOrDefault(mtd => mtd.Name == "Initialize"));
                mod.Types.Add(type);
                TypeDefinition modType = mod.GetType("<Module>");
                ILProcessor psr = modType.GetStaticConstructor().Body.GetILProcessor();
                psr.InsertBefore(psr.Body.Instructions.Count - 1, Instruction.Create(OpCodes.Call, type.Methods.FirstOrDefault(mtd => mtd.Name == "InitializeSafe")));

                type.Name = ObfuscationHelper.GetRandomName();
                type.Namespace = "";
                AddHelper(type, HelperAttribute.NoInjection);
                foreach (MethodDefinition mtdDef in type.Methods)
                {
                    mtdDef.Name = ObfuscationHelper.GetRandomName();
                    AddHelper(mtdDef, HelperAttribute.NoInjection);
                }
                Database.AddEntry("AntiDebug", "Helper", type.FullName);
            }
        }

        public void Init() { }
        public void Deinit() { }
    }
}
