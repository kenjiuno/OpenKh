using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace OpenKh.BdSim;

/// <summary>BD_STATUS as returned by BdExecVM (same values and names as khkh_xldM BdStatus).</summary>
public enum BdStatus
{
    Exec = 0,
    Halt = 1,
    Exit = 2,
    Ret = 3,
    Ignore = 4,
    Error = 5,
}

public sealed record BdRunResult(BdStatus Status, string Log);

/// <summary>
/// A condition bdEmu reports as a thrown exception (broken sp/tp invariants, unknown trigger,
/// out-of-range guest access, integer division by zero, step limit, ...).
/// </summary>
public sealed class BdVmException : Exception
{
    public BdVmException(string message) : base(message)
    {
    }
}

/// <summary>
/// Direct interpreter of the KH2 beads VM with PS2 (SLPM_666.75) semantics. It does not emulate a CPU.
///
/// Guest memory is one flat 32 MiB arena addressed with 32-bit guest addresses, laid out like bdEmu
/// (bdx at 0x1000400, sp +1MiB, tp +2MiB, wp +3MiB). All accesses are little endian and may be unaligned.
///
/// <see cref="Run"/> corresponds to <c>MobileAiExec.RunVM(ai, trigger, push, keepLastState)</c>.
/// </summary>
public sealed class BdVm
{
    public const int MemSize = 0x0200_0000;
    public const uint Base = 0x0100_0000;
    public const uint TopAddr = Base + 1024;
    public const uint SpAddr = Base + 1024 + 0x10_0000;
    public const uint TpAddr = Base + 1024 + 0x20_0000;
    public const uint WpAddr = Base + 1024 + 0x30_0000;
    public const int MaxBdxSize = 0x10_0000;

    private byte[]? _mem;

    /// <summary>Float arithmetic: <see cref="FpuMode.Ieee"/> (default, what bdEmu does) or <see cref="FpuMode.Ee"/>.</summary>
    public FpuMode FloatMode { get; set; } = FpuMode.Ieee;

    /// <summary>Maximum number of instructions per <see cref="Run"/> (guards against endless loops).</summary>
    public long StepLimit { get; set; } = 1_000_000_000;

    /// <summary>Guest memory (empty until the first <see cref="Run"/>).</summary>
    public ReadOnlySpan<byte> Memory => _mem ?? Array.Empty<byte>();

    public BdRunResult Run(byte[] bdx, int trigger, int[]? push = null, bool keepLastState = false)
    {
        int initPc = FindTrigger(bdx, trigger);

        if (bdx.Length > MaxBdxSize)
        {
            throw new BdVmException($"bdx is too large: {bdx.Length} bytes (max {MaxBdxSize})");
        }
        if (_mem is null)
        {
            _mem = new byte[MemSize];
        }
        else if (!keepLastState)
        {
            Array.Clear(_mem);
        }

        var m = new Machine(_mem)
        {
            Top = TopAddr,
            Pc = unchecked((uint)initPc),
            Sp = SpAddr,
            Tp = TpAddr,
            Wp = WpAddr,
            Mode = FloatMode,
        };
        Array.Copy(bdx, 0, _mem, (int)TopAddr, bdx.Length);

        uint exitTp = TpAddr;
        if (push is not null)
        {
            foreach (int v in push)
            {
                m.Push(unchecked((uint)v));
            }
        }

        long steps = 0;
        BdStatus status;
        while ((status = m.Step()) == BdStatus.Exec)
        {
            if (++steps >= StepLimit)
            {
                throw new BdVmException($"step limit ({StepLimit}) exceeded");
            }
        }

        // same sanity checks as bdEmu, applied whatever the status is
        if (m.Sp != SpAddr)
        {
            throw new BdVmException($"sp {m.Sp} must be {SpAddr}");
        }
        if (m.Tp != exitTp)
        {
            throw new BdVmException($"tp {m.Tp} must be {exitTp}");
        }
        return new BdRunResult(status, m.Log);
    }

