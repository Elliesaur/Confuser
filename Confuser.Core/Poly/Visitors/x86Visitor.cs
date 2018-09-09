using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core.Poly.Expressions;

namespace Confuser.Core.Poly.Visitors
{
    enum x86OpCode
    {
        MOV,
        ADD,
        SUB,
        MUL,
        DIV,
        NEG,
        NOT,
        XOR,
        POP
    }
    enum x86Register
    {
        EAX,
        ECX,
        EDX,
        EBX,
        ESP,
        EBP,
        ESI,
        EDI
    }
    interface Ix86Operand { }
    class x86RegisterOperand : Ix86Operand
    {
        public x86Register Register { get; set; }
        public override string ToString()
        {
            return Register.ToString();
        }
    }
    class x86ImmediateOperand : Ix86Operand
    {
        public int Immediate { get; set; }
        public override string ToString()
        {
            return Immediate.ToString("X") + "h";
        }
    }
    class x86Instruction
    {
        public x86OpCode OpCode { get; set; }
        public Ix86Operand[] Operands { get; set; }

        public byte[] Assemble()
        {
            switch (OpCode)
            {
                case x86OpCode.MOV:
                    {
                        if (Operands.Length != 2) throw new InvalidOperationException();
                        if (Operands[0] is x86RegisterOperand &&
                            Operands[1] is x86RegisterOperand)
                        {
                            byte[] ret = new byte[2];
                            ret[0] = 0x89;
                            ret[1] = 0xc0;
                            ret[1] |= (byte)((int)(Operands[1] as x86RegisterOperand).Register << 3);
                            ret[1] |= (byte)((int)(Operands[0] as x86RegisterOperand).Register << 0);
                            return ret;
                        }
                        else if (Operands[0] is x86RegisterOperand &&
                                 Operands[1] is x86ImmediateOperand)
                        {
                            byte[] ret = new byte[5];
                            ret[0] = 0xb8;
                            ret[0] |= (byte)((int)(Operands[0] as x86RegisterOperand).Register << 0);
                            Buffer.BlockCopy(BitConverter.GetBytes((Operands[1] as x86ImmediateOperand).Immediate), 0, ret, 1, 4);
                            return ret;
                        }
                        else
                            throw new NotSupportedException();
                    }

                case x86OpCode.ADD:
                    {
                        if (Operands.Length != 2) throw new InvalidOperationException();
                        if (Operands[0] is x86RegisterOperand &&
                            Operands[1] is x86RegisterOperand)
                        {
                            byte[] ret = new byte[2];
                            ret[0] = 0x01;
                            ret[1] = 0xc0;
                            ret[1] |= (byte)((int)(Operands[1] as x86RegisterOperand).Register << 3);
                            ret[1] |= (byte)((int)(Operands[0] as x86RegisterOperand).Register << 0);
                            return ret;
                        }
                        else if (Operands[0] is x86RegisterOperand &&
                                 Operands[1] is x86ImmediateOperand)
                        {
                            byte[] ret = new byte[6];
                            ret[0] = 0x81;
                            ret[1] = 0xc0;
                            ret[1] |= (byte)((int)(Operands[0] as x86RegisterOperand).Register << 0);
                            Buffer.BlockCopy(BitConverter.GetBytes((Operands[1] as x86ImmediateOperand).Immediate), 0, ret, 2, 4);
                            return ret;
                        }
                        else
                            throw new NotSupportedException();
                    }

                case x86OpCode.SUB:
                    {
                        if (Operands.Length != 2) throw new InvalidOperationException();
                        if (Operands[0] is x86RegisterOperand &&
                            Operands[1] is x86RegisterOperand)
                        {
                            byte[] ret = new byte[2];
                            ret[0] = 0x29;
                            ret[1] = 0xc0;
                            ret[1] |= (byte)((int)(Operands[1] as x86RegisterOperand).Register << 3);
                            ret[1] |= (byte)((int)(Operands[0] as x86RegisterOperand).Register << 0);
                            return ret;
                        }
                        else if (Operands[0] is x86RegisterOperand &&
                                 Operands[1] is x86ImmediateOperand)
                        {
                            byte[] ret = new byte[6];
                            ret[0] = 0x81;
                            ret[1] = 0xe8;
                            ret[1] |= (byte)((int)(Operands[0] as x86RegisterOperand).Register << 0);
                            Buffer.BlockCopy(BitConverter.GetBytes((Operands[1] as x86ImmediateOperand).Immediate), 0, ret, 2, 4);
                            return ret;
                        }
                        else
                            throw new NotSupportedException();
                    }

                case x86OpCode.NEG:
                    {
                        if (Operands.Length != 1) throw new InvalidOperationException();
                        if (Operands[0] is x86RegisterOperand)
                        {
                            byte[] ret = new byte[2];
                            ret[0] = 0xf7;
                            ret[1] = 0xd8;
                            ret[1] |= (byte)((int)(Operands[0] as x86RegisterOperand).Register << 0);
                            return ret;
                        }
                        else
                            throw new NotSupportedException();
                    }

                case x86OpCode.NOT:
                    {
                        if (Operands.Length != 1) throw new InvalidOperationException();
                        if (Operands[0] is x86RegisterOperand)
                        {
                            byte[] ret = new byte[2];
                            ret[0] = 0xf7;
                            ret[1] = 0xd0;
                            ret[1] |= (byte)((int)(Operands[0] as x86RegisterOperand).Register << 0);
                            return ret;
                        }
                        else
                            throw new NotSupportedException();
                    }

                case x86OpCode.XOR:
                    {
                        if (Operands.Length != 2) throw new InvalidOperationException();
                        if (Operands[0] is x86RegisterOperand &&
                            Operands[1] is x86RegisterOperand)
                        {
                            byte[] ret = new byte[2];
                            ret[0] = 0x31;
                            ret[1] = 0xc0;
                            ret[1] |= (byte)((int)(Operands[1] as x86RegisterOperand).Register << 3);
                            ret[1] |= (byte)((int)(Operands[0] as x86RegisterOperand).Register << 0);
                            return ret;
                        }
                        else if (Operands[0] is x86RegisterOperand &&
                                 Operands[1] is x86ImmediateOperand)
                        {
                            byte[] ret = new byte[6];
                            ret[0] = 0x81;
                            ret[1] = 0xf0;
                            ret[1] |= (byte)((int)(Operands[0] as x86RegisterOperand).Register << 0);
                            Buffer.BlockCopy(BitConverter.GetBytes((Operands[1] as x86ImmediateOperand).Immediate), 0, ret, 2, 4);
                            return ret;
                        }
                        else
                            throw new NotSupportedException();
                    }

                case x86OpCode.POP:
                    {
                        if (Operands.Length != 1) throw new InvalidOperationException();
                        if (Operands[0] is x86RegisterOperand)
                        {
                            byte[] ret = new byte[1];
                            ret[0] = 0x58;
                            ret[0] |= (byte)((int)(Operands[0] as x86RegisterOperand).Register << 0);
                            return ret;
                        }
                        else
                            throw new NotSupportedException();
                    }

                default:
                    throw new NotSupportedException();
            }
        }

