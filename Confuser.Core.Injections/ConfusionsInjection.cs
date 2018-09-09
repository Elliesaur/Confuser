using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

static class AntiDebugger
{
    [DllImport("ntdll.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
    static extern int NtQueryInformationProcess(IntPtr ProcessHandle, int ProcessInformationClass,
        byte[] ProcessInformation, uint ProcessInformationLength, out int ReturnLength);
    [DllImport("ntdll.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
    static extern uint NtSetInformationProcess(IntPtr ProcessHandle, int ProcessInformationClass,
        byte[] ProcessInformation, uint ProcessInformationLength);
    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr hObject);
    [DllImport("kernel32.dll")]
    static extern bool IsDebuggerPresent();
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    static extern int OutputDebugString(string str);

    public static void Initialize()
    {
        if (Environment.GetEnvironmentVariable("COR_ENABLE_PROFILING") != null ||
            Environment.GetEnvironmentVariable("COR_PROFILER") != null)
            Process.GetCurrentProcess().Kill();

        Thread thread = new Thread(AntiDebug);
        thread.IsBackground = true;
        thread.Start(null);
    }
    static void AntiDebug(object thread)
    {
        Thread th = thread as Thread;
        if (th == null)
        {
            th = new Thread(AntiDebug);
            th.IsBackground = true;
            th.Start(Thread.CurrentThread);
            Thread.Sleep(500);
        }
        while (true)
        {
            //Managed
            if (Debugger.IsAttached || Debugger.IsLogging())
                Process.GetCurrentProcess().Kill();

            //IsDebuggerPresent
            if (IsDebuggerPresent())
                Process.GetCurrentProcess().Kill();

            //Open process
            IntPtr ps = Process.GetCurrentProcess().Handle;
            if (ps == IntPtr.Zero)
                Process.GetCurrentProcess().Kill();

            //OutputDebugString
            if (OutputDebugString("") > IntPtr.Size)
                Process.GetCurrentProcess().Kill();

            //Close
            try
            {
                CloseHandle(IntPtr.Zero);
            }
            catch
            {
                Process.GetCurrentProcess().Kill();
            }

            if (!th.IsAlive)
                Process.GetCurrentProcess().Kill();

            Thread.Sleep(1000);
        }
    }

    public static void InitializeSafe()
    {
        string x = "COR_";
        if (Environment.GetEnvironmentVariable(x + "PROFILER") != null ||
            Environment.GetEnvironmentVariable(x + "ENABLE_PROFILING") != null)
            Environment.FailFast("");

        Thread thread = new Thread(AntiDebugSafe);
        thread.IsBackground = true;
        thread.Start(null);
    }
    private static void AntiDebugSafe(object thread)
    {
        Thread th = thread as Thread;
        if (th == null)
        {
            th = new Thread(AntiDebugSafe);
            th.IsBackground = true;
            th.Start(Thread.CurrentThread);
            Thread.Sleep(500);
        }
        while (true)
        {
            if (Debugger.IsAttached || Debugger.IsLogging())
                Environment.FailFast("");

            if (!th.IsAlive)
                Environment.FailFast("");

            Thread.Sleep(1000);
        }
    }
}

static class Proxies
{
    private static void CtorProxy(RuntimeFieldHandle f)
    {
        FieldInfo fld = FieldInfo.GetFieldFromHandle(f);
        var m = fld.Module;
        byte[] dat = m.ResolveSignature(fld.MetadataToken);

        uint x =
            ((uint)dat[dat.Length - 6] << 0) |
            ((uint)dat[dat.Length - 5] << 8) |
            ((uint)dat[dat.Length - 3] << 16) |
            ((uint)dat[dat.Length - 2] << 24);

        ConstructorInfo mtd = m.ResolveMethod(Mutation.Placeholder((int)x) | ((int)dat[dat.Length - 7] << 24)) as ConstructorInfo;

        var args = mtd.GetParameters();
        Type[] arg = new Type[args.Length];
        for (int i = 0; i < args.Length; i++)
            arg[i] = args[i].ParameterType;

        DynamicMethod dm;
        if (mtd.DeclaringType.IsInterface || mtd.DeclaringType.IsArray)
            dm = new DynamicMethod("", mtd.DeclaringType, arg, fld.DeclaringType, true);
        else
            dm = new DynamicMethod("", mtd.DeclaringType, arg, mtd.DeclaringType, true);
        Console.WriteLine(mtd.DeclaringType);
        Console.WriteLine(mtd.Name);
        StackFrame[] st = new StackTrace(0).GetFrames(); if (st.Length > 1) if (st[0].GetMethod().Module.Assembly != Assembly.GetExecutingAssembly()) Process.GetCurrentProcess().Kill();
        var info = dm.GetDynamicILInfo();
        info.SetLocalSignature(new byte[] { 0x7, 0x0 });
        byte[] y = new byte[2 * arg.Length + 6 + 5];
        for (int i = 0; i < arg.Length; i++)
        {
            y[i * 2] = 0x0e;
            y[i * 2 + 1] = (byte)i;
        }
        y[arg.Length * 2] = 0x73;
        Buffer.BlockCopy(BitConverter.GetBytes(info.GetTokenFor(mtd.MethodHandle)), 0, y, arg.Length * 2 + 1, 4);
        y[arg.Length * 2 + 5] = 0x74;
        Buffer.BlockCopy(BitConverter.GetBytes(info.GetTokenFor(mtd.DeclaringType.TypeHandle)), 0, y, arg.Length * 2 + 6, 4);
        y[y.Length - 1] = 0x2a;
        info.SetCode(y, arg.Length + 1);
        //Mutation.Break();
        fld.SetValue(null, dm.CreateDelegate(fld.FieldType));
    }
    private static void MtdProxy(RuntimeFieldHandle f)
    {
        var fld = FieldInfo.GetFieldFromHandle(f);
        var m = fld.Module;
        byte[] dat = m.ResolveSignature(fld.MetadataToken);

        uint x =
            ((uint)dat[dat.Length - 6] << 0) |
            ((uint)dat[dat.Length - 5] << 8) |
            ((uint)dat[dat.Length - 3] << 16) |
            ((uint)dat[dat.Length - 2] << 24);

        // REMOVE THIS FOR DLL MODULES!!!

        //StackFrame[] st = new StackTrace(0).GetFrames(); 




        var mtd = m.ResolveMethod(Mutation.Placeholder((int)x) | ((int)dat[dat.Length - 7] << 24)) as MethodInfo;

        if (mtd.IsStatic)
        {
            // REMOVE THIS FOR DLL MODULES!!!
            /*if (st.Length > 1)
                if (st[0].GetMethod().Module.Assembly != Assembly.GetExecutingAssembly() || st[0].GetMethod().Module.Assembly != Assembly.GetEntryAssembly())
                    Process.GetCurrentProcess().Kill();
                else
                    
             */
            fld.SetValue(null, Delegate.CreateDelegate(fld.FieldType, mtd));
        }
        else
        {
            string n = fld.Name;

            var tmp = mtd.GetParameters();
            Type[] arg = new Type[tmp.Length + 1];
            arg[0] = typeof(object);
            for (int i = 0; i < tmp.Length; i++)
                arg[i + 1] = tmp[i].ParameterType;

            DynamicMethod dm;
            var decl = mtd.DeclaringType;
            var decl2 = fld.DeclaringType;
            //StackFrame[] st = new StackTrace(0).GetFrames(); if (st.Length > 1) if (st[0].GetMethod().Module.Assembly != Assembly.GetExecutingAssembly() || st[0].GetMethod().Module.Assembly != Assembly.GetEntryAssembly()) Process.GetCurrentProcess().Kill();
            if (decl.IsInterface || decl.IsArray)
                dm = new DynamicMethod("", mtd.ReturnType, arg, decl2, true);
            else
                dm = new DynamicMethod("", mtd.ReturnType, arg, decl, true);

            var info = dm.GetDynamicILInfo();
            info.SetLocalSignature(new byte[] { 0x7, 0x0 });
            byte[] y = new byte[2 * arg.Length + 11];
            int idx = 0;
            for (int i = 0; i < arg.Length; i++)
            {
                y[idx++] = 0x0e;
                y[idx++] = (byte)i;
                if (i == 0)
                {
                    y[idx++] = 0x74;
                    Buffer.BlockCopy(BitConverter.GetBytes(info.GetTokenFor(decl.TypeHandle)), 0, y, idx, 4);
                    idx += 4;
                }
            }
            y[idx++] = (byte)((n[0] == Mutation.Key0I) ? 0x6f : 0x28);
            Buffer.BlockCopy(BitConverter.GetBytes(info.GetTokenFor(mtd.MethodHandle)), 0, y, idx, 4);
            idx += 4;
            y[idx] = 0x2a;


            // REMOVE THIS FOR DLL MODULES!!!
            //st = new StackTrace(0).GetFrames(); if (st.Length > 1) if (st[0].GetMethod().Module.Assembly != Assembly.GetExecutingAssembly() || st[0].GetMethod().Module.Assembly != Assembly.GetEntryAssembly()) y[0] = 0x2a;




            info.SetCode(y, arg.Length + 1);

            fld.SetValue(null, dm.CreateDelegate(fld.FieldType));
        }
    }
}
class Lzma
{
    const uint kNumStates = 12;

    struct State
    {
        public uint Index;
        public void Init() { Index = 0; }
        public void UpdateChar()
        {
            if (Index < 4) Index = 0;
            else if (Index < 10) Index -= 3;
            else Index -= 6;
        }
        public void UpdateMatch() { Index = (uint)(Index < 7 ? 7 : 10); }
        public void UpdateRep() { Index = (uint)(Index < 7 ? 8 : 11); }
        public void UpdateShortRep() { Index = (uint)(Index < 7 ? 9 : 11); }
        public bool IsCharState() { return Index < 7; }
    }

    const int kNumPosSlotBits = 6;

    const uint kNumLenToPosStates = 4;

    const uint kMatchMinLen = 2;

    static uint GetLenToPosState(uint len)
    {
        len -= kMatchMinLen;
        if (len < kNumLenToPosStates)
            return len;
        return unchecked((uint)(kNumLenToPosStates - 1));
    }

    const int kNumAlignBits = 4;
    const uint kAlignTableSize = 1 << kNumAlignBits;

    const uint kStartPosModelIndex = 4;
    const uint kEndPosModelIndex = 14;

    const uint kNumFullDistances = 1 << ((int)kEndPosModelIndex / 2);

    const int kNumPosStatesBitsMax = 4;
    const uint kNumPosStatesMax = (1 << kNumPosStatesBitsMax);

    const int kNumLowLenBits = 3;
    const int kNumMidLenBits = 3;
    const int kNumHighLenBits = 8;
    const uint kNumLowLenSymbols = 1 << kNumLowLenBits;
    const uint kNumMidLenSymbols = 1 << kNumMidLenBits;

    class OutWindow
    {
        byte[] _buffer = null;
        uint _pos;
        uint _windowSize = 0;
        uint _streamPos;
        Stream _stream;

        public void Create(uint windowSize)
        {
            if (_windowSize != windowSize)
            {
                _buffer = new byte[windowSize];
            }
            _windowSize = windowSize;
            _pos = 0;
            _streamPos = 0;
        }

        public void Init(System.IO.Stream stream, bool solid)
        {
            ReleaseStream();
            _stream = stream;
            if (!solid)
            {
                _streamPos = 0;
                _pos = 0;
            }
        }

        public void ReleaseStream()
        {
            Flush();
            _stream = null;
            Buffer.BlockCopy(new byte[_buffer.Length], 0, _buffer, 0, _buffer.Length);
        }

        public void Flush()
        {
            uint size = _pos - _streamPos;
            if (size == 0)
                return;
            _stream.Write(_buffer, (int)_streamPos, (int)size);
            if (_pos >= _windowSize)
                _pos = 0;
            _streamPos = _pos;
        }

        public void CopyBlock(uint distance, uint len)
        {
            uint pos = _pos - distance - 1;
            if (pos >= _windowSize)
                pos += _windowSize;
            for (; len > 0; len--)
            {
                if (pos >= _windowSize)
                    pos = 0;
                _buffer[_pos++] = _buffer[pos++];
                if (_pos >= _windowSize)
                    Flush();
            }
        }

        public void PutByte(byte b)
        {
            _buffer[_pos++] = b;
            if (_pos >= _windowSize)
                Flush();
        }

        public byte GetByte(uint distance)
        {
            uint pos = _pos - distance - 1;
            if (pos >= _windowSize)
                pos += _windowSize;
            return _buffer[pos];
        }
    }

    class Decoder
    {
        public const uint kTopValue = (1 << 24);
        public uint Range;
        public uint Code;
        public Stream Stream;

        public void Init(System.IO.Stream stream)
        {
            // Stream.Init(stream);
            Stream = stream;

            Code = 0;
            Range = 0xFFFFFFFF;
            for (int i = 0; i < 5; i++)
                Code = (Code << 8) | (byte)Stream.ReadByte();
        }

        public void ReleaseStream()
        {
            Stream = null;
        }

        public void Normalize()
        {
            while (Range < kTopValue)
            {
                Code = (Code << 8) | (byte)Stream.ReadByte();
                Range <<= 8;
            }
        }

        public uint DecodeDirectBits(int numTotalBits)
        {
            uint range = Range;
            uint code = Code;
            uint result = 0;
            for (int i = numTotalBits; i > 0; i--)
            {
                range >>= 1;
                /*
                result <<= 1;
                if (code >= range)
                {
                    code -= range;
                    result |= 1;
                }
                */
                uint t = (code - range) >> 31;
                code -= range & (t - 1);
                result = (result << 1) | (1 - t);

                if (range < kTopValue)
                {
                    code = (code << 8) | (byte)Stream.ReadByte();
                    range <<= 8;
                }
            }
            Range = range;
            Code = code;
            return result;
        }
    }

    struct BitDecoder
    {
        public const int kNumBitModelTotalBits = 11;
        public const uint kBitModelTotal = (1 << kNumBitModelTotalBits);
        const int kNumMoveBits = 5;

        uint Prob;

        public void Init() { Prob = kBitModelTotal >> 1; }

        public uint Decode(Decoder rangeDecoder)
        {
            uint newBound = (uint)(rangeDecoder.Range >> kNumBitModelTotalBits) * (uint)Prob;
            if (rangeDecoder.Code < newBound)
            {
                rangeDecoder.Range = newBound;
                Prob += (kBitModelTotal - Prob) >> kNumMoveBits;
                if (rangeDecoder.Range < Decoder.kTopValue)
                {
                    rangeDecoder.Code = (rangeDecoder.Code << 8) | (byte)rangeDecoder.Stream.ReadByte();
                    rangeDecoder.Range <<= 8;
                }
                return 0;
            }
            else
            {
                rangeDecoder.Range -= newBound;
                rangeDecoder.Code -= newBound;
                Prob -= (Prob) >> kNumMoveBits;
                if (rangeDecoder.Range < Decoder.kTopValue)
                {
                    rangeDecoder.Code = (rangeDecoder.Code << 8) | (byte)rangeDecoder.Stream.ReadByte();
                    rangeDecoder.Range <<= 8;
                }
                return 1;
            }
        }
    }

    struct BitTreeDecoder
    {
        BitDecoder[] Models;
        int NumBitLevels;

        public BitTreeDecoder(int numBitLevels)
        {
            NumBitLevels = numBitLevels;
            Models = new BitDecoder[1 << numBitLevels];
        }

        public void Init()
        {
            for (uint i = 1; i < (1 << NumBitLevels); i++)
                Models[i].Init();
        }

        public uint Decode(Decoder rangeDecoder)
        {
            uint m = 1;
            for (int bitIndex = NumBitLevels; bitIndex > 0; bitIndex--)
                m = (m << 1) + Models[m].Decode(rangeDecoder);
            return m - ((uint)1 << NumBitLevels);
        }

        public uint ReverseDecode(Decoder rangeDecoder)
        {
            uint m = 1;
            uint symbol = 0;
            for (int bitIndex = 0; bitIndex < NumBitLevels; bitIndex++)
            {
                uint bit = Models[m].Decode(rangeDecoder);
                m <<= 1;
                m += bit;
                symbol |= (bit << bitIndex);
            }
            return symbol;
        }

        public static uint ReverseDecode(BitDecoder[] Models, UInt32 startIndex,
            Decoder rangeDecoder, int NumBitLevels)
        {
            uint m = 1;
            uint symbol = 0;
            for (int bitIndex = 0; bitIndex < NumBitLevels; bitIndex++)
            {
                uint bit = Models[startIndex + m].Decode(rangeDecoder);
                m <<= 1;
                m += bit;
                symbol |= (bit << bitIndex);
            }
            return symbol;
        }
    }

    public class LzmaDecoder
    {
        class LenDecoder
        {
            BitDecoder m_Choice = new BitDecoder();
            BitDecoder m_Choice2 = new BitDecoder();
            BitTreeDecoder[] m_LowCoder = new BitTreeDecoder[kNumPosStatesMax];
            BitTreeDecoder[] m_MidCoder = new BitTreeDecoder[kNumPosStatesMax];
            BitTreeDecoder m_HighCoder = new BitTreeDecoder(kNumHighLenBits);
            uint m_NumPosStates = 0;

            public void Create(uint numPosStates)
            {
                for (uint posState = m_NumPosStates; posState < numPosStates; posState++)
                {
                    m_LowCoder[posState] = new BitTreeDecoder(kNumLowLenBits);
                    m_MidCoder[posState] = new BitTreeDecoder(kNumMidLenBits);
                }
                m_NumPosStates = numPosStates;
            }

            public void Init()
            {
                m_Choice.Init();
                for (uint posState = 0; posState < m_NumPosStates; posState++)
                {
                    m_LowCoder[posState].Init();
                    m_MidCoder[posState].Init();
                }
                m_Choice2.Init();
                m_HighCoder.Init();
            }

            public uint Decode(Decoder rangeDecoder, uint posState)
            {
                if (m_Choice.Decode(rangeDecoder) == 0)
                    return m_LowCoder[posState].Decode(rangeDecoder);
                else
                {
                    uint symbol = kNumLowLenSymbols;
                    if (m_Choice2.Decode(rangeDecoder) == 0)
                        symbol += m_MidCoder[posState].Decode(rangeDecoder);
                    else
                    {
                        symbol += kNumMidLenSymbols;
                        symbol += m_HighCoder.Decode(rangeDecoder);
                    }
                    return symbol;
                }
            }
        }

        class LiteralDecoder
        {
            struct Decoder2
            {
                BitDecoder[] m_Decoders;
                public void Create() { m_Decoders = new BitDecoder[0x300]; }
                public void Init() { for (int i = 0; i < 0x300; i++) m_Decoders[i].Init(); }

                public byte DecodeNormal(Decoder rangeDecoder)
                {
                    uint symbol = 1;
                    do
                        symbol = (symbol << 1) | m_Decoders[symbol].Decode(rangeDecoder);
                    while (symbol < 0x100);
                    return (byte)symbol;
                }

                public byte DecodeWithMatchByte(Decoder rangeDecoder, byte matchByte)
                {
                    uint symbol = 1;
                    do
                    {
                        uint matchBit = (uint)(matchByte >> 7) & 1;
                        matchByte <<= 1;
                        uint bit = m_Decoders[((1 + matchBit) << 8) + symbol].Decode(rangeDecoder);
                        symbol = (symbol << 1) | bit;
                        if (matchBit != bit)
                        {
                            while (symbol < 0x100)
                                symbol = (symbol << 1) | m_Decoders[symbol].Decode(rangeDecoder);
                            break;
                        }
                    }
                    while (symbol < 0x100);
                    return (byte)symbol;
                }
            }

            Decoder2[] m_Coders;
            int m_NumPrevBits;
            int m_NumPosBits;
            uint m_PosMask;

            public void Create(int numPosBits, int numPrevBits)
            {
                if (m_Coders != null && m_NumPrevBits == numPrevBits &&
                    m_NumPosBits == numPosBits)
                    return;
                m_NumPosBits = numPosBits;
                m_PosMask = ((uint)1 << numPosBits) - 1;
                m_NumPrevBits = numPrevBits;
                uint numStates = (uint)1 << (m_NumPrevBits + m_NumPosBits);
                m_Coders = new Decoder2[numStates];
                for (uint i = 0; i < numStates; i++)
                    m_Coders[i].Create();
            }

            public void Init()
            {
                uint numStates = (uint)1 << (m_NumPrevBits + m_NumPosBits);
                for (uint i = 0; i < numStates; i++)
                    m_Coders[i].Init();
            }

            uint GetState(uint pos, byte prevByte)
            { return ((pos & m_PosMask) << m_NumPrevBits) + (uint)(prevByte >> (8 - m_NumPrevBits)); }

            public byte DecodeNormal(Decoder rangeDecoder, uint pos, byte prevByte)
            { return m_Coders[GetState(pos, prevByte)].DecodeNormal(rangeDecoder); }

            public byte DecodeWithMatchByte(Decoder rangeDecoder, uint pos, byte prevByte, byte matchByte)
            { return m_Coders[GetState(pos, prevByte)].DecodeWithMatchByte(rangeDecoder, matchByte); }
        };

        OutWindow m_OutWindow = new OutWindow();
        Decoder m_RangeDecoder = new Decoder();

        BitDecoder[] m_IsMatchDecoders = new BitDecoder[kNumStates << kNumPosStatesBitsMax];
        BitDecoder[] m_IsRepDecoders = new BitDecoder[kNumStates];
        BitDecoder[] m_IsRepG0Decoders = new BitDecoder[kNumStates];
        BitDecoder[] m_IsRepG1Decoders = new BitDecoder[kNumStates];
        BitDecoder[] m_IsRepG2Decoders = new BitDecoder[kNumStates];
        BitDecoder[] m_IsRep0LongDecoders = new BitDecoder[kNumStates << kNumPosStatesBitsMax];

        BitTreeDecoder[] m_PosSlotDecoder = new BitTreeDecoder[kNumLenToPosStates];
        BitDecoder[] m_PosDecoders = new BitDecoder[kNumFullDistances - kEndPosModelIndex];

        BitTreeDecoder m_PosAlignDecoder = new BitTreeDecoder(kNumAlignBits);

        LenDecoder m_LenDecoder = new LenDecoder();
        LenDecoder m_RepLenDecoder = new LenDecoder();

        LiteralDecoder m_LiteralDecoder = new LiteralDecoder();

        uint m_DictionarySize;
        uint m_DictionarySizeCheck;

        uint m_PosStateMask;

        public LzmaDecoder()
        {
            m_DictionarySize = 0xFFFFFFFF;
            for (int i = 0; i < kNumLenToPosStates; i++)
                m_PosSlotDecoder[i] = new BitTreeDecoder(kNumPosSlotBits);
        }

        void SetDictionarySize(uint dictionarySize)
        {
            if (m_DictionarySize != dictionarySize)
            {
                m_DictionarySize = dictionarySize;
                m_DictionarySizeCheck = Math.Max(m_DictionarySize, 1);
                uint blockSize = Math.Max(m_DictionarySizeCheck, (1 << 12));
                m_OutWindow.Create(blockSize);
            }
        }

        void SetLiteralProperties(int lp, int lc)
        {
            m_LiteralDecoder.Create(lp, lc);
        }

        void SetPosBitsProperties(int pb)
        {
            uint numPosStates = (uint)1 << pb;
            m_LenDecoder.Create(numPosStates);
            m_RepLenDecoder.Create(numPosStates);
            m_PosStateMask = numPosStates - 1;
        }

        bool _solid = false;
        void Init(System.IO.Stream inStream, System.IO.Stream outStream)
        {
            m_RangeDecoder.Init(inStream);
            m_OutWindow.Init(outStream, _solid);

            uint i;
            for (i = 0; i < kNumStates; i++)
            {
                for (uint j = 0; j <= m_PosStateMask; j++)
                {
                    uint index = (i << kNumPosStatesBitsMax) + j;
                    m_IsMatchDecoders[index].Init();
                    m_IsRep0LongDecoders[index].Init();
                }
                m_IsRepDecoders[i].Init();
                m_IsRepG0Decoders[i].Init();
                m_IsRepG1Decoders[i].Init();
                m_IsRepG2Decoders[i].Init();
            }

            m_LiteralDecoder.Init();
            for (i = 0; i < kNumLenToPosStates; i++)
                m_PosSlotDecoder[i].Init();
            // m_PosSpecDecoder.Init();
            for (i = 0; i < kNumFullDistances - kEndPosModelIndex; i++)
                m_PosDecoders[i].Init();

            m_LenDecoder.Init();
            m_RepLenDecoder.Init();
            m_PosAlignDecoder.Init();
        }

        public void Code(System.IO.Stream inStream, System.IO.Stream outStream,
            Int64 inSize, Int64 outSize)
        {
            Init(inStream, outStream);

            State state = new State();
            state.Init();
            uint rep0 = 0, rep1 = 0, rep2 = 0, rep3 = 0;

            UInt64 nowPos64 = 0;
            UInt64 outSize64 = (UInt64)outSize;
            if (nowPos64 < outSize64)
            {
                if (m_IsMatchDecoders[state.Index << kNumPosStatesBitsMax].Decode(m_RangeDecoder) != 0)
                    throw new Exception();
                state.UpdateChar();
                byte b = m_LiteralDecoder.DecodeNormal(m_RangeDecoder, 0, 0);
                m_OutWindow.PutByte(b);
                nowPos64++;
            }
            while (nowPos64 < outSize64)
            {
                // UInt64 next = Math.Min(nowPos64 + (1 << 18), outSize64);
                // while(nowPos64 < next)
                {
                    uint posState = (uint)nowPos64 & m_PosStateMask;
                    if (m_IsMatchDecoders[(state.Index << kNumPosStatesBitsMax) + posState].Decode(m_RangeDecoder) == 0)
                    {
                        byte b;
                        byte prevByte = m_OutWindow.GetByte(0);
                        if (!state.IsCharState())
                            b = m_LiteralDecoder.DecodeWithMatchByte(m_RangeDecoder,
                                (uint)nowPos64, prevByte, m_OutWindow.GetByte(rep0));
                        else
                            b = m_LiteralDecoder.DecodeNormal(m_RangeDecoder, (uint)nowPos64, prevByte);
                        m_OutWindow.PutByte(b);
                        state.UpdateChar();
                        nowPos64++;
                    }
                    else
                    {
                        uint len;
                        if (m_IsRepDecoders[state.Index].Decode(m_RangeDecoder) == 1)
                        {
                            if (m_IsRepG0Decoders[state.Index].Decode(m_RangeDecoder) == 0)
                            {
                                if (m_IsRep0LongDecoders[(state.Index << kNumPosStatesBitsMax) + posState].Decode(m_RangeDecoder) == 0)
                                {
                                    state.UpdateShortRep();
                                    m_OutWindow.PutByte(m_OutWindow.GetByte(rep0));
                                    nowPos64++;
                                    continue;
                                }
                            }
                            else
                            {
                                UInt32 distance;
                                if (m_IsRepG1Decoders[state.Index].Decode(m_RangeDecoder) == 0)
                                {
                                    distance = rep1;
                                }
                                else
                                {
                                    if (m_IsRepG2Decoders[state.Index].Decode(m_RangeDecoder) == 0)
                                        distance = rep2;
                                    else
                                    {
                                        distance = rep3;
                                        rep3 = rep2;
                                    }
                                    rep2 = rep1;
                                }
                                rep1 = rep0;
                                rep0 = distance;
                            }
                            len = m_RepLenDecoder.Decode(m_RangeDecoder, posState) + kMatchMinLen;
                            state.UpdateRep();
                        }
                        else
                        {
                            rep3 = rep2;
                            rep2 = rep1;
                            rep1 = rep0;
                            len = kMatchMinLen + m_LenDecoder.Decode(m_RangeDecoder, posState);
                            state.UpdateMatch();
                            uint posSlot = m_PosSlotDecoder[GetLenToPosState(len)].Decode(m_RangeDecoder);
                            if (posSlot >= kStartPosModelIndex)
                            {
                                int numDirectBits = (int)((posSlot >> 1) - 1);
                                rep0 = ((2 | (posSlot & 1)) << numDirectBits);
                                if (posSlot < kEndPosModelIndex)
                                    rep0 += BitTreeDecoder.ReverseDecode(m_PosDecoders,
                                            rep0 - posSlot - 1, m_RangeDecoder, numDirectBits);
                                else
                                {
                                    rep0 += (m_RangeDecoder.DecodeDirectBits(
                                        numDirectBits - kNumAlignBits) << kNumAlignBits);
                                    rep0 += m_PosAlignDecoder.ReverseDecode(m_RangeDecoder);
                                }
                            }
                            else
                                rep0 = posSlot;
                        }
                        if (rep0 >= nowPos64 || rep0 >= m_DictionarySizeCheck)
                        {
                            if (rep0 == 0xFFFFFFFF)
                                break;
                        }
                        m_OutWindow.CopyBlock(rep0, len);
                        nowPos64 += len;
                    }
                }
            }
            m_OutWindow.Flush();
            m_OutWindow.ReleaseStream();
            m_RangeDecoder.ReleaseStream();
        }

        public void SetDecoderProperties(byte[] properties)
        {
            int lc = properties[0] % 9;
            int remainder = properties[0] / 9;
            int lp = remainder % 5;
            int pb = remainder / 5;
            UInt32 dictionarySize = 0;
            for (int i = 0; i < 4; i++)
                dictionarySize += ((UInt32)(properties[1 + i])) << (i * 8);
            SetDictionarySize(dictionarySize);
            SetLiteralProperties(lp, lc);
            SetPosBitsProperties(pb);
        }
    }
}

static class Encryptions
{

    static Assembly datAsm;
    static Assembly Resources(object sender, ResolveEventArgs args)
    {
        if (datAsm == null)
        {
            Stream str = typeof(Exception).Assembly.GetManifestResourceStream(Mutation.Key0S);
            byte[] dat = new byte[str.Length];
            str.Read(dat, 0, dat.Length);
            byte k = (byte)Mutation.Key0I;
            for (int i = 0; i < dat.Length; i++)
            {
                dat[i] = (byte)(dat[i] ^ k + 13); //CHANGED DATA
                k = (byte)((k * Mutation.Key1I) % 0x100);
            }

            var s = new MemoryStream(dat);
            Lzma.LzmaDecoder decoder = new Lzma.LzmaDecoder();
            byte[] prop = new byte[5];
            s.Read(prop, 0, 5);
            decoder.SetDecoderProperties(prop);
            long outSize = 0;
            for (int i = 0; i < 8; i++)
            {
                int v = s.ReadByte();
                if (v < 0)
                    throw (new Exception());
                outSize |= ((long)(byte)v) << (8 * i);
            }
            byte[] b = new byte[outSize];
            var z = new MemoryStream(b, true);
            long compressedSize = s.Length - 13;
            decoder.Code(s, z, compressedSize, outSize);

            z.Position = 0;
            using (BinaryReader rdr = new BinaryReader(z))
            {
                dat = rdr.ReadBytes(rdr.ReadInt32());
                datAsm = System.Reflection.Assembly.Load(dat);
                Buffer.BlockCopy(new byte[dat.Length], 0, dat, 0, dat.Length);
            }
        }
        if (Array.IndexOf(datAsm.GetManifestResourceNames(), args.Name) == -1)
            return null;
        else
            return datAsm;
    }

    //private static string SafeStrings(int id)
    //{
    //    Dictionary<int, string> hashTbl;
    //    if ((hashTbl = AppDomain.CurrentDomain.GetData("PADDINGPADDINGPADDING") as Dictionary<int, string>) == null)
    //    {
    //        AppDomain.CurrentDomain.SetData("PADDINGPADDINGPADDING", hashTbl = new Dictionary<int, string>());
    //        MemoryStream stream = new MemoryStream();
    //        Assembly asm = Assembly.GetCallingAssembly();
    //        using (DeflateStream str = new DeflateStream(asm.GetManifestResourceStream("PADDINGPADDINGPADDING"), CompressionMode.Decompress))
    //        {
    //            byte[] dat = new byte[0x1000];
    //            int read = str.Read(dat, 0, 0x1000);
    //            do
    //            {
    //                stream.Write(dat, 0, read);
    //                read = str.Read(dat, 0, 0x1000);
    //            }
    //            while (read != 0);
    //        }
    //        AppDomain.CurrentDomain.SetData("PADDINGPADDINGPADDINGPADDING", stream.ToArray());
    //    }
    //    string ret;
    //    int mdTkn = new StackFrame(1).GetMethod().MetadataToken;
    //    int pos = (mdTkn ^ id) - 12345678;
    //    if (!hashTbl.TryGetValue(pos, out ret))
    //    {
    //        using (BinaryReader rdr = new BinaryReader(new MemoryStream((byte[])AppDomain.CurrentDomain.GetData("PADDINGPADDINGPADDINGPADDING"))))
    //        {
    //            rdr.BaseStream.Seek(pos, SeekOrigin.Begin);
    //            int len = (int)((~rdr.ReadUInt32()) ^ 87654321);
    //            byte[] b = rdr.ReadBytes(len);

    //            ///////////////////

    //            uint seed = 88888888;
    //            ushort _m = (ushort)(seed >> 16);
    //            ushort _c = (ushort)(seed & 0xffff);
    //            ushort m = _c; ushort c = _m;
    //            byte[] k = new byte[b.Length];
    //            for (int i = 0; i < k.Length; i++)
    //            {
    //                k[i] = (byte)((seed * m + c) % 0x100);
    //                m = (ushort)((seed * m + _m) % 0x10000);
    //                c = (ushort)((seed * c + _c) % 0x10000);
    //            }

    //            int key = 0;
    //            for (int i = 0; i < b.Length; i++)
    //            {
    //                byte o = b[i];
    //                b[i] = (byte)(b[i] ^ (key / k[i]));
    //                key += o;
    //            }
    //            hashTbl[pos] = (ret = Encoding.UTF8.GetString(b));
    //            ///////////////////
    //        }
    //    }
    //    return ret;
    //}
    //private static string Strings(int id)
    //{
    //    Dictionary<int, string> hashTbl;
    //    if ((hashTbl = AppDomain.CurrentDomain.GetData("PADDINGPADDINGPADDING") as Dictionary<int, string>) == null)
    //    {
    //        AppDomain.CurrentDomain.SetData("PADDINGPADDINGPADDING", hashTbl = new Dictionary<int, string>());
    //        MemoryStream stream = new MemoryStream();
    //        Assembly asm = Assembly.GetCallingAssembly();
    //        using (DeflateStream str = new DeflateStream(asm.GetManifestResourceStream("PADDINGPADDINGPADDING"), CompressionMode.Decompress))
    //        {
    //            byte[] dat = new byte[0x1000];
    //            int read = str.Read(dat, 0, 0x1000);
    //            do
    //            {
    //                stream.Write(dat, 0, read);
    //                read = str.Read(dat, 0, 0x1000);
    //            }
    //            while (read != 0);
    //        }
    //        AppDomain.CurrentDomain.SetData("PADDINGPADDINGPADDINGPADDING", stream.ToArray());
    //    }
    //    string ret;
    //    int mdTkn = new StackFrame(1).GetMethod().MetadataToken;
    //    int pos = (mdTkn ^ id) - 12345678;
    //    if (!hashTbl.TryGetValue(pos, out ret))
    //    {
    //        using (BinaryReader rdr = new BinaryReader(new MemoryStream((byte[])AppDomain.CurrentDomain.GetData("PADDINGPADDINGPADDINGPADDING"))))
    //        {
    //            rdr.BaseStream.Seek(pos, SeekOrigin.Begin);
    //            int len = (int)((~rdr.ReadUInt32()) ^ 87654321);

    //            ///////////////////
    //            byte[] f = new byte[(len + 7) & ~7];

    //            for (int i = 0; i < f.Length; i++)
    //            {
    //                Poly.PolyStart();
    //                int count = 0;
    //                int shift = 0;
    //                byte b;
    //                do
    //                {
    //                    b = rdr.ReadByte();
    //                    count |= (b & 0x7F) << shift;
    //                    shift += 7;
    //                } while ((b & 0x80) != 0);

    //                f[i] = (byte)Poly.PlaceHolder(count);
    //            }

    //            hashTbl[pos] = (ret = Encoding.Unicode.GetString(f, 0, len));
    //            ///////////////////
    //        }
    //    }
    //    return ret;
    //}

    public static int PlaceHolder(int val) { return 0; }

    static Dictionary<uint, object> constTbl;
    static byte[] constBuffer;
    static void Initialize()
    {
        constTbl = new Dictionary<uint, object>();
        var s = new MemoryStream();
        Assembly asm = Assembly.GetExecutingAssembly();
        var x = asm.GetManifestResourceStream(Encoding.UTF8.GetString(BitConverter.GetBytes(Mutation.Key0I)));

        var method = MethodBase.GetCurrentMethod();
        int q = Mutation.Key0Delayed ^ method.MetadataToken;
        var key = method.Module.ResolveSignature(q);

        var str = new CryptoStream(x,
            new RijndaelManaged().CreateDecryptor(key, MD5.Create().ComputeHash(key)), CryptoStreamMode.Read);
        {
            byte[] dat = new byte[0x1000];
            int read = str.Read(dat, 0, 0x1000);
            do
            {
                s.Write(dat, 0, read);
                read = str.Read(dat, 0, 0x1000);
            }
            while (read != 0);
        }
        str.Dispose();
        s.Position = 0;

        Lzma.LzmaDecoder decoder = new Lzma.LzmaDecoder();
        byte[] prop = new byte[5];
        s.Read(prop, 0, 5);
        decoder.SetDecoderProperties(prop);
        long outSize = 0;
        for (int i = 0; i < 8; i++)
        {
            int v = s.ReadByte();
            if (v < 0)
                throw (new Exception());
            outSize |= ((long)(byte)v) << (8 * i);
        }
        byte[] b = new byte[outSize];
        long compressedSize = s.Length - 13;
        decoder.Code(s, new MemoryStream(b, true), compressedSize, outSize);

        s = new MemoryStream();
        BinaryWriter wtr = new BinaryWriter(s);
        {
            int i = 0;
            while (i < b.Length)
            {
                int count = 0;
                int shift = 0;
                byte c;
                do
                {
                    c = b[i++];
                    count |= (c & 0x7F) << shift;
                    shift += 7;
                } while ((c & 0x80) != 0);

                count = Mutation.Placeholder(count);
                wtr.Write((byte)count);
            }
        }
        s.Dispose();

        constBuffer = s.ToArray();
    }
    static void InitializeSafe()
    {

        constTbl = new Dictionary<uint, object>();
        var s = new MemoryStream();
        Assembly asm = Assembly.GetExecutingAssembly();
        var x = asm.GetManifestResourceStream(Encoding.UTF8.GetString(BitConverter.GetBytes(Mutation.Key0I)));
        byte[] buff = new byte[x.Length];
        x.Read(buff, 0, buff.Length);

        var method = MethodBase.GetCurrentMethod();
        //int ok = Mutation.Key0Delayed;
        //int token = method.DeclaringType.GetFields()[0].MetadataToken;
        var key = method.Module.ResolveSignature((int)(Mutation.Key0Delayed ^ method.MetadataToken));

        uint seed = BitConverter.ToUInt32(key, 0xc) * (uint)Mutation.Key0I;

        Random rng = new Random((int)seed);

        byte[] rBytes = new byte[buff.Length];

        // REMOVE THIS FOR DLL MODULES!!!
        /*StackFrame[] st = new StackTrace(0).GetFrames();
        if (st.Length > 1)
            if (st[0].GetMethod().Module.Assembly != Assembly.GetExecutingAssembly() || st[0].GetMethod().Module.Assembly != Assembly.GetEntryAssembly())
                throw new BadImageFormatException();

        */


        for (int i = 0; i < buff.Length; i++)
        {
            rBytes[i] = (byte)((byte)buff[i] ^ (byte)rng.Next(256));
        }

        // old dec function
        /*ushort _m = (ushort)(seed >> 16);
        ushort _c = (ushort)(seed & 0xffff);
        ushort m = _c; ushort c = _m;
        for (int i = 0; i < buff.Length; i++)
        {
            buff[i] ^= (byte)((seed * m + c) % 0x100);
            m = (ushort)((seed * m + _m) % 0x10000);
            c = (ushort)((seed * c + _c) % 0x10000);
        }*/


        var str = new CryptoStream(new MemoryStream(rBytes),
            new RijndaelManaged().CreateDecryptor(key, MD5.Create().ComputeHash(key)), CryptoStreamMode.Read);
        {
            byte[] dat = new byte[0x1000];
            int read = str.Read(dat, 0, 0x1000);
            do
            {
                s.Write(dat, 0, read);
                read = str.Read(dat, 0, 0x1000);
            }
            while (read != 0);
        }

        Lzma.LzmaDecoder decoder = new Lzma.LzmaDecoder();
        byte[] prop = new byte[5];
        s.Position = 0;
        s.Read(prop, 0, 5);
        decoder.SetDecoderProperties(prop);
        long outSize = 0;
        for (int i = 0; i < 8; i++)
        {
            int v = s.ReadByte();
            if (v < 0)
                throw (new Exception());
            outSize |= ((long)(byte)v) << (8 * i);
        }
        byte[] b = new byte[outSize];
        long compressedSize = s.Length - 13;



        // REMOVE THIS FOR DLL MODULES!!!
        /*st = new StackTrace(0).GetFrames();
        if (st.Length > 1)
            if (st[0].GetMethod().Module.Assembly != Assembly.GetExecutingAssembly() || st[0].GetMethod().Module.Assembly != Assembly.GetEntryAssembly())
                throw new BadImageFormatException();*/



        decoder.Code(s, new MemoryStream(b, true), compressedSize, outSize);

        str.Dispose();

        constBuffer = b;
    }
    static T Constants<T>(uint a, ulong b)
    {
        //Assembly asm = typeof(Assembly).Assembly.ManifestModule.

        StackFrame frame = new StackFrame(1);
        Type getType = frame.GetMethod().DeclaringType;
        if (getType != null && getType.Assembly != Type.GetTypeFromHandle(Mutation.DeclaringType()).Assembly)
        {
            object ah = null;
            byte[] shit = Encoding.UTF8.GetBytes("Good luck with that...");
            if (typeof(T) == typeof(string))
            {
                ah = Encoding.UTF8.GetString(shit);
            }
            else
            {
                new Random().NextBytes(shit);
                var t = new T[1];
                Buffer.BlockCopy(shit, 0, t, 0, Marshal.SizeOf(default(T)));
                ah = t[0];
            }
            return (T)ah;
        }
        
            //Environment.FailFast(null);
        /*Assembly realAssembly = (Assembly)typeof(Assembly).GetMethod("GetExecutingAssembly", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        
        if (realAssembly != Assembly.GetCallingAssembly())
        {
            if (typeof(T) == typeof(string))
            {
                object stra = Guid.NewGuid().ToString();
                return (T)stra;

            }
            else
            {
                var t = new T[1];
                t[0] = (T)(object)new Random().Next(int.MinValue, int.MaxValue);
                return (T)t[0];
            }
        }*/

        object ret;
        uint x = (uint)(Type.GetTypeFromHandle(Mutation.DeclaringType()).MetadataToken * a);
        ulong h = (ulong)Mutation.Key0L * x;
        ulong h1 = (ulong)Mutation.Key1L;
        ulong h2 = (ulong)Mutation.Key2L;
        h1 = h1 * h;
        h2 = h2 * h;
        h = h * h;

        ulong hash = 0xCBF29CE484222325;
        while (h != 0)
        {
            hash *= 0x100000001B3;
            hash = (hash ^ h) + (h1 ^ h2) * (uint)Mutation.Key0I;
            h1 *= 0x811C9DC5;
            h2 *= 0xA2CEBAB2;
            h >>= 8;
        }
        ulong dat = hash ^ b;
        uint pos = (uint)(dat >> 32);
        uint len = (uint)dat;
        lock (constTbl)
        {
            if (!constTbl.TryGetValue(pos, out ret))
            {
                byte[] bs = new byte[len];
                Array.Copy(constBuffer, (int)pos, bs, 0, len);
                var method = MethodBase.GetCurrentMethod();
                byte[] key = BitConverter.GetBytes(method.MetadataToken ^ Mutation.Key0Delayed);
                for (int i = 0; i < bs.Length; i++)
                    bs[i] ^= key[(pos + i) % 4];

                frame = new StackFrame(1);
                getType = frame.GetMethod().DeclaringType;
                if (getType != null && getType.Assembly != Type.GetTypeFromHandle(Mutation.DeclaringType()).Assembly)
                {
                    bs[0] = 0xff;
                }

                if (typeof(T) == typeof(string))
                    ret = Encoding.UTF8.GetString(bs);
                else
                {
                    var t = new T[1];
                    Buffer.BlockCopy(bs, 0, t, 0, Marshal.SizeOf(default(T)));
                    ret = t[0];
                }
                constTbl[pos] = ret;
            }
        }
        return (T)ret;
    }
}

static class AntiDumping
{
    [DllImportAttribute("kernel32.dll")]
    static unsafe extern bool VirtualProtect(byte* lpAddress, int dwSize, uint flNewProtect, out uint lpflOldProtect);

    public static unsafe void Initialize()
    {
        uint old;
        byte* bas = (byte*)Marshal.GetHINSTANCE(typeof(AntiDumping).Module);
        byte* ptr = bas + 0x3c;
        byte* ptr2;
        ptr = ptr2 = bas + *(uint*)ptr;
        ptr += 0x6;
        ushort sectNum = *(ushort*)ptr;
        ptr += 14;
        ushort optSize = *(ushort*)ptr;
        ptr = ptr2 = ptr + 0x4 + optSize;

        byte* @new = stackalloc byte[11];// (byte*)Marshal.AllocHGlobal(11);
        if (typeof(AntiDumping).Module.FullyQualifiedName[0] != '<')   //Mapped
        {
            //VirtualProtect(ptr - 16, 8, 0x40, out old);
            //*(uint*)(ptr - 12) = 0;
            byte* mdDir = bas + *(uint*)(ptr - 16);
            //*(uint*)(ptr - 16) = 0;

            if (*(uint*)(ptr - 0x78) != 0)
            {
                byte* importDir = bas + *(uint*)(ptr - 0x78);
                byte* oftMod = bas + *(uint*)importDir;
                byte* modName = bas + *(uint*)(importDir + 12);
                byte* funcName = bas + *(uint*)oftMod + 2;
                VirtualProtect(modName, 11, 0x40, out old);

                *(uint*)@new = 0x6c64746e;
                *((uint*)@new + 1) = 0x6c642e6c;
                *((ushort*)@new + 4) = 0x006c;
                *(@new + 10) = 0;

                for (int i = 0; i < 11; i++)
                    *(modName + i) = *(@new + i);

                VirtualProtect(funcName, 11, 0x40, out old);

                *(uint*)@new = 0x6f43744e;
                *((uint*)@new + 1) = 0x6e69746e;
                *((ushort*)@new + 4) = 0x6575;
                *(@new + 10) = 0;

                for (int i = 0; i < 11; i++)
                    *(funcName + i) = *(@new + i);
            }

            for (int i = 0; i < sectNum; i++)
            {
                VirtualProtect(ptr, 8, 0x40, out old);
                Marshal.Copy(new byte[8], 0, (IntPtr)ptr, 8);
                ptr += 0x28;
            }
            VirtualProtect(mdDir, 0x48, 0x40, out old);
            byte* mdHdr = bas + *(uint*)(mdDir + 8);
            *(uint*)mdDir = 0;
            *((uint*)mdDir + 1) = 0;
            *((uint*)mdDir + 2) = 0;
            *((uint*)mdDir + 3) = 0;

            VirtualProtect(mdHdr, 4, 0x40, out old);
            *(uint*)mdHdr = 0;
            mdHdr += 12;
            mdHdr += *(uint*)mdHdr;
            mdHdr = (byte*)(((uint)mdHdr + 7) & ~3);
            mdHdr += 2;
            ushort numOfStream = *mdHdr;
            mdHdr += 2;
            for (int i = 0; i < numOfStream; i++)
            {
                VirtualProtect(mdHdr, 8, 0x40, out old);
                //*(uint*)mdHdr = 0;
                mdHdr += 4;
                //*(uint*)mdHdr = 0;
                mdHdr += 4;
                for (int ii = 0; ii < 8; ii++)
                {
                    VirtualProtect(mdHdr, 4, 0x40, out old);
                    *mdHdr = 0; mdHdr++;
                    if (*mdHdr == 0)
                    {
                        mdHdr += 3;
                        break;
                    }
                    *mdHdr = 0; mdHdr++;
                    if (*mdHdr == 0)
                    {
                        mdHdr += 2;
                        break;
                    }
                    *mdHdr = 0; mdHdr++;
                    if (*mdHdr == 0)
                    {
                        mdHdr += 1;
                        break;
                    }
                    *mdHdr = 0; mdHdr++;
                }
            }
        }
        else   //Flat
        {
            //VirtualProtect(ptr - 16, 8, 0x40, out old);
            //*(uint*)(ptr - 12) = 0;
            uint mdDir = *(uint*)(ptr - 16);
            //*(uint*)(ptr - 16) = 0;
            uint importDir = *(uint*)(ptr - 0x78);

            uint[] vAdrs = new uint[sectNum];
            uint[] vSizes = new uint[sectNum];
            uint[] rAdrs = new uint[sectNum];
            for (int i = 0; i < sectNum; i++)
            {
                VirtualProtect(ptr, 8, 0x40, out old);
                Marshal.Copy(new byte[8], 0, (IntPtr)ptr, 8);
                vAdrs[i] = *(uint*)(ptr + 12);
                vSizes[i] = *(uint*)(ptr + 8);
                rAdrs[i] = *(uint*)(ptr + 20);
                ptr += 0x28;
            }


            if (importDir != 0)
            {
                for (int i = 0; i < sectNum; i++)
                    if (vAdrs[i] < importDir && importDir < vAdrs[i] + vSizes[i])
                    {
                        importDir = importDir - vAdrs[i] + rAdrs[i];
                        break;
                    }
                byte* importDirPtr = bas + importDir;
                uint oftMod = *(uint*)importDirPtr;
                for (int i = 0; i < sectNum; i++)
                    if (vAdrs[i] < oftMod && oftMod < vAdrs[i] + vSizes[i])
                    {
                        oftMod = oftMod - vAdrs[i] + rAdrs[i];
                        break;
                    }
                byte* oftModPtr = bas + oftMod;
                uint modName = *(uint*)(importDirPtr + 12);
                for (int i = 0; i < sectNum; i++)
                    if (vAdrs[i] < modName && modName < vAdrs[i] + vSizes[i])
                    {
                        modName = modName - vAdrs[i] + rAdrs[i];
                        break;
                    }
                uint funcName = *(uint*)oftModPtr + 2;
                for (int i = 0; i < sectNum; i++)
                    if (vAdrs[i] < funcName && funcName < vAdrs[i] + vSizes[i])
                    {
                        funcName = funcName - vAdrs[i] + rAdrs[i];
                        break;
                    }
                VirtualProtect(bas + modName, 11, 0x40, out old);

                *(uint*)@new = 0x6c64746e;
                *((uint*)@new + 1) = 0x6c642e6c;
                *((ushort*)@new + 4) = 0x006c;
                *(@new + 10) = 0;

                for (int i = 0; i < 11; i++)
                    *(bas + modName + i) = *(@new + i);

                VirtualProtect(bas + funcName, 11, 0x40, out old);

                *(uint*)@new = 0x6f43744e;
                *((uint*)@new + 1) = 0x6e69746e;
                *((ushort*)@new + 4) = 0x6575;
                *(@new + 10) = 0;

                for (int i = 0; i < 11; i++)
                    *(bas + funcName + i) = *(@new + i);
            }


            for (int i = 0; i < sectNum; i++)
                if (vAdrs[i] < mdDir && mdDir < vAdrs[i] + vSizes[i])
                {
                    mdDir = mdDir - vAdrs[i] + rAdrs[i];
                    break;
                }
            byte* mdDirPtr = bas + mdDir;
            VirtualProtect(mdDirPtr, 0x48, 0x40, out old);
            uint mdHdr = *(uint*)(mdDirPtr + 8);
            for (int i = 0; i < sectNum; i++)
                if (vAdrs[i] < mdHdr && mdHdr < vAdrs[i] + vSizes[i])
                {
                    mdHdr = mdHdr - vAdrs[i] + rAdrs[i];
                    break;
                }
            *(uint*)mdDirPtr = 0;
            *((uint*)mdDirPtr + 1) = 0;
            *((uint*)mdDirPtr + 2) = 0;
            *((uint*)mdDirPtr + 3) = 0;


            byte* mdHdrPtr = bas + mdHdr;
            VirtualProtect(mdHdrPtr, 4, 0x40, out old);
            *(uint*)mdHdrPtr = 0;
            mdHdrPtr += 12;
            mdHdrPtr += *(uint*)mdHdrPtr;
            mdHdrPtr = (byte*)(((uint)mdHdrPtr + 7) & ~3);
            mdHdrPtr += 2;
            ushort numOfStream = *mdHdrPtr;
            mdHdrPtr += 2;
            for (int i = 0; i < numOfStream; i++)
            {
                VirtualProtect(mdHdrPtr, 8, 0x40, out old);
                //*(uint*)mdHdrPtr = 0;
                mdHdrPtr += 4;
                //*(uint*)mdHdrPtr = 0;
                mdHdrPtr += 4;
                for (int ii = 0; ii < 8; ii++)
                {
                    VirtualProtect(mdHdrPtr, 4, 0x40, out old);
                    *mdHdrPtr = 0; mdHdrPtr++;
                    if (*mdHdrPtr == 0)
                    {
                        mdHdrPtr += 3;
                        break;
                    }
                    *mdHdrPtr = 0; mdHdrPtr++;
                    if (*mdHdrPtr == 0)
                    {
                        mdHdrPtr += 2;
                        break;
                    }
                    *mdHdrPtr = 0; mdHdrPtr++;
                    if (*mdHdrPtr == 0)
                    {
                        mdHdrPtr += 1;
                        break;
                    }
                    *mdHdrPtr = 0; mdHdrPtr++;
                }
            }
        }
        //Marshal.FreeHGlobal((IntPtr)@new);
    }
}