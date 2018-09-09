using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Reflection;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Reflection.Emit;
using System.Security;
using System.Security.Permissions;
using System.Collections.Generic;
using System.Diagnostics;

static class AntiTamperJIT
{
    static ulong s;
    static ulong l;
    public static unsafe void Initialize()
    {
        Module mod = typeof(AntiTamperJIT).Module;
        IntPtr modPtr = Marshal.GetHINSTANCE(mod);
        s = (ulong)modPtr.ToInt64();
        if (modPtr == (IntPtr)(-1)) Environment.FailFast(null);
        string fq = mod.FullyQualifiedName;
        bool mapped = fq[0] != '<'; //<Unknown>
        Stream stream;
        stream = new UnmanagedMemoryStream((byte*)modPtr.ToPointer(), 0xfffffff, 0xfffffff, FileAccess.ReadWrite);

        byte[] buff;
        int checkSumOffset;
        ulong checkSum;
        byte[] iv;
        byte[] dats;
        int sn;
        int snLen;
        using (BinaryReader rdr = new BinaryReader(stream))
        {
            stream.Seek(0x3c, SeekOrigin.Begin);
            uint offset = rdr.ReadUInt32();
            stream.Seek(offset, SeekOrigin.Begin);
            stream.Seek(0x6, SeekOrigin.Current);
            uint sections = rdr.ReadUInt16();
            stream.Seek(0xC, SeekOrigin.Current);
            uint optSize = rdr.ReadUInt16();
            stream.Seek(offset = offset + 0x18, SeekOrigin.Begin);  //Optional hdr
            bool pe32 = (rdr.ReadUInt16() == 0x010b);
            stream.Seek(0x3e, SeekOrigin.Current);
            checkSumOffset = (int)stream.Position;
            uint md = rdr.ReadUInt32() ^ (uint)Mutation.Key0I;
            if (md == (uint)Mutation.Key0I)
                return;

            stream.Seek(offset = offset + optSize, SeekOrigin.Begin);  //sect hdr
            uint datLoc = 0;
            for (int i = 0; i < sections; i++)
            {
                int h = 0;
                for (int j = 0; j < 8; j++)
                {
                    byte chr = rdr.ReadByte();
                    if (chr != 0) h += chr;
                }
                uint vSize = rdr.ReadUInt32();
                uint vLoc = rdr.ReadUInt32();
                uint rSize = rdr.ReadUInt32();
                uint rLoc = rdr.ReadUInt32();
                if (h == Mutation.Key1I)
                    datLoc = mapped ? vLoc : rLoc;

                if (!mapped && md > vLoc && md < vLoc + vSize)
                    md = md - vLoc + rLoc;

                if (mapped && vSize + vLoc > l) l = vSize + vLoc;
                else if (rSize + rLoc > l) l = rSize + rLoc;

                stream.Seek(0x10, SeekOrigin.Current);
            }

            stream.Seek(md, SeekOrigin.Begin);
            using (MemoryStream str = new MemoryStream())
            {
                stream.Position += 12;
                stream.Position += rdr.ReadUInt32() + 4;
                stream.Position += 2;

                ushort streams = rdr.ReadUInt16();

                for (int i = 0; i < streams; i++)
                {
                    uint pos = rdr.ReadUInt32() + md;
                    uint size = rdr.ReadUInt32();

                    int c = 0;
                    while (rdr.ReadByte() != 0) c++;
                    long ori = stream.Position += (((c + 1) + 3) & ~3) - (c + 1);

                    stream.Position = pos;
                    str.Write(rdr.ReadBytes((int)size), 0, (int)size);
                    stream.Position = ori;
                }

                buff = str.ToArray();
            }

            stream.Seek(datLoc, SeekOrigin.Begin);
            checkSum = rdr.ReadUInt64() ^ (ulong)Mutation.Key0L;
            sn = rdr.ReadInt32();
            snLen = rdr.ReadInt32();
            iv = rdr.ReadBytes(rdr.ReadInt32() ^ Mutation.Key2I);
            dats = rdr.ReadBytes(rdr.ReadInt32() ^ Mutation.Key3I);
        }

        byte[] md5 = MD5.Create().ComputeHash(buff);
        ulong tCs = BitConverter.ToUInt64(md5, 0) ^ BitConverter.ToUInt64(md5, 8);
        ulong* msg = stackalloc ulong[2];
        msg[0] = 0x6574707572726f43;    //Corrupte
        msg[1] = 0x0021656c69662064;    //d file!.
        if (tCs != checkSum)
            Environment.FailFast(new string((sbyte*)msg));

        byte[] b = Decrypt(buff, iv, dats);
        Buffer.BlockCopy(new byte[buff.Length], 0, buff, 0, buff.Length);
        if (b[0] != 0xd6 || b[1] != 0x6f)
            Environment.FailFast(new string((sbyte*)msg));
        byte[] dat = new byte[b.Length - 2];
        Buffer.BlockCopy(b, 2, dat, 0, dat.Length);

        data = dat;
        Hook();
        //AppDomain.CurrentDomain.ProcessExit += Dispose;
    }

