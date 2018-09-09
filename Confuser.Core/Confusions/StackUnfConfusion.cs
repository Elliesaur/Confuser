using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Confuser.Core.Confusions
{
    public class StackUnfConfusion : StructurePhase, IConfusion
    {
        public string ID
        {
            get { return "stack underflow"; }
        }
        public string Name
        {
            get { return "Stack Underflow Confusion"; }
        }
        public string Description
        {
            get { return "This confusion will add a piece of code in the front of the methods and cause decompilers to crash."; }
        }
        public Target Target
        {
            get { return Target.Methods; }
        }
        public Preset Preset
        {
            get { return Preset.Aggressive; }
        }
        public bool StandardCompatible
        {
            get { return false; }
        }
        public Phase[] Phases
        {
            get { return new Phase[] { this }; }
        }

        public void Init() { }
        public void Deinit() { }


        public override IConfusion Confusion
        {
            get { return this; }
        }
        public override int PhaseID
        {
            get { return 3; }
        }
        public override Priority Priority
        {
            get { return Priority.CodeLevel; }
        }
        public override bool WholeRun
        {
            get { return false; }
        }
        public bool SupportLateAddition
        {
            get { return true; }
        }
        public Behaviour Behaviour
        {
            get { return Behaviour.AlterCode; }
        }

        public override void Initialize(ModuleDefinition mod)
        {
            rad = new Random();
        }
        public override void DeInitialize()
        {
            //
        }

        Random rad;
        public override void Process(ConfusionParameter parameter)
        {
            MethodDefinition mtd = parameter.Target as MethodDefinition;

            if (!mtd.HasBody) return;
            MethodBody bdy = mtd.Body;
            ILProcessor wkr = bdy.GetILProcessor();

            Instruction original = bdy.Instructions[0];
            Instruction jmp = wkr.Create(OpCodes.Br_S, original);

            Instruction stackundering = wkr.Create(OpCodes.Pop);
            Instruction stackrecovering;
            switch (rad.Next(0, 4))
            {
                case 0:
                    stackrecovering = wkr.Create(OpCodes.Ldnull); break;
                case 1:
                    stackrecovering = wkr.Create(OpCodes.Ldc_I4_0); break;
                case 2:
                    stackrecovering = wkr.Create(OpCodes.Ldstr, ""); break;
                default:
                    stackrecovering = wkr.Create(OpCodes.Ldc_I8, (long)rad.Next()); break;
            }
            wkr.InsertBefore(original, stackrecovering);
            wkr.InsertBefore(stackrecovering, stackundering);
            wkr.InsertBefore(stackundering, jmp);

            foreach (ExceptionHandler eh in bdy.ExceptionHandlers)
            {
                if (eh.TryStart == original)
                    eh.TryStart = jmp;
                else if (eh.HandlerStart == original)
                    eh.HandlerStart = jmp;
                else if (eh.FilterStart == original)
                    eh.FilterStart = jmp;
            }
        }
    }
}
