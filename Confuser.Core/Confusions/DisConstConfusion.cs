using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Confuser.Core.Poly;
using Confuser.Core.Poly.Visitors;

namespace Confuser.Core.Confusions
{
    public class DisConstConfusion : StructurePhase, IConfusion, IProgressProvider
    {
        public string Name
        {
            get { return "Constant Disintegration Confusion"; }
        }
        public string Description
        {
            get
            {
                return @"This confusion disintegrate the constants in the code into expressions.
***This confusion could affect the performance if your application uses constant frequently***";
            }
        }
        public string ID
        {
            get { return "disintegrate const"; }
        }
        public bool StandardCompatible
        {
            get { return true; }
        }
        public Target Target
        {
            get { return Target.Methods; }
        }
        public Preset Preset
        {
            get { return Preset.Maximum; }
        }
        public Phase[] Phases
        {
            get { return new Phase[] { this }; }
        }

        public override Priority Priority
        {
            get { return Priority.CodeLevel; }
        }
        public override IConfusion Confusion
        {
            get { return this; }
        }
        public override int PhaseID
        {
            get { return 3; }
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
            get { return Behaviour.Encrypt | Behaviour.AlterCode; }
        }
        public override void Initialize(ModuleDefinition mod)
        {
            //
        }
        public override void DeInitialize()
        {
            //
        }

        public override void Process(ConfusionParameter parameter)
        {

            List<Context> txts = new List<Context>();
            IList<IAnnotationProvider> targets = parameter.Target as IList<IAnnotationProvider>;
            for (int i = 0; i < targets.Count; i++)
            {
                MethodDefinition mtd = targets[i] as MethodDefinition;
                if (!mtd.HasBody) continue;
                mtd.Body.SimplifyMacros();
                int lv = 5;
                if (Array.IndexOf(parameter.Parameters.AllKeys, mtd.GetHashCode().ToString("X8") + "_level") != -1)
                {
                    if (!int.TryParse(parameter.Parameters[mtd.GetHashCode().ToString("X8") + "_level"], out lv) && (lv <= 0 || lv > 10))
                    {
                        Log("Invaild level, 5 will be used.");
                        lv = 5;
                    }
                }
                foreach(Instruction inst in mtd.Body.Instructions)
                {
                    if ((inst.OpCode.Name == "ldc.i4" && (int)inst.Operand != -1 && (int)inst.Operand != 0 && (int)inst.Operand != 1) ||
                        //(inst.OpCode.Name == "ldc.i8" && (long)inst.Operand != -1 && (long)inst.Operand != 0 && (long)inst.Operand != 1) ||
                        (inst.OpCode.Name == "ldc.r4" && (float)inst.Operand != -1 && (float)inst.Operand != 0 && (float)inst.Operand != 1) ||
                        (inst.OpCode.Name == "ldc.r8" && (double)inst.Operand != -1 && (double)inst.Operand != 0 && (double)inst.Operand != 1))
                        txts.Add(new Context() { mtd = mtd, psr = mtd.Body.GetILProcessor(), inst = inst, lv = lv });
                }
                progresser.SetProgress((i + 1) / (double)targets.Count);
            }

            for (int i = 0; i < txts.Count; i++)
            {
                Context txt = txts[i];
                int instIdx = txt.mtd.Body.Instructions.IndexOf(txt.inst);
                double val = Convert.ToDouble(txt.inst.Operand);
                int seed;

                Expression exp;
                double eval = 0;
                double tmp = 0;
                do
                {
                    exp = ExpressionGenerator.Generate(txt.lv, out seed);
                    eval = DoubleExpressionEvaluator.Evaluate(exp, val);
                    try
                    {
                        tmp = DoubleExpressionEvaluator.ReverseEvaluate(exp, eval);
                    }
                    catch { continue; }
                } while (tmp != val);

                Instruction[] expInsts = new CecilVisitor(exp, true, new Instruction[] { Instruction.Create(OpCodes.Ldc_R8, eval) }, true).GetInstructions();
                if (expInsts.Length == 0) continue;
                string op = txt.inst.OpCode.Name;
                txt.psr.Replace(instIdx, expInsts[0]);
                for (int ii = 1; ii < expInsts.Length; ii++)
                {
                    txt.psr.InsertAfter(instIdx + ii - 1, expInsts[ii]);
                }
                switch (op)
                {
                    case "ldc.i4":
                        txt.psr.InsertAfter(instIdx +expInsts.Length - 1, Instruction.Create(OpCodes.Conv_I4)); break;
                    //case "ldc.i8":
                    //    txt.psr.InsertAfter(instIdx +expInsts.Length - 1, Instruction.Create(OpCodes.Conv_I8)); break;
                    case "ldc.r4":
                        txt.psr.InsertAfter(instIdx +expInsts.Length - 1, Instruction.Create(OpCodes.Conv_R4)); break;
                    case "ldc.r8":
                        txt.psr.InsertAfter(instIdx +expInsts.Length - 1, Instruction.Create(OpCodes.Conv_R8)); break;
                }

                progresser.SetProgress((i + 1) / (double)txts.Count);
            }
        }

        IProgresser progresser;
        void IProgressProvider.SetProgresser(IProgresser progresser)
        {
            this.progresser = progresser;
        }

        private class Context { public MethodDefinition mtd; public ILProcessor psr; public Instruction inst; public int lv;}
    }
}