        public override string ToString()
        {
            StringBuilder ret = new StringBuilder();
            ret.Append(OpCode);
            foreach (var i in Operands)
                ret.AppendFormat(" {0}", i);
            return ret.ToString();
        }
    }
    class x86Visitor : ExpressionVisitor
    {
        List<x86Instruction> insts;
        Func<x86Register, x86Instruction[]> args;
        x86Register reg;
        bool[] regs;

        public bool RegisterOverflowed { get; private set; }

        public x86Visitor(Expression exp, Func<x86Register, x86Instruction[]> args)
        {
            insts = new List<x86Instruction>();
            this.args = args ?? (_ => new x86Instruction[]
            {
                new x86Instruction() {
                    OpCode = x86OpCode.POP,
                    Operands = new Ix86Operand[]
                    {
                        new x86RegisterOperand() { Register = _ }
                    }
                }
            });

            regs = new bool[8];
            regs[(int)x86Register.EBP] = true;  //CRITICAL registers!
            regs[(int)x86Register.ESP] = true;

            exp.VisitPostOrder(this);
            reg = GetRegister(exp);
        }

        public x86Instruction[] GetInstructions(out x86Register reg)
        {
            reg = this.reg;
            return insts.ToArray();
        }

        object REG = new object();
        x86Register GetFreeRegister(Expression exp)
        {
            for (int i = 0; i < 8; i++)
                if (!regs[i])
                    return (x86Register)i;

            RegisterOverflowed = true;
            return x86Register.ESP;         //WHAT? Shouldn't reach here.
        }
        x86Register GetRegister(Expression exp)
        {
            return (x86Register)exp.Annotations[REG];
        }
        void SetRegister(Expression exp, x86Register reg)
        {
            exp.Annotations[REG] = reg;
            regs[(int)reg] = true;
        }
        void ReleaseRegister(Expression exp)
        {
            regs[(int)GetRegister(exp)] = false;
        }