    private static int ReadI32(byte[] b, int off)
    {
        if (off < 0 || off + 4 > b.Length)
        {
            throw new BdVmException($"bdx header read out of range at offset {off}");
        }
        return BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(off, 4));
    }

    /// <summary>header+0x1c: (key, entry) pairs. Same loop as bdEmu: match first, then the key == 0 terminator check.</summary>
    private static int FindTrigger(byte[] bdx, int trigger)
    {
        for (int x = 0; ; x++)
        {
            int off = 28 + 8 * x;
            int key = ReadI32(bdx, off);
            if (key == trigger)
            {
                return ReadI32(bdx, off + 4);
            }
            if (key == 0)
            {
                throw new BdVmException($"Trigger {trigger} not found!");
            }
        }
    }

    /// <summary>The three traps bdEmu registers: table 0, funcs 0..2.</summary>
    private static bool LookupTrap(uint table, uint func, out int kind, out int argc, out bool hasRet)
    {
        kind = -1;
        argc = 0;
        hasRet = false;
        if (table != 0)
        {
            return false;
        }
        switch (func)
        {
            case 0: kind = 0; argc = 2; return true; // puti(name, value)
            case 1: kind = 1; argc = 2; return true; // putf(name, value)
            case 2: kind = 2; argc = 1; return true; // puts(str)
            default: return false;
        }
    }

    /// <summary>
    /// How bdEmu (net48) prints a float with <c>TextWriter.Write(float)</c>: 7 significant digits ("G7"),
    /// and negative zero as "0" (.NET Core 3.0+ would print "-0").
    /// </summary>
    public static string FormatSingle(float f)
    {
        string s = f.ToString("G7", CultureInfo.InvariantCulture);
        return s == "-0" ? "0" : s;
    }

    private static readonly uint Pi = BitConverter.SingleToUInt32Bits(MathF.PI);
    private static readonly uint TwoPi = BitConverter.SingleToUInt32Bits(2.0f * MathF.PI);
    private static readonly uint C180 = BitConverter.SingleToUInt32Bits(180.0f);

    /// <summary>fmodf(x + pi, 2pi) - pi</summary>
    private static uint Norm(FpuMode m, uint x) => Fpu.Sub(m, Fpu.Mod(m, Fpu.Add(m, x, Pi), TwoPi), Pi);

    private sealed class Machine
    {
        private readonly byte[] _mem;
        private readonly StringBuilder _log = new();

        public uint Top;
        public uint Pc; // halfword index from top + 0x10
        public uint Sp;
        public uint Tp;
        public uint Wp;
        public FpuMode Mode;

        public Machine(byte[] mem)
        {
            _mem = mem;
        }

        public string Log => _log.ToString();

        // ---- guest memory -------------------------------------------------

        private int Range(uint a, int n)
        {
            if ((ulong)a + (ulong)n > (ulong)_mem.Length)
            {
                throw new BdVmException($"guest memory access out of range: 0x{a:x8} (+{n} bytes)");
            }
            return (int)a;
        }

        private byte Rd8(uint a) => _mem[Range(a, 1)];

        private ushort Rd16(uint a) => BinaryPrimitives.ReadUInt16LittleEndian(_mem.AsSpan(Range(a, 2), 2));

        private uint Rd32(uint a) => BinaryPrimitives.ReadUInt32LittleEndian(_mem.AsSpan(Range(a, 4), 4));

        private void Wr32(uint a, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(_mem.AsSpan(Range(a, 4), 4), v);

        /// <summary>memmove-style copy inside the arena.</summary>
        private void Copy(uint dst, uint src, int n)
        {
            int s = Range(src, n);
            int d = Range(dst, n);
            Array.Copy(_mem, s, _mem, d, n);
        }

        private string ReadCString(uint a)
        {
            var sb = new StringBuilder();
            while (true)
            {
                byte b = Rd8(a);
                if (b == 0)
                {
                    return sb.ToString();
                }
                sb.Append((char)b); // (char)byte, like bdEmu
                a = unchecked(a + 1);
            }
        }

        // ---- code fetch ---------------------------------------------------

        private ushort Code16(uint k)
        {
            ulong a = (ulong)Top + 0x10 + ((ulong)Pc + k) * 2;
            if (a > uint.MaxValue)
            {
                throw new BdVmException($"code fetch out of range (pc={Pc})");
            }
            return Rd16((uint)a);
        }

        private uint Code32(uint k) => (uint)Code16(k) | ((uint)Code16(k + 1) << 16);

        // ---- tp -----------------------------------------------------------

        public void Push(uint v)
        {
            Wr32(Tp, v);
            Tp = unchecked(Tp + 4);
        }

        private uint Pop()
        {
            uint v = Rd32(unchecked(Tp - 4));
            Tp = unchecked(Tp - 4);
            return v;
        }

        private uint Peek() => Rd32(unchecked(Tp - 4));

        private void Poke(uint v) => Wr32(unchecked(Tp - 4), v);

        /// <summary>Resolves an address space (the ssub field) + 16-bit operand. null = undefined space (ssub &gt;= 4).</summary>
        private uint? Space(uint ssub, uint imm)
        {
            switch (ssub)
            {
                case 0: return unchecked(Sp + imm);
                case 1: return unchecked(Wp + imm);
                case 2: return unchecked(Rd32(Sp) + imm);
                case 3: return unchecked(Top + 0x10 + imm * 2);
                default: return null;
            }
        }

        private void Enter(uint count, uint ret)
        {
            Sp = unchecked(Sp + count * 4);
            Wr32(unchecked(Sp - 4), ret);
            Wr32(unchecked(Sp - 8), count);
        }

        // ---- one instruction ------------------------------------------------

        public BdStatus Step()
        {
            ushort w = Code16(0);
            uint op = (uint)(w & 0xF);
            uint sub = (uint)((w >> 4) & 3);
            uint ssub = (uint)(w >> 6);

            switch (op)
            {
                // push
                case 0:
                {
                    if (sub < 2)
                    {
                        Push(Code32(1));
                        Pc += 3;
                    }
                    else
                    {
                        uint imm = Code16(1);
                        uint? a = Space(ssub, imm);
                        if (a is null)
                        {
                            return BdStatus.Error;
                        }
                        Push(sub == 2 ? a.Value : Rd32(a.Value));
                        Pc += 2;
                    }
                    break;
                }

                // pop
                case 1:
                {
                    uint imm = Code16(1);
                    uint? a = Space(ssub, imm);
                    if (a is null)
                    {
                        return BdStatus.Error;
                    }
                    Wr32(a.Value, Pop());
                    Pc += 2;
                    break;
                }

                // memcpy.<space>  operands: (size, offset); src pointer on tp
                case 2:
                {
                    int size = Code16(1);
                    uint off = Code16(2);
                    uint? a = Space(ssub, off);
                    if (a is null)
                    {
                        return BdStatus.Error;
                    }
                    uint src = Pop();
                    Copy(a.Value, src, size);
                    Pc += 3;
                    break;
                }

                // push.d.pop imm16 (fetchValue)
                case 3:
                {
                    uint imm = Code16(1);
                    uint addr = Pop();
                    Push(Rd32(unchecked(addr + imm)));
                    Pc += 2;
                    break;
                }

                // memcpy ssub: val = top, dest = below. ssub == 0: 4-byte store.
                case 4:
                {
                    uint val = Pop();
                    uint dest = Pop();
                    if (ssub == 0)
                    {
                        Wr32(dest, val);
                    }
                    else
                    {
                        Copy(dest, val, (int)ssub);
                    }
                    Pc += 1;
                    break;
                }

                // unary
                case 5:
                {
                    uint a = Peek();
                    uint r;
                    if (sub == 0)
                    {
                        int x = unchecked((int)a);
                        switch (ssub)
                        {
                            case 0: r = EeFpu.CvtS(a); break; // int -> float
                            case 2: r = unchecked((uint)(-x)); break;
                            case 3: r = ~a; break;
                            case 4:
                            case 8: r = x == 0 ? 1u : 0u; break;
                            case 5: r = unchecked((uint)(x < 0 ? -x : x)); break;
                            case 6: r = x < 0 ? 1u : 0u; break;
                            case 7: r = x <= 0 ? 1u : 0u; break;
                            case 9: r = x != 0 ? 1u : 0u; break;
                            case 10: r = x >= 0 ? 1u : 0u; break;
                            case 11: r = x > 0 ? 1u : 0u; break;
                            default: return BdStatus.Error;
                        }
                    }
                    else if (sub == 1)
                    {
                        float f = Fpu.Operand(Mode, a);
                        switch (ssub)
                        {
                            case 1: r = EeFpu.CvtW(a); break; // float -> int
                            case 2: r = a ^ 0x80000000u; break;
                            case 5: r = a & 0x7FFFFFFFu; break;
                            case 6: r = f < 0.0f ? 1u : 0u; break;
                            case 7: r = f <= 0.0f ? 1u : 0u; break;
                            case 8: r = f == 0.0f ? 1u : 0u; break;
                            case 9: r = f != 0.0f ? 1u : 0u; break;
                            case 10: r = f >= 0.0f ? 1u : 0u; break;
                            case 11: r = f > 0.0f ? 1u : 0u; break;
                            default: return BdStatus.Error;
                        }
                    }
                    else
                    {
                        return BdStatus.Error;
                    }
                    Poke(r);
                    Pc += 1;
                    break;
                }

                // binary: b = top, a = below, result = a op b
                case 6:
                {
                    bool valid = (sub == 0 && ssub <= 11) || (sub == 1 && ssub <= 4);
                    if (!valid)
                    {
                        return BdStatus.Error;
                    }
                    uint b = Pop();
                    uint a = Pop();
                    uint r;
                    if (sub == 0)
                    {
                        int x = unchecked((int)a);
                        int y = unchecked((int)b);
                        switch (ssub)
                        {
                            case 0: r = unchecked(a + b); break;
                            case 1: r = unchecked(a - b); break;
                            case 2: r = unchecked(a * b); break;
                            case 3:
                            case 4:
                                if (y == 0)
                                {
                                    throw new BdVmException(
                                        $"BREAK 7 (integer {(ssub == 3 ? "division" : "modulo")} by zero)");
                                }
                                if (y == -1)
                                {
                                    // C# would throw OverflowException for int.MinValue / -1; the EE gives MinValue and 0
                                    r = ssub == 3 ? unchecked((uint)(-x)) : 0u;
                                }
                                else
                                {
                                    r = unchecked((uint)(ssub == 3 ? x / y : x % y));
                                }
                                break;
                            case 5: r = a & b; break;
                            case 6: r = a | b; break;
                            case 7: r = a ^ b; break;
                            case 8: r = a << (int)(b & 31); break; // sllv: low 5 bits
                            case 9: r = unchecked((uint)(x >> (int)(b & 31))); break; // srav: low 5 bits, arithmetic
                            case 10: r = (a != 0 && b != 0) ? 1u : 0u; break; // land
                            default: r = (a != 0 || b != 0) ? 1u : 0u; break; // lor
                        }
                    }
                    else
                    {
                        switch (ssub)
                        {
                            case 0: r = Fpu.Add(Mode, a, b); break;
                            case 1: r = Fpu.Sub(Mode, a, b); break;
                            case 2: r = Fpu.Mul(Mode, a, b); break;
                            case 3: r = Fpu.Div(Mode, a, b); break;
                            default: r = Fpu.Mod(Mode, a, b); break;
                        }
                    }
                    Push(r);
                    Pc += 1;
                    break;
                }

                // branch, relative to the halfword after the operand
                case 7:
                {
                    if (ssub > 2)
                    {
                        return BdStatus.Error;
                    }
                    uint imm = unchecked((uint)(int)(short)Code16(1));
                    uint next = Pc + 2;
                    bool take = ssub == 0 || (ssub == 1 ? Pop() == 0 : Pop() != 0);
                    Pc = take ? unchecked(next + imm) : next;
                    break;
                }

                // jal ssub, imm16
                case 8:
                {
                    uint imm = unchecked((uint)(int)(short)Code16(1));
                    uint ret = Pc + 2;
                    Enter(ssub, ret);
                    Pc = unchecked(ret + imm);
                    break;
                }

                // jal32 ssub, imm32
                case 0xB:
                {
                    uint imm = Code32(1);
                    uint ret = Pc + 3;
                    Enter(ssub, ret);
                    Pc = unchecked(ret + imm);
                    break;
                }

                case 9:
                {
                    switch (ssub)
                    {
                        case 0:
                            Pc += 1;
                            return BdStatus.Halt;
                        case 1:
                            Pc += 1;
                            return BdStatus.Exit;
                        case 2:
                        {
                            uint ip = Rd32(unchecked(Sp - 4));
                            uint cnt = Rd32(unchecked(Sp - 8));
                            Sp = unchecked(Sp - cnt * 4);
                            Pc = ip;
                            if (ip == 0)
                            {
                                return BdStatus.Ret;
                            }
                            break;
                        }
                        case 3:
                            Pop();
                            Pc += 1;
                            break;
                        case 5:
                            Push(Peek());
                            Pc += 1;
                            break;
                        case 6:
                            Poke(Fpu.Result(Mode, MathF.Sin(Fpu.Operand(Mode, Norm(Mode, Peek())))));
                            Pc += 1;
                            break;
                        case 7:
                            Poke(Fpu.Result(Mode, MathF.Cos(Fpu.Operand(Mode, Norm(Mode, Peek())))));
                            Pc += 1;
                            break;
                        case 8:
                        {
                            // degr: fmodf(x * pi / 180 + pi, 2pi) - pi
                            uint x = Peek();
                            uint t = Fpu.Add(Mode, Fpu.Div(Mode, Fpu.Mul(Mode, x, Pi), C180), Pi);
                            Poke(Fpu.Sub(Mode, Fpu.Mod(Mode, t, TwoPi), Pi));
                            Pc += 1;
                            break;
                        }
                        case 9:
                            // radd: x * 180 / pi
                            Poke(Fpu.Div(Mode, Fpu.Mul(Mode, Peek(), C180), Pi));
                            Pc += 1;
                            break;
                        default:
                            return BdStatus.Error; // includes ssub 4
                    }
                    break;
                }

                // syscall table=ssub, func=imm16
                case 0xA:
                {
                    uint imm = Code16(1);
                    Pc += 2;
                    if (!LookupTrap(ssub, imm, out int kind, out int argc, out bool hasRet))
                    {
                        throw new BdVmException($"trap {ssub},{imm} is not registered");
                    }
                    uint baseAddr = unchecked(Tp - (uint)(argc * 4));
                    var args = new uint[16];
                    for (int i = 0; i < argc; i++)
                    {
                        args[i] = Rd32(unchecked(baseAddr + (uint)(i * 4)));
                    }
                    Tp = baseAddr;
                    switch (kind)
                    {
                        case 0: // puti: bdEmu prints a uint (unsigned decimal)
                            _log.Append(ReadCString(args[0]));
                            _log.Append(args[1].ToString(CultureInfo.InvariantCulture));
                            break;
                        case 1: // putf: net48's float.ToString() has 7 significant digits ("G7")
                            _log.Append(ReadCString(args[0]));
                            _log.Append(FormatSingle(BitConverter.UInt32BitsToSingle(args[1])));
                            break;
                        default: // puts
                            _log.Append(ReadCString(args[0]));
                            break;
                    }
                    if (hasRet)
                    {
                        Push(args[0]);
                    }
                    break;
                }

                default:
                    return BdStatus.Error;
            }
            return BdStatus.Exec;
        }
    }
}
