using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Confuser.Core.Analyzers;

namespace Confuser.Core.Confusions
{
    public class DynamicConfusion : StructurePhase, IConfusion
    {
        public class CleanupPhase : StructurePhase
        {
            DynamicConfusion dc;
            public CleanupPhase(DynamicConfusion d)
            {
                dc = d;
            }
            public override IConfusion Confusion
            {
                get
                {
                    return dc;
                }
            }
            public override int PhaseID
            {
                get { return 3; }
            }

            public override Priority Priority
            {
                get { return Priority.AssemblyLevel; }
            }

            public override bool WholeRun
            {
                get { return false; }
            }

            public override void Initialize(ModuleDefinition mod)
            {
                foreach (var di in ObfuscationHelper.StringGen.DynGen.Dynamics)
                {
                    foreach (var mtd in di.Wrapper.Methods)
                    {
                        if (mtd.IsRuntimeSpecialName || mtd.IsConstructor || mtd.IsSpecialName)
                            continue;
                        mtd.Name = ObfuscationHelper.GetRandomName();
                    }
                    foreach (var fd in di.Wrapper.Fields)
                    {
                        fd.Name = ObfuscationHelper.GetRandomName();
                    }
                    //if (di.Wrapper.Methods.FirstOrDefault(x => x.Name.StartsWith("DYN__")) != null)
                    //{
                    //    mod.Types.Remove(di.Wrapper);
                            
                    //}
                }

            }

            public override void DeInitialize()
            {

            }

            public override void Process(ConfusionParameter parameter)
            {

            }
        }
        public override int PhaseID
        {
            get { return 1; }
        }

        public override Priority Priority
        {
            get { return Priority.Safe; }
        }

        public override bool WholeRun
        {
            get { return false; }
        }

        ModuleDefinition mod;
        public override void Initialize(ModuleDefinition mod)
        {
            this.mod = mod;
            ObfuscationHelper.StringGen = new Poly.Strings.StringGenerator(Random.Next(500000), mod);
            foreach (var di in ObfuscationHelper.StringGen.DynGen.Dynamics)
            {
                foreach (TypeDefinition td in di.Wrapper.NestedTypes)
                {
                    foreach (MethodDefinition md in td.Methods)
                    {
                        AddHelper(md, HelperAttribute.NoEncrypt);
                    }
                    AddHelper(td, HelperAttribute.NoEncrypt);
                }
                foreach (MethodDefinition md in di.Wrapper.Methods)
                {
                    AddHelper(md, HelperAttribute.NoEncrypt);
                }
                AddHelper(di.Wrapper, HelperAttribute.NoEncrypt);
                
            }
            
        }

        public override void DeInitialize()
        {
            
        }

        public override void Process(ConfusionParameter parameter)
        {
            
        }

        public override IConfusion Confusion
        {
            get { return this; }
        }
        public string ID
        {
            get { return "dynamics"; }
        }
        public string Name
        {
            get { return "Dynamic Confusion"; }
        }
        public string Description
        {
            get { return "This confusion enables dynamic generation of expressions in all expression generators."; }
        }
        public Target Target
        {
            get { return Target.Types | Target.Fields | Target.Methods | Target.Properties | Target.Events; }
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
            get { return Behaviour.Inject | Behaviour.AlterStructure; }
        }

        public Phase[] Phases
        {
            get { return new Phase[] { this, new CleanupPhase(this) }; }
        }

        public void Init() { }
        public void Deinit() { }
    }
}