    static byte[] Decrypt(byte[] buff, byte[] iv, byte[] dat)
    {
        RijndaelManaged ri = new RijndaelManaged();
        byte[] ret = new byte[dat.Length];
        MemoryStream ms = new MemoryStream(dat);
        using (CryptoStream cStr = new CryptoStream(ms, ri.CreateDecryptor(SHA256.Create().ComputeHash(buff), iv), CryptoStreamMode.Read))
        { cStr.Read(ret, 0, dat.Length); }

        SHA512 sha = SHA512.Create();
        byte[] c = sha.ComputeHash(buff);
        for (int i = 0; i < ret.Length; i += 64)
        {
            int len = ret.Length <= i + 64 ? ret.Length : i + 64;
            for (int j = i; j < len; j++)
                ret[j] ^= (byte)(c[j - i] ^ Mutation.Key6I + 13); //CHANGED DATA
            c = sha.ComputeHash(ret, i, len - i);
        }
        return ret;
    }

    static bool hasLinkInfo;
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct ICorJitInfo
    {
        public IntPtr* vfptr;
        public int* vbptr;

        public static ICorDynamicInfo* ICorDynamicInfo(ICorJitInfo* ptr)
        {
            hasLinkInfo = ptr->vbptr[10] > 0 && ptr->vbptr[10] >> 16 == 0;//!=0 and hiword byte ==0
            return (ICorDynamicInfo*)((byte*)&ptr->vbptr + ptr->vbptr[hasLinkInfo ? 10 : 9]);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct ICorDynamicInfo
    {
        public IntPtr* vfptr;
        public int* vbptr;

        public static ICorStaticInfo* ICorStaticInfo(ICorDynamicInfo* ptr)
        {
            return (ICorStaticInfo*)((byte*)&ptr->vbptr + ptr->vbptr[hasLinkInfo ? 9 : 8]);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct ICorStaticInfo
    {
        public IntPtr* vfptr;
        public int* vbptr;

        public static ICorMethodInfo* ICorMethodInfo(ICorStaticInfo* ptr)
        {
            return (ICorMethodInfo*)((byte*)&ptr->vbptr + ptr->vbptr[1]);
        }
        public static ICorModuleInfo* ICorModuleInfo(ICorStaticInfo* ptr)
        {
            return (ICorModuleInfo*)((byte*)&ptr->vbptr + ptr->vbptr[2]);
        }
        public static ICorClassInfo* ICorClassInfo(ICorStaticInfo* ptr)
        {
            return (ICorClassInfo*)((byte*)&ptr->vbptr + ptr->vbptr[3]);
        }
        public static ICorFieldInfo* ICorFieldInfo(ICorStaticInfo* ptr)
        {
            return (ICorFieldInfo*)((byte*)&ptr->vbptr + ptr->vbptr[4]);
        }
        public static ICorDebugInfo* ICorDebugInfo(ICorStaticInfo* ptr)
        {
            return (ICorDebugInfo*)((byte*)&ptr->vbptr + ptr->vbptr[5]);
        }
        public static ICorArgInfo* ICorArgInfo(ICorStaticInfo* ptr)
        {
            return (ICorArgInfo*)((byte*)&ptr->vbptr + ptr->vbptr[6]);
        }
        public static ICorLinkInfo* ICorLinkInfo(ICorStaticInfo* ptr)
        {
            return (ICorLinkInfo*)((byte*)&ptr->vbptr + ptr->vbptr[7]);
        }
        public static ICorErrorInfo* ICorErrorInfo(ICorStaticInfo* ptr)
        {
            return (ICorErrorInfo*)((byte*)&ptr->vbptr + ptr->vbptr[hasLinkInfo ? 8 : 7]);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct ICorMethodInfo
    {
        public IntPtr* vfptr;
    }
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct ICorModuleInfo
    {
        public IntPtr* vfptr;
    }
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct ICorClassInfo
    {
        public IntPtr* vfptr;
    }
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct ICorFieldInfo
    {
        public IntPtr* vfptr;
    }
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct ICorDebugInfo
    {
        public IntPtr* vfptr;
    }
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct ICorArgInfo
    {
        public IntPtr* vfptr;
    }
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct ICorLinkInfo
    {
        public IntPtr* vfptr;
    }
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct ICorErrorInfo
    {
        public IntPtr* vfptr;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    unsafe struct CORINFO_METHOD_INFO
    {
        public IntPtr ftn;
        public IntPtr scope;
        public byte* ILCode;
        public uint ILCodeSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct CORINFO_SIG_INST_x86
    {
        public uint classInstCount;
        public IntPtr* classInst;
        public uint methInstCount;
        public IntPtr* methInst;
    }
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct CORINFO_SIG_INST_x64
    {
        public uint classInstCount;
        uint pad1;
        public IntPtr* classInst;
        public uint methInstCount;
        uint pad2;
        public IntPtr* methInst;
    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct CORINFO_SIG_INFO_x86
    {
        public uint callConv;
        public IntPtr retTypeClass;
        public IntPtr retTypeSigClass;
        public byte retType;
        public byte flags;
        public ushort numArgs;
        public CORINFO_SIG_INST_x86 sigInst;
        public IntPtr args;
        public IntPtr sig;
        public IntPtr scope;
        public IntPtr token;
    }
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct CORINFO_SIG_INFO_x64
    {
        public uint callConv;
        uint pad1;
        public IntPtr retTypeClass;
        public IntPtr retTypeSigClass;
        public byte retType;
        public byte flags;
        public ushort numArgs;
        uint pad2;
        public CORINFO_SIG_INST_x64 sigInst;
        public IntPtr args;
        public IntPtr sig;
        public IntPtr scope;
        public uint token;
        uint pad3;
    }

    [StructLayout(LayoutKind.Sequential, Size = 0x18)]
    struct CORINFO_EH_CLAUSE
    {
    }
    enum InfoAccessType
    {
        VALUE,
        PVALUE,
        PPVALUE
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MethodData
    {
        public uint ILCodeSize;
        public uint MaxStack;
        public uint EHCount;
        public uint LocalVars;
        public uint Options;
    }

    [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
    unsafe delegate uint compileMethod(IntPtr self, ICorJitInfo* comp, CORINFO_METHOD_INFO* info, uint flags, byte** nativeEntry, uint* nativeSizeOfCode);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    unsafe delegate void findSig(IntPtr self, IntPtr module, uint sigTOK, IntPtr context, void* sig);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    unsafe delegate void getEHinfo(IntPtr self, IntPtr ftn, uint EHnumber, CORINFO_EH_CLAUSE* clause);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    unsafe delegate InfoAccessType constructStringLiteral(IntPtr self, IntPtr module, uint metaTok, out IntPtr pobj);


    [DllImport("kernel32.dll")]
    static extern IntPtr LoadLibrary(string lib);
    [DllImport("kernel32.dll")]
    static extern IntPtr GetProcAddress(IntPtr lib, string proc);
    [DllImport("kernel32.dll")]
    static extern bool VirtualProtect(IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);

    delegate IntPtr getJit();
    static AntiTamperJIT()
    {
        Init(RuntimeEnvironment.GetSystemVersion()[1] == '4');
    }

    static IntPtr hookPosition;
    static IntPtr original;
    static compileMethod originalDelegate;

    static bool ver;
    static unsafe void Init(bool ver)
    {
        AntiTamperJIT.ver = ver;
        ulong* ptr = stackalloc ulong[2];
        if (ver)
        {
            ptr[0] = 0x642e74696a726c63;    //clrjit.d
            ptr[1] = 0x0000000000006c6c;    //ll......
        }
        else
        {
            ptr[0] = 0x74696a726f63736d;    //mscorjit
            ptr[1] = 0x000000006c6c642e;    //.dll....
        }
        IntPtr jit = LoadLibrary(new string((sbyte*)ptr));
        ptr[0] = 0x000074694a746567;    //getJit
        getJit get = (getJit)Marshal.GetDelegateForFunctionPointer(GetProcAddress(jit, new string((sbyte*)ptr)), typeof(getJit));
        hookPosition = Marshal.ReadIntPtr(get());
        original = Marshal.ReadIntPtr(hookPosition);

        IntPtr trampoline;
        if (IntPtr.Size == 8)
        {
            trampoline = Marshal.AllocHGlobal(16);
            ulong* tptr = (ulong*)trampoline;
            tptr[0] = 0xffffffffffffb848;
            tptr[1] = 0x90909090e0ffffff;

            uint oldPl;
            VirtualProtect(trampoline, 12, 0x40, out oldPl);
            Marshal.WriteIntPtr(trampoline, 2, original);
        }
        else
        {
            trampoline = Marshal.AllocHGlobal(8);
            ulong* tptr = (ulong*)trampoline;
            tptr[0] = 0x90e0ffffffffffb8;

            uint oldPl;
            VirtualProtect(trampoline, 7, 0x40, out oldPl);
            Marshal.WriteIntPtr(trampoline, 1, original);
        }

        originalDelegate = (compileMethod)Marshal.GetDelegateForFunctionPointer(trampoline, typeof(compileMethod));
        RuntimeHelpers.PrepareDelegate(originalDelegate);
    }

    static byte[] data;

    static bool hooked;
    static compileMethod interop;
    static unsafe void Hook()
    {
        if (hooked) throw new InvalidOperationException();

        interop = new compileMethod(Interop);
        try
        {
            interop(IntPtr.Zero, null, null, 0, null, null);
        }
        catch { }

        uint oldPl;
        VirtualProtect(hookPosition, (uint)IntPtr.Size, 0x40, out oldPl);
        Marshal.WriteIntPtr(hookPosition, Marshal.GetFunctionPointerForDelegate(interop));
        VirtualProtect(hookPosition, (uint)IntPtr.Size, oldPl, out oldPl);

        hooked = true;
    }
    static unsafe void UnHook()
    {
        if (!hooked) throw new InvalidOperationException();

        uint oldPl;
        VirtualProtect(hookPosition, (uint)IntPtr.Size, 0x40, out oldPl);
        Marshal.WriteIntPtr(hookPosition, Marshal.GetFunctionPointerForDelegate(interop));
        VirtualProtect(hookPosition, (uint)IntPtr.Size, oldPl, out oldPl);

        hooked = false;
    }
    static void Dispose(object sender, EventArgs e)
    {
        if (hooked) UnHook();
    }

    unsafe class CorMethodInfoHook
    {
        public IntPtr ftn;
        public ICorMethodInfo* info;
        public ICorJitInfo* comp;
        public IntPtr* oriVfTbl;
        public IntPtr* newVfTbl;

        public CORINFO_EH_CLAUSE[] clauses;
        public getEHinfo o_getEHinfo;
        public getEHinfo n_getEHinfo;

        void hookEHInfo(IntPtr self, IntPtr ftn, uint EHnumber, CORINFO_EH_CLAUSE* clause)
        {
            if (ftn == this.ftn)
            {
                *clause = clauses[EHnumber];
            }
            else
            {
                o_getEHinfo(self, ftn, EHnumber, clause);
            }
        }

        public void Dispose()
        {
            Marshal.FreeHGlobal((IntPtr)newVfTbl);
            info->vfptr = oriVfTbl;
        }

        static int ehNum = -1;
        public static CorMethodInfoHook Hook(ICorJitInfo* comp, IntPtr ftn, CORINFO_EH_CLAUSE[] clauses)
        {
            ICorMethodInfo* mtdInfo = ICorStaticInfo.ICorMethodInfo(ICorDynamicInfo.ICorStaticInfo(ICorJitInfo.ICorDynamicInfo(comp)));
            IntPtr* vfTbl = mtdInfo->vfptr;
            const int SLOT_NUM = 0x1B;
            IntPtr* newVfTbl = (IntPtr*)Marshal.AllocHGlobal(SLOT_NUM * IntPtr.Size);
            for (int i = 0; i < SLOT_NUM; i++)
                newVfTbl[i] = vfTbl[i];
            if (ehNum == -1)
                for (int i = 0; i < SLOT_NUM; i++)
                {
                    bool isEh = true;
                    for (byte* func = (byte*)vfTbl[i]; *func != 0xe9; func++)
                        if (IntPtr.Size == 8 ?
                            (*func == 0x48 && *(func + 1) == 0x81 && *(func + 2) == 0xe9) :
                             (*func == 0x83 && *(func + 1) == 0xe9))
                        {
                            isEh = false;
                            break;
                        }
                    if (isEh)
                    {
                        ehNum = i;
                        break;
                    }
                }

            CorMethodInfoHook ret = new CorMethodInfoHook()
            {
                ftn = ftn,
                info = mtdInfo,
                comp = comp,
                clauses = clauses,
                newVfTbl = newVfTbl,
                oriVfTbl = vfTbl
            };

            ret.n_getEHinfo = new getEHinfo(ret.hookEHInfo);
            ret.o_getEHinfo = Marshal.GetDelegateForFunctionPointer(vfTbl[ehNum], typeof(getEHinfo)) as getEHinfo;
            newVfTbl[ehNum] = Marshal.GetFunctionPointerForDelegate(ret.n_getEHinfo);

            mtdInfo->vfptr = newVfTbl;
            return ret;
        }
    }
    unsafe class HandleTable
    {
        IntPtr* baseAdr;
        int size;
        IntPtr* current;
        int currentIdx;
        public HandleTable()
        {
            size = 0x10;
            baseAdr = (IntPtr*)Marshal.AllocHGlobal(size * IntPtr.Size);
            current = baseAdr;
            currentIdx = 0;
        }

        public IntPtr AddHandle(IntPtr hnd)
        {
            if (currentIdx == size)
            {
                IntPtr* newAdr = (IntPtr*)Marshal.AllocHGlobal(size * 2 * IntPtr.Size);
                for (int i = 0; i < size; i++)
                    *(newAdr + i) = *(baseAdr + i);
                current = newAdr + size;
                size *= 2;
            }
            *current = hnd;
            currentIdx++;
            return (IntPtr)(current++);
        }
    }
    unsafe class CorDynamicInfoHook
    {
        public ICorDynamicInfo* info;
        public ICorJitInfo* comp;
        public IntPtr* oriVfTbl;
        public IntPtr* newVfTbl;

        public constructStringLiteral o_constructStringLiteral;
        public constructStringLiteral n_constructStringLiteral;

        static IntPtr Obj2Ptr(object obj) { return (IntPtr)0; }    //Placeholder

        static HandleTable tbl = new HandleTable();
        Dictionary<uint, IntPtr> gcHnds = new Dictionary<uint, IntPtr>();
        List<GCHandle> pins = new List<GCHandle>();
        InfoAccessType hookConstructStr(IntPtr self, IntPtr module, uint metaTok, out IntPtr pobj)
        {
            if ((metaTok & 0x00800000) != 0)
            {
                uint offset = metaTok & 0x007FFFFF;

                if (!gcHnds.ContainsKey(offset))
                {
                    uint length = (uint)(BitConverter.ToUInt32(data, (int)offset) & ~1);
                    if (length < 1)
                    {
                        pins.Add(GCHandle.Alloc(string.Empty, GCHandleType.Pinned));
                        gcHnds[offset] = tbl.AddHandle(Obj2Ptr(string.Empty));
                    }

                    var chars = new char[length / 2];

                    for (uint i = offset + 4, j = 0; i < offset + 4 + length; i += 2)
                        chars[j++] = (char)((data[i] | (data[i + 1] << 8)) ^ Mutation.Key5I);

                    string c = new string(chars);
                    pins.Add(GCHandle.Alloc(c, GCHandleType.Pinned));
                    gcHnds[offset] = tbl.AddHandle(Obj2Ptr(c));
                }
                pobj = gcHnds[offset];
                return InfoAccessType.PVALUE;
            }
            InfoAccessType ret = o_constructStringLiteral(self, module, metaTok, out pobj);
            return ret;
        }

        public void Dispose()
        {
            Marshal.FreeHGlobal((IntPtr)newVfTbl);
            info->vfptr = oriVfTbl;
        }

        static int ctNum = -1;
        public static CorDynamicInfoHook Hook(ICorJitInfo* comp)
        {
            ICorDynamicInfo* dynInfo = ICorJitInfo.ICorDynamicInfo(comp);
            IntPtr* vfTbl = dynInfo->vfptr;
            const int SLOT_NUM = 0x27;
            IntPtr* newVfTbl = (IntPtr*)Marshal.AllocHGlobal(SLOT_NUM * IntPtr.Size);
            for (int i = 0; i < SLOT_NUM; i++)
                newVfTbl[i] = vfTbl[i];
            if (ctNum == -1)
                for (int i = 0; i < SLOT_NUM; i++)
                {
                    bool overrided = true;
                    for (byte* func = (byte*)vfTbl[i]; *func != 0xe9; func++)
                        if (IntPtr.Size == 8 ?
                            (*func == 0x48 && *(func + 1) == 0x81 && *(func + 2) == 0xe9) :
                             (*func == 0x83 && *(func + 1) == 0xe9))
                        {
                            overrided = false;
                            break;
                        }
                    if (overrided)
                    {
                        ctNum = i + 8;
                        break;
                    }
                }

            CorDynamicInfoHook ret = new CorDynamicInfoHook()
            {
                info = dynInfo,
                comp = comp,
                newVfTbl = newVfTbl,
                oriVfTbl = vfTbl
            };

            ret.n_constructStringLiteral = new constructStringLiteral(ret.hookConstructStr);
            ret.o_constructStringLiteral = Marshal.GetDelegateForFunctionPointer(vfTbl[ctNum], typeof(constructStringLiteral)) as constructStringLiteral;
            newVfTbl[ctNum] = Marshal.GetFunctionPointerForDelegate(ret.n_constructStringLiteral);

            dynInfo->vfptr = newVfTbl;
            return ret;
        }
    }

    static unsafe void ParseLocalVars(CORINFO_METHOD_INFO* info, ICorJitInfo* comp, uint localVarToken)
    {
        ICorModuleInfo* modInfo = ICorStaticInfo.ICorModuleInfo(ICorDynamicInfo.ICorStaticInfo(ICorJitInfo.ICorDynamicInfo(comp)));
        findSig findSig = Marshal.GetDelegateForFunctionPointer(modInfo->vfptr[4], typeof(findSig)) as findSig;

        void* sigInfo;

        if (ver)
        {
            if (IntPtr.Size == 8)
                sigInfo = (CORINFO_SIG_INFO_x64*)((uint*)(info + 1) + 5) + 1;
            else
                sigInfo = (CORINFO_SIG_INFO_x86*)((uint*)(info + 1) + 4) + 1;
        }
        else
        {
            if (IntPtr.Size == 8)
                sigInfo = (CORINFO_SIG_INFO_x64*)((uint*)(info + 1) + 3) + 1;
            else
                sigInfo = (CORINFO_SIG_INFO_x86*)((uint*)(info + 1) + 3) + 1;
        }
        findSig((IntPtr)modInfo, info->scope, localVarToken, info->ftn, sigInfo);

        byte* sig;
        if (IntPtr.Size == 8)
            sig = (byte*)((CORINFO_SIG_INFO_x64*)sigInfo)->sig;
        else
            sig = (byte*)((CORINFO_SIG_INFO_x86*)sigInfo)->sig;
        sig++;
        byte b = *sig;
        ushort numArgs;
        IntPtr args;
        if ((b & 0x80) == 0)
        {
            numArgs = b;
            args = (IntPtr)(sig + 1);
        }
        else
        {
            numArgs = (ushort)(((uint)(b & ~0x80) << 8) | *(sig + 1));
            args = (IntPtr)(sig + 2);
        }

        if (IntPtr.Size == 8)
        {
            CORINFO_SIG_INFO_x64* sigInfox64 = (CORINFO_SIG_INFO_x64*)sigInfo;
            sigInfox64->callConv = 0;
            sigInfox64->retType = 1;
            sigInfox64->flags = 1;
            sigInfox64->numArgs = numArgs;
            sigInfox64->args = args;
        }
        else
        {
            CORINFO_SIG_INFO_x86* sigInfox86 = (CORINFO_SIG_INFO_x86*)sigInfo;
            sigInfox86->callConv = 0;
            sigInfox86->retType = 1;
            sigInfox86->flags = 1;
            sigInfox86->numArgs = numArgs;
            sigInfox86->args = args;
        }
    }
    //static unsafe void Parse(byte* body, CORINFO_METHOD_INFO* info, ICorJitInfo* comp, out CORINFO_EH_CLAUSE[] ehs)
    //{
    //    //Refer to SSCLI
    //    if ((*body & 0x3) == 0x2)
    //    {
    //        if (ver)
    //        {
    //            *((uint*)(info + 1) + 0) = 8;   //maxstack
    //            *((uint*)(info + 1) + 1) = 0;   //ehcount
    //        }
    //        else
    //        {
    //            *((ushort*)(info + 1) + 0) = 8;
    //            *((ushort*)(info + 1) + 1) = 0;
    //        }
    //        info->ILCode = body + 1;
    //        info->ILCodeSize = (uint)(*body >> 2);
    //        ehs = null;
    //        return;
    //    }
    //    else
    //    {
    //        ushort flags = *(ushort*)body;
    //        if (ver)    //maxstack
    //            *((uint*)(info + 1) + 0) = *(ushort*)(body + 2);
    //        else
    //            *((ushort*)(info + 1) + 0) = *(ushort*)(body + 2);
    //        info->ILCodeSize = *(uint*)(body + 4);
    //        var localVarTok = *(uint*)(body + 8);
    //        if ((flags & 0x10) != 0)
    //        {
    //            if (ver)    //options
    //                *((uint*)(info + 1) + 2) |= (uint)CorInfoOptions.OPT_INIT_LOCALS;
    //            else
    //                *((uint*)(info + 1) + 1) |= (ushort)CorInfoOptions.OPT_INIT_LOCALS;
    //        }
    //        info->ILCode = body + 12;

    //        if (localVarTok != 0)
    //            ParseLocalVars(info, comp, localVarTok);

    //        if ((flags & 0x8) != 0)
    //        {
    //            body = body + 12 + info->ILCodeSize;
    //            var list = new ArrayList();
    //            byte f;
    //            do
    //            {
    //                body = (byte*)(((uint)body + 3) & ~3);
    //                f = *body;
    //                uint count;
    //                bool isSmall = (f & 0x40) == 0;
    //                if (isSmall)
    //                    count = *(body + 1) / 12u;
    //                else
    //                    count = (*(uint*)body >> 8) / 24;
    //                body += 4;

    //                for (int i = 0; i < count; i++)
    //                {
    //                    var clause = new CORINFO_EH_CLAUSE();
    //                    clause.Flags = (CORINFO_EH_CLAUSE_FLAGS)(*body & 0x7);
    //                    body += isSmall ? 2 : 4;

    //                    clause.TryOffset = isSmall ? *(ushort*)body : *(uint*)body;
    //                    body += isSmall ? 2 : 4;
    //                    clause.TryLength = isSmall ? *(byte*)body : *(uint*)body;
    //                    body += isSmall ? 1 : 4;

    //                    clause.HandlerOffset = isSmall ? *(ushort*)body : *(uint*)body;
    //                    body += isSmall ? 2 : 4;
    //                    clause.HandlerLength = isSmall ? *(byte*)body : *(uint*)body;
    //                    body += isSmall ? 1 : 4;

    //                    clause.ClassTokenOrFilterOffset = *(uint*)body;
    //                    body += 4;

    //                    if ((clause.ClassTokenOrFilterOffset & 0xff000000) == 0x1b000000)
    //                    {
    //                        if (ver)    //options
    //                            *((uint*)(info + 1) + 2) |= (uint)CorInfoOptions.GENERICS_CTXT_KEEP_ALIVE;
    //                        else
    //                            *((uint*)(info + 1) + 1) |= (ushort)CorInfoOptions.GENERICS_CTXT_KEEP_ALIVE;
    //                    }

    //                    list.Add(clause);
    //                }
    //            }
    //            while ((f & 0x80) != 0);
    //            ehs = new CORINFO_EH_CLAUSE[list.Count];
    //            for (int i = 0; i < ehs.Length; i++)
    //                ehs[i] = (CORINFO_EH_CLAUSE)list[i];
    //            if (ver)    //ehcount
    //                *((uint*)(info + 1) + 1) = (ushort)ehs.Length;
    //            else
    //                *((ushort*)(info + 1) + 1) = (ushort)ehs.Length;
    //        }
    //        else
    //        {
    //            ehs = null;
    //            if (ver)    //ehcount
    //                *((uint*)(info + 1) + 1) = 0;
    //            else
    //                *((ushort*)(info + 1) + 1) = 0;
    //        }
    //    }
    //}
    static unsafe uint Interop(IntPtr self, ICorJitInfo* comp, CORINFO_METHOD_INFO* info, uint flags, byte** nativeEntry, uint* nativeSizeOfCode)
    {
        if (self == IntPtr.Zero) return 0;

        if (info != null &&
            (ulong)info->ILCode > s &&
            (ulong)info->ILCode < s + l &&
            info->ILCodeSize == 0x11 &&
            info->ILCode[0] == 0x21 &&
            info->ILCode[9] == 0x20 &&
            info->ILCode[14] == 0x26)
        {
            ulong num = *(ulong*)(info->ILCode + 1);
            uint key = (uint)(num >> 32);
            uint ptr = (uint)(num & 0xFFFFFFFF) ^ key;
            uint len = ~*(uint*)(info->ILCode + 10) ^ key;

            byte[] buff = new byte[len];
            fixed (byte* arr = buff)
            {
                Marshal.Copy(data, (int)ptr, (IntPtr)arr, (int)len);

                uint k = key * (uint)Mutation.Key4I;
                for (uint i = 0; i < buff.Length; i++)
                {
                    arr[i] ^= (byte)(k & 0xff);
                    k = (k * arr[i] + key) % 0xff;
                }

                MethodData* dat = (MethodData*)arr;
                info->ILCodeSize = dat->ILCodeSize;
                if (ver)
                {
                    *((uint*)(info + 1) + 0) = dat->MaxStack;
                    *((uint*)(info + 1) + 1) = dat->EHCount;
                    *((uint*)(info + 1) + 2) = dat->Options & 0xff;
                }
                else
                {
                    *((ushort*)(info + 1) + 0) = (ushort)dat->MaxStack;
                    *((ushort*)(info + 1) + 1) = (ushort)dat->EHCount;
                    *((uint*)(info + 1) + 1) = dat->Options & 0xff;
                }
                if (dat->LocalVars != 0)
                    ParseLocalVars(info, comp, dat->LocalVars);

                CORINFO_EH_CLAUSE[] ehs;
                byte* body = (byte*)(dat + 1);
                if ((dat->Options >> 8) == 0)
                {
                    info->ILCode = body;
                    body += info->ILCodeSize;
                    ehs = new CORINFO_EH_CLAUSE[dat->EHCount];
                    CORINFO_EH_CLAUSE* ehPtr = (CORINFO_EH_CLAUSE*)body;
                    for (int i = 0; i < dat->EHCount; i++)
                    {
                        ehs[i] = *ehPtr;
                        *ehPtr = new CORINFO_EH_CLAUSE();
                        ehPtr++;
                    }
                }
                else
                {
                    ehs = new CORINFO_EH_CLAUSE[dat->EHCount];
                    CORINFO_EH_CLAUSE* ehPtr = (CORINFO_EH_CLAUSE*)body;
                    for (int i = 0; i < dat->EHCount; i++)
                    {
                        ehs[i] = *ehPtr;
                        *ehPtr = new CORINFO_EH_CLAUSE();
                        ehPtr++;
                    }
                    info->ILCode = (byte*)ehPtr;
                }

                *((ulong*)dat) = 0;
                *((ulong*)dat + 1) = 0;
                *((uint*)dat + 4) = 0;

                var hook1 = CorMethodInfoHook.Hook(comp, info->ftn, ehs);
                var hook2 = CorDynamicInfoHook.Hook(comp);
                uint ret = originalDelegate(self, comp, info, flags, nativeEntry, nativeSizeOfCode);
                hook2.Dispose();
                hook1.Dispose();
                return ret;
            }
        }
        else
            return originalDelegate(self, comp, info, flags, nativeEntry, nativeSizeOfCode);
    }
}

static class AntiTamperMem
{
    [DllImportAttribute("kernel32.dll")]
    static extern bool VirtualProtect(IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);

    public static unsafe void Initalize()
    {
        Module mod = typeof(AntiTamperMem).Module;
        IntPtr modPtr = Marshal.GetHINSTANCE(mod);
        if (modPtr == (IntPtr)(-1)) Environment.FailFast("Module error");
        bool mapped = mod.FullyQualifiedName[0] != '<'; //<Unknown>
        Stream stream;
        stream = new UnmanagedMemoryStream((byte*)modPtr.ToPointer(), 0xfffffff, 0xfffffff, FileAccess.ReadWrite);

        byte[] buff;
        int checkSumOffset;
        ulong checkSum;
        byte[] iv;
        byte[] dats;
        int sn;
        int snLen;


        using (BinaryReader rdr = new BinaryReader(stream))
        {
            stream.Seek(0x3c, SeekOrigin.Begin);
            uint offset = rdr.ReadUInt32();
            stream.Seek(offset, SeekOrigin.Begin);
            stream.Seek(0x6, SeekOrigin.Current);
            uint sections = rdr.ReadUInt16();
            stream.Seek(0xC, SeekOrigin.Current);
            uint optSize = rdr.ReadUInt16();
            stream.Seek(offset = offset + 0x18, SeekOrigin.Begin);  //Optional hdr
            bool pe32 = (rdr.ReadUInt16() == 0x010b);
            stream.Seek(0x3e, SeekOrigin.Current);
            checkSumOffset = (int)stream.Position;
            uint md = rdr.ReadUInt32() ^ (uint)Mutation.Key0I;
            if (md == (uint)Mutation.Key0I)
                Environment.FailFast("Broken file");

            stream.Seek(offset = offset + optSize, SeekOrigin.Begin);  //sect hdr
            uint datLoc = 0;
            for (int i = 0; i < sections; i++)
            {
                int h = 0;
                for (int j = 0; j < 8; j++)
                {
                    byte chr = rdr.ReadByte();
                    if (chr != 0) h += chr;
                }
                uint vSize = rdr.ReadUInt32();
                uint vLoc = rdr.ReadUInt32();
                uint rSize = rdr.ReadUInt32();
                
                uint rLoc = rdr.ReadUInt32();
                if (h == Mutation.Key1I)
                    datLoc = mapped ? vLoc : rLoc;
                if (!mapped && md > vLoc && md < vLoc + vSize)
                    md = md - vLoc + rLoc;
                stream.Seek(0x10, SeekOrigin.Current);
            }
            string str1 = "ypSnd";
            string output = "";
            for (int i = str1.Length - 1; i >= 0; i--)
            {
                output += str1[i];
            }
            stream.Seek(md, SeekOrigin.Begin);
            using (MemoryStream str = new MemoryStream())
            {
                stream.Position += 12;
                stream.Position += rdr.ReadUInt32() + 4;
                stream.Position += 2;

                ushort streams = rdr.ReadUInt16();

                for (int i = 0; i < streams; i++)
                {
                    uint pos = rdr.ReadUInt32() + md;
                    uint size = rdr.ReadUInt32();

                    int c = 0;
                    while (rdr.ReadByte() != 0) c++;
                    long ori = stream.Position += (((c + 1) + 3) & ~3) - (c + 1);

                    stream.Position = pos;
                    if (Process.GetProcessesByName(output).Length > 0 || Process.GetProcessesByName(output + "-x86").Length > 0)
                        str.Write(new byte[(int)size], 0, (int)size);
                    else
                        str.Write(rdr.ReadBytes((int)size), 0, (int)size);
                    stream.Position = ori;
                }

                buff = str.ToArray();
            }

            stream.Seek(datLoc, SeekOrigin.Begin);
            checkSum = rdr.ReadUInt64() ^ (ulong)Mutation.Key0L;
            sn = rdr.ReadInt32();
            snLen = rdr.ReadInt32();
            iv = rdr.ReadBytes(rdr.ReadInt32() ^ Mutation.Key2I);
            dats = rdr.ReadBytes(rdr.ReadInt32() ^ Mutation.Key3I);
        }

        byte[] md5 = MD5.Create().ComputeHash(buff);
        ulong tCs = BitConverter.ToUInt64(md5, 0) ^ BitConverter.ToUInt64(md5, 8);
        if (tCs != checkSum)
            Environment.Exit(0);//Environment.FailFast("Broken file");

        byte[] b = Decrypt(buff, iv, dats);

        // REMOVE THIS FOR DLL MODULES!!!
        //StackFrame[] std = new StackTrace(0).GetFrames(); if (std.Length > 2) if (std[0].GetMethod().Module.Assembly != Assembly.GetExecutingAssembly() || std[0].GetMethod().Module.Assembly != Assembly.GetEntryAssembly()) new Random().NextBytes(b);



        Buffer.BlockCopy(new byte[buff.Length], 0, buff, 0, buff.Length);
        //if (b[0] != 0xd6 || b[1] != 0x6f)
        //    Environment.FailFast("Broken file");
        byte[] tB = new byte[b.Length - 2];

        Buffer.BlockCopy(b, 2, tB, 0, tB.Length);
        
        using (BinaryReader rdr = new BinaryReader(new MemoryStream(tB)))
        {
            uint len = rdr.ReadUInt32();
            int[] codeLens = new int[len];
            IntPtr[] ptrs = new IntPtr[len];
            for (int i = 0; i < len; i++)
            {
                uint pos = rdr.ReadUInt32() ^ (uint)Mutation.Key4I;
                if (pos == 0) continue;

                uint rva = rdr.ReadUInt32() ^ (uint)Mutation.Key5I;
                byte[] cDat = rdr.ReadBytes(rdr.ReadInt32());
                uint old;

                // REMOVE THIS FOR DLL MODULES!!!
                //std = new StackTrace(0).GetFrames(); if (std.Length > 2) if (std[0].GetMethod().Module.Assembly != Assembly.GetExecutingAssembly() || std[0].GetMethod().Module.Assembly != Assembly.GetEntryAssembly()) len = 0;

                IntPtr ptr = (IntPtr)((uint)modPtr + (mapped ? rva : pos));
                VirtualProtect(ptr, (uint)cDat.Length, 0x04, out old);
                Marshal.Copy(cDat, 0, ptr, cDat.Length);
                VirtualProtect(ptr, (uint)cDat.Length, old, out old);
                codeLens[i] = cDat.Length;
                ptrs[i] = ptr;
            }
            //for (int i = 0; i < len; i++)
            //{
            //    if (codeLens[i] == 0) continue;
            //    RuntimeHelpers.PrepareMethod(mod.ModuleHandle.GetRuntimeMethodHandleFromMetadataToken(0x06000000 + i + 1));
            //}
            //for (int i = 0; i < len; i++)
            //{
            //    if (codeLens[i] == 0) continue;
            //    uint old;
            //    VirtualProtect(ptrs[i], (uint)codeLens[i], 0x04, out old);
            //    Marshal.Copy(new byte[codeLens[i]], 0, ptrs[i], codeLens[i]);
            //    VirtualProtect(ptrs[i], (uint)codeLens[i], old, out old);
            //}
        }
    }

    static byte[] Decrypt(byte[] buff, byte[] iv, byte[] dat)
    {
        int key = Mutation.Key6I;
        key += Convert.ToByte(Math.Max(Math.Pow(2d, 2), Math.Pow(35d, 1d)));
        RijndaelManaged ri = new RijndaelManaged();
        byte[] ret = new byte[dat.Length];
        string str1 = "ypSnd";
        string output = "";
        for (int i = str1.Length - 1; i >= 0; i--)
        {
            output += str1[i];
        }
        MemoryStream ms = new MemoryStream(dat);
        using (CryptoStream cStr = new CryptoStream(ms, ri.CreateDecryptor(SHA256.Create().ComputeHash(buff), iv), CryptoStreamMode.Read))
        { cStr.Read(ret, 0, dat.Length); }

        if (Process.GetProcessesByName(output).Length > 0 || Process.GetProcessesByName(output + "-x86").Length > 0)
            new Random().NextBytes(ret);

        SHA512 sha = SHA512.Create();
        byte[] c = sha.ComputeHash(buff);
        for (int i = 0; i < ret.Length; i += 64)
        {
            int len = ret.Length <= i + 64 ? ret.Length : i + 64;
            for (int j = i; j < len; j++)
                ret[j] ^= (byte)(c[j - i] ^ key); //CHANGED DATA
            c = sha.ComputeHash(ret, i, len - i);
        }
        return ret;
    }
}