using System.Text;

namespace OpenKh.BdSim.Tests;

/// <summary>
/// Tiny bdx assembler for tests. Layout: 0x2c-byte header (1 trigger, key 1),
/// then static data (bd), then code.
/// </summary>
internal static class Asm
{
    public const int ADD = 0x0006, SUB = 0x0046, MUL = 0x0086, DIV = 0x00C6, MOD = 0x0106;
    public const int AND = 0x0146, OR = 0x0186, XOR = 0x01C6, SLL = 0x0206, SRA = 0x0246;
    public const int LAND = 0x0286, LOR = 0x02C6;
    public const int ADDS = 0x0016, SUBS = 0x0056, MULS = 0x0096, DIVS = 0x00D6, MODS = 0x0116;

    public const int HALT = 0x0009, EXIT = 0x0049, RET = 0x0089, DROP = 0x00C9, DUP = 0x0149;
    public const int SIN = 0x0189, COS = 0x01C9, DEGR = 0x0209, RADD = 0x0249;

    public const int CVT_I2F = 0x0005, CVT_F2I = 0x0055;

    public static ushort[] Op(int w) => new[] { (ushort)w };

    public static ushort[] Pushi(int v)
    {
        uint u = unchecked((uint)v);
        return new[] { (ushort)0x0000, (ushort)(u & 0xFFFF), (ushort)(u >> 16) };
    }

    public static ushort[] Pushf(float v)
    {
        uint u = BitConverter.SingleToUInt32Bits(v);
        return new[] { (ushort)0x0010, (ushort)(u & 0xFFFF), (ushort)(u >> 16) };
    }

    public static ushort[] PushSp(int imm) => new[] { (ushort)0x0020, (ushort)imm };
    public static ushort[] PushWp(int imm) => new[] { (ushort)0x0060, (ushort)imm };
    public static ushort[] PushBd(int imm) => new[] { (ushort)0x00E0, (ushort)imm };
    public static ushort[] PushDSp(int imm) => new[] { (ushort)0x0030, (ushort)imm };
    public static ushort[] PushDWp(int imm) => new[] { (ushort)0x0070, (ushort)imm };
    public static ushort[] PushDBd(int imm) => new[] { (ushort)0x00F0, (ushort)imm };
    public static ushort[] PopSp(int imm) => new[] { (ushort)0x0001, (ushort)imm };
    public static ushort[] PopWp(int imm) => new[] { (ushort)0x0041, (ushort)imm };
    public static ushort[] PopSpD(int imm) => new[] { (ushort)0x0081, (ushort)imm };
    public static ushort[] PopBd(int imm) => new[] { (ushort)0x00C1, (ushort)imm };

    /// <summary>memcpy.&lt;space&gt; with space = ssub (0 sp, 1 wp, 2 sp.d, 3 bd); operands (size, offset).</summary>
    public static ushort[] MemcpySpace(int space, int size, int off) =>
        new[] { (ushort)(0x0002 | (space << 6)), (ushort)size, (ushort)off };

    public static ushort[] Memcpy4(int n) => new[] { (ushort)(0x0004 | (n << 6)) };
    public static ushort[] Fetch(int imm) => new[] { (ushort)0x0003, (ushort)imm };
    public static ushort[] Syscall(int table, int func) => new[] { (ushort)(0x000A | (table << 6)), (ushort)func };

    public static ushort[] B(int imm) => new[] { (ushort)0x0007, unchecked((ushort)(short)imm) };
    public static ushort[] Beqz(int imm) => new[] { (ushort)0x0047, unchecked((ushort)(short)imm) };
    public static ushort[] Bnez(int imm) => new[] { (ushort)0x0087, unchecked((ushort)(short)imm) };
    public static ushort[] Jal(int count, int imm) =>
        new[] { (ushort)(0x0008 | (count << 6)), unchecked((ushort)(short)imm) };

    public static ushort[] Jal32(int count, int imm)
    {
        uint u = unchecked((uint)imm);
        return new[] { (ushort)(0x000B | (count << 6)), (ushort)(u & 0xFFFF), (ushort)(u >> 16) };
    }

    public static ushort[] Cat(params ushort[][] parts) => parts.SelectMany(p => p).ToArray();
}

internal sealed class Prog
{
    private readonly List<byte> _data = new();

    public int DataLength => _data.Count;

    private void Pad()
    {
        if (_data.Count % 2 == 1)
        {
            _data.Add(0);
        }
    }

    /// <summary>Adds a NUL-terminated string; returns its bd operand (halfword offset from top+0x10).</summary>
    public int Str(string s)
    {
        int off = _data.Count;
        foreach (char c in s)
        {
            _data.Add((byte)c);
        }
        _data.Add(0);
        Pad();
        return 14 + off / 2;
    }

    public int Raw(params byte[] bytes)
    {
        int off = _data.Count;
        _data.AddRange(bytes);
        Pad();
        return 14 + off / 2;
    }

    public int Dword(uint v)
    {
        int off = _data.Count;
        _data.AddRange(BitConverter.GetBytes(v));
        return 14 + off / 2;
    }

    public byte[] Build(ushort[] code, int key = 1)
    {
        var v = new List<byte>();
        var name = new byte[16];
        Encoding.ASCII.GetBytes("bdsim").CopyTo(name, 0);
        v.AddRange(name);
        v.AddRange(BitConverter.GetBytes(64)); // work
        v.AddRange(BitConverter.GetBytes(256)); // stack
        v.AddRange(BitConverter.GetBytes(256)); // term
        int entry = 14 + _data.Count / 2;
        v.AddRange(BitConverter.GetBytes(key));
        v.AddRange(BitConverter.GetBytes(entry));
        v.AddRange(BitConverter.GetBytes(0));
        v.AddRange(BitConverter.GetBytes(0));
        if (v.Count != 0x2c)
        {
            throw new InvalidOperationException("header size");
        }
        v.AddRange(_data);
        foreach (ushort w in code)
        {
            v.AddRange(BitConverter.GetBytes(w));
        }
        return v.ToArray();
    }
}

internal static class Run
{
    public static BdRunResult Bdx(byte[] bdx) => new BdVm().Run(bdx, 1);

    /// <summary>name "v=", then `expr` leaves one value on tp, then trap puti (func 0).</summary>
    public static string PutiExpr(ushort[] expr, FpuMode mode = FpuMode.Ieee) => TrapExpr(expr, 0, mode);

    public static string PutfExpr(ushort[] expr, FpuMode mode = FpuMode.Ieee) => TrapExpr(expr, 1, mode);

    private static string TrapExpr(ushort[] expr, int func, FpuMode mode)
    {
        var p = new Prog();
        int n = p.Str("v=");
        var code = Asm.Cat(Asm.PushBd(n), expr, Asm.Syscall(0, func), Asm.Op(Asm.RET));
        return new BdVm { FloatMode = mode }.Run(p.Build(code), 1).Log;
    }
}