        public override void VisitPostOrder(Expression exp)
        {
            x86Register reg = GetFreeRegister(exp);

            if (exp is ConstantExpression)
            {
                insts.Add(new x86Instruction()
                {
                    OpCode = x86OpCode.MOV,
                    Operands = new Ix86Operand[]
                    {
                        new x86RegisterOperand()  { Register  = reg },
                        new x86ImmediateOperand() { Immediate = (exp as ConstantExpression).Value }
                    }
                });
                SetRegister(exp, reg);
            }
            else if (exp is VariableExpression)
            {
                insts.AddRange(args(reg));
                SetRegister(exp, reg);
            }
            else if (exp is AddExpression)
            {
                AddExpression _exp = exp as AddExpression;
                insts.Add(new x86Instruction()
                {
                    OpCode = x86OpCode.ADD,
                    Operands = new Ix86Operand[]
                    {
                        new x86RegisterOperand() { Register = GetRegister(_exp.OperandA) },
                        new x86RegisterOperand() { Register = GetRegister(_exp.OperandB) }
                    }
                });
                SetRegister(exp, GetRegister(_exp.OperandA));
                ReleaseRegister(_exp.OperandB);
            }
            else if (exp is SubExpression)
            {
                SubExpression _exp = exp as SubExpression;
                insts.Add(new x86Instruction()
                {
                    OpCode = x86OpCode.SUB,
                    Operands = new Ix86Operand[]
                    {
                        new x86RegisterOperand() { Register = GetRegister(_exp.OperandA) },
                        new x86RegisterOperand() { Register = GetRegister(_exp.OperandB) }
                    }
                });
                SetRegister(exp, GetRegister(_exp.OperandA));
                ReleaseRegister(_exp.OperandB);
            }

            else if (exp is MulExpression)
                throw new NotSupportedException();

            else if (exp is DivExpression)
                throw new NotSupportedException();

            else if (exp is NegExpression)
            {
                NegExpression _exp = exp as NegExpression;
                insts.Add(new x86Instruction()
                {
                    OpCode = x86OpCode.NEG,
                    Operands = new Ix86Operand[]
                    {
                        new x86RegisterOperand() { Register = GetRegister(_exp.Value) }
                    }
                });
                SetRegister(exp, GetRegister(_exp.Value));
            }

            else if (exp is InvExpression)
            {
                InvExpression _exp = exp as InvExpression;
                insts.Add(new x86Instruction()
                {
                    OpCode = x86OpCode.NOT,
                    Operands = new Ix86Operand[]
                    {
                        new x86RegisterOperand() { Register = GetRegister(_exp.Value) }
                    }
                });
                SetRegister(exp, GetRegister(_exp.Value));
            }

            else if (exp is XorExpression)
            {
                XorExpression _exp = exp as XorExpression;
                insts.Add(new x86Instruction()
                {
                    OpCode = x86OpCode.XOR,
                    Operands = new Ix86Operand[]
                    {
                        new x86RegisterOperand() { Register = GetRegister(_exp.OperandA) },
                        new x86RegisterOperand() { Register = GetRegister(_exp.OperandB) }
                    }
                });
                SetRegister(exp, GetRegister(_exp.OperandA));
                ReleaseRegister(_exp.OperandB);
            }
        }

        public override void VisitPreOrder(Expression exp)
        {
            //
        }
    }
}
