using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Confuser.Core.Confusions
{
    public class AntiDumpConfusion : StructurePhase, IConfusion
    {
        public string Name
        {
            get { return "Anti Dumping Confusion"; }
        }
        public string Description
        {
            get { return "This confusion prevent the assembly from dumping from memory."; }
        }
        public string ID
        {
            get { return "anti dump"; }
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
            get { return Preset.Aggressive; }
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
            TypeDefinition type = CecilHelper.Inject(mod, self.MainModule.GetType("AntiDumping"));
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
            Database.AddEntry("AntiDump", "Helper", type.FullName);
        }

        public void Init() { }
        public void Deinit() { }
    }
}