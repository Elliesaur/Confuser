﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Security.Cryptography;

static class CompressShell
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

    class LzmaDecoder
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


    static Assembly DecryptAsm(object sender, ResolveEventArgs e)
    {
        byte[] b = Encoding.UTF8.GetBytes(e.Name);
        for (int i = 0; i < b.Length; i++)
            b[i] = (byte)(b[i] ^ Mutation.Key2I ^ i);
        string resName = Encoding.UTF8.GetString(b);
        Stream str = typeof(CompressShell).Assembly.GetManifestResourceStream(resName);
        if (str != null)
        {
            byte[] asmDat;
            using (BinaryReader rdr = new BinaryReader(str))
            {
                asmDat = rdr.ReadBytes((int)str.Length);
            }
            asmDat = Decrypt(asmDat);
            var asm = Assembly.Load(asmDat);
            byte[] over = new byte[asmDat.Length];
            Buffer.BlockCopy(over, 0, asmDat, 0, asmDat.Length);

            return asm;
        }
        return null;
    }
    static Assembly ResolveResource(object sender, ResolveEventArgs e)
    {
        byte[] b = Encoding.UTF8.GetBytes(e.Name);
        for (int i = 0; i < b.Length; i++)
            b[i] = (byte)(b[i] ^ Mutation.Key1I ^ i);
        string resName = Encoding.UTF8.GetString(b);
        Stream str = typeof(CompressShell).Assembly.GetManifestResourceStream(resName);
        if (str != null)
        {
            byte[] asmDat;
            using (BinaryReader rdr = new BinaryReader(str))
            {
                asmDat = rdr.ReadBytes((int)str.Length);
            }
            asmDat = Decrypt(asmDat);
            var asm = Assembly.Load(asmDat);
            byte[] over = new byte[asmDat.Length];
            Buffer.BlockCopy(over, 0, asmDat, 0, asmDat.Length);

            return asm;
        }
        return null;
    }

    static byte[] Decrypt(byte[] asm)
    {
        byte[] dat;
        byte[] iv;
        byte[] key;
        using (BinaryReader rdr = new BinaryReader(new MemoryStream(asm)))
        {
            dat = rdr.ReadBytes(rdr.ReadInt32());
            iv = rdr.ReadBytes(rdr.ReadInt32());
            key = rdr.ReadBytes(rdr.ReadInt32());
        }
        int key0 = Mutation.Key0I;
        for (int j = 0; j < key.Length; j += 4)
        {
            key[j + 0] ^= (byte)((key0 & 0x000000ff ) + 51 >> 0);
            key[j + 1] ^= (byte)((key0 & 0x0000ff00) - 51 >> 8);
            key[j + 2] ^= (byte)((key0 & 0x00ff0000) + 51 >> 16);
            key[j + 3] ^= (byte)((key0 & 0xff000000) - 51 >> 24);
        }
        RijndaelManaged rijn = new RijndaelManaged();
        using (var s = new CryptoStream(new MemoryStream(dat), rijn.CreateDecryptor(key, iv), CryptoStreamMode.Read))
        {
            byte[] l = new byte[4];
            s.Read(l, 0, 4);
            uint len = BitConverter.ToUInt32(l, 0);

            LzmaDecoder decoder = new LzmaDecoder();
            byte[] prop = new byte[5];
            s.Read(prop, 0, 5);
            decoder.SetDecoderProperties(prop);
            long outSize = 0;
            for (int i = 0; i < 8; i++)
            {
                int v = s.ReadByte();
                if (v < 0)
                    throw (new Exception("Can't Read 1"));
                outSize |= ((long)(byte)v) << (8 * i);
            }
            byte[] ret = new byte[outSize];
            long compressedSize = len - 13;
            decoder.Code(s, new MemoryStream(ret, true), compressedSize, outSize);

            return ret;
        }
    }

    static string Res = Mutation.Key0S;
    static ulong Rid = (ulong)Mutation.Key0L;
    static Module Mod;

    static ulong modPow(ulong bas, ulong pow, ulong mod)
    {
        ulong m = 1;
        while (pow > 0)
        {
            if ((pow & 1) != 0)
                m = (m * bas) % mod;
            pow = pow >> 1;
            bas = (bas * bas) % mod;
        }
        return m;
    }
    [STAThread]
    static int Main(string[] args)
    {
        Assembly asm = Assembly.GetEntryAssembly();
        Stream str = asm.GetManifestResourceStream(Res);
        byte[] asmDat;
        using (BinaryReader rdr = new BinaryReader(str))
            asmDat = rdr.ReadBytes((int)str.Length);
        asmDat = Decrypt(asmDat);

        Mod = asm.LoadModule("___.netmodule", asmDat);
        byte[] over = new byte[asmDat.Length];
        Buffer.BlockCopy(over, 0, asmDat, 0, asmDat.Length);

        AppDomain.CurrentDomain.AssemblyResolve += DecryptAsm;
        MethodBase m = Mod.ResolveMethod(0x06000000 | (int)modPow(Rid, 0x47, (ulong)Mutation.Key1L));
        object ret;
        if (m.GetParameters().Length == 1)
            ret = m.Invoke(null, new object[] { args });
        else
            ret = m.Invoke(null, null);
        if (ret is int)
            return (int)ret;
        else
            return 0;
    }
}