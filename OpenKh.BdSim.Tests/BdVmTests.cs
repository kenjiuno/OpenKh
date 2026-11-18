using Xunit;
using static OpenKh.BdSim.Tests.Asm;

namespace OpenKh.BdSim.Tests;

public class BdVmTests
{
    private static void Eq(string expected, string actual, string label)
    {
        Assert.True(expected == actual, $"{label}: expected '{expected}' but was '{actual}'");
    }

    private static string BinI(int a, int b, int w) => Run.PutiExpr(Cat(Pushi(a), Pushi(b), Op(w)));

    private static string UnI(int a, int w) => Run.PutiExpr(Cat(Pushi(a), Op(w)));

    private static string BinF(float a, float b, int w, FpuMode mode = FpuMode.Ieee) =>
        Run.PutfExpr(Cat(Pushf(a), Pushf(b), Op(w)), mode);

    private static string UnFToI(float a, int w) => Run.PutiExpr(Cat(Pushf(a), Op(w)));

    private static string G7(uint bits) => BdVm.FormatSingle(BitConverter.UInt32BitsToSingle(bits));

    [Fact]
    public void HaltRetExitStatuses()
    {
        var p = new Prog();
        Assert.Equal(BdStatus.Halt, Run.Bdx(p.Build(Op(HALT))).Status);
        Assert.Equal(BdStatus.Exit, Run.Bdx(p.Build(Op(EXIT))).Status);
        Assert.Equal(BdStatus.Ret, Run.Bdx(p.Build(Op(RET))).Status);
        // op9 ssub 4 is undefined
        Assert.Equal(BdStatus.Error, Run.Bdx(p.Build(Op(0x0109))).Status);
        // op 0xC is undefined
        Assert.Equal(BdStatus.Error, Run.Bdx(p.Build(Op(0x000C))).Status);
    }

    [Fact]
    public void IntegerBinaryOps()
    {
        (int a, int b, int w, string e)[] cases =
        {
            (7, 5, ADD, "v=12"),
            (3, 5, SUB, "v=4294967294"),
            (-3, 4, MUL, "v=4294967284"),
            (-7, 2, DIV, "v=4294967293"), // -3
            (-7, 2, MOD, "v=4294967295"), // -1
            (12, 10, AND, "v=8"),
            (12, 10, OR, "v=14"),
            (12, 10, XOR, "v=6"),
            (1, 4, SLL, "v=16"),
            (2, 3, LAND, "v=1"),
            (2, 0, LAND, "v=0"),
            (0, 0, LOR, "v=0"),
            (0, 5, LOR, "v=1"),
        };
        foreach (var (a, b, w, e) in cases)
        {
            Eq(e, BinI(a, b, w), $"{a} op{w:X4} {b}");
        }
    }

    [Fact]
    public void ShiftsUseLow5BitsLikeMips()
    {
        (int a, int b, int w, string e)[] cases =
        {
            (1, 16, SLL, "v=65536"), // case1 test10041 expects 1: that expectation came from an emulator bug
            (1, 31, SLL, "v=2147483648"),
            (1, 32, SLL, "v=1"), // 32 & 31 == 0
            (1, 33, SLL, "v=2"),
            (-8, 1, SRA, "v=4294967292"), // arithmetic
            (-8, 33, SRA, "v=4294967292"),
            (-1, 31, SRA, "v=4294967295"),
        };
        foreach (var (a, b, w, e) in cases)
        {
            Eq(e, BinI(a, b, w), $"{a} op{w:X4} {b}");
        }
    }

    [Fact]
    public void DivisionEdgeCases()
    {
        // C# throws OverflowException for these; the EE returns MinValue and 0
        Assert.Equal("v=2147483648", BinI(int.MinValue, -1, DIV));
        Assert.Equal("v=0", BinI(int.MinValue, -1, MOD));
        Assert.Equal("v=0", BinI(5, -1, MOD));
        foreach (int w in new[] { DIV, MOD })
        {
            var p = new Prog();
            int n = p.Str("v=");
            var code = Cat(PushBd(n), Pushi(1), Pushi(0), Op(w), Syscall(0, 0), Op(RET));
            var ex = Assert.Throws<BdVmException>(() => Run.Bdx(p.Build(code)));
            Assert.Contains("BREAK 7", ex.Message);
        }
    }

    [Fact]
    public void IntegerUnaryOps()
    {
        (int a, int w, string e)[] cases =
        {
            (5, 0x0085, "v=4294967291"), // neg
            (0, 0x00C5, "v=4294967295"), // not (~)
            (0, 0x0105, "v=1"), // seqz
            (3, 0x0105, "v=0"),
            (0, 0x0205, "v=1"), // seqz (duplicate encoding)
            (-4, 0x0145, "v=4"), // abs
            (int.MinValue, 0x0145, "v=2147483648"), // abs wraps
            (-1, 0x0185, "v=1"), // sltz
            (0, 0x01C5, "v=1"), // slez
            (1, 0x01C5, "v=0"),
            (0, 0x0245, "v=0"), // snez
            (-1, 0x0285, "v=0"), // sgez
            (0, 0x0285, "v=1"),
            (1, 0x02C5, "v=1"), // sgtz
            (0, 0x02C5, "v=0"),
        };
        foreach (var (a, w, e) in cases)
        {
            Eq(e, UnI(a, w), $"op{w:X4}({a})");
        }
    }

    [Fact]
    public void UnaryUndefinedEncodingsAreErrors()
    {
        var p = new Prog();
        // int ssub 1, int ssub 12; float ssub 0, float ssub 3; sub 2
        foreach (int w in new[] { 0x0045, 0x0305, 0x0015, 0x00D5, 0x0025 })
        {
            // nothing pushed: tp stays balanced, so the Error status itself is observable
            Assert.True(BdStatus.Error == Run.Bdx(p.Build(Op(w))).Status, $"w={w:X4}");
        }
    }

    [Fact]
    public void FloatOps()
    {
        Eq("v=3.75", BinF(1.5f, 2.25f, ADDS), "add.s");
        Eq("v=0.5", BinF(1.0f, 0.5f, SUBS), "sub.s");
        Eq("v=3", BinF(1.5f, 2.0f, MULS), "mul.s");
        Eq("v=0.25", BinF(1.0f, 4.0f, DIVS), "div.s");
        Eq("v=1.5", BinF(5.5f, 2.0f, MODS), "mod.s");
        Eq("v=-1.5", Run.PutfExpr(Cat(Pushf(1.5f), Op(0x0095))), "neg.s");
        Eq("v=1.5", Run.PutfExpr(Cat(Pushf(-1.5f), Op(0x0155))), "abs.s");
        Eq("v=1", UnFToI(-1.0f, 0x0195), "sltz.s");
        Eq("v=1", UnFToI(0.0f, 0x0215), "seqz.s");
        Eq("v=1", UnFToI(2.0f, 0x02D5), "sgtz.s");
        Eq("v=0", UnFToI(2.0f, 0x01D5), "slez.s");
    }

    [Fact]
    public void Conversions()
    {
        Eq("v=7", Run.PutfExpr(Cat(Pushi(7), Op(CVT_I2F))), "i2f 7");
        Eq("v=-3", Run.PutfExpr(Cat(Pushi(-3), Op(CVT_I2F))), "i2f -3");
        Eq("v=4294967295", UnFToI(-1.9f, CVT_F2I), "f2i -1.9"); // truncates toward zero
        Eq("v=1", UnFToI(1.9f, CVT_F2I), "f2i 1.9");
        Eq("v=2147483647", UnFToI(3.0e10f, CVT_F2I), "f2i +overflow"); // CVT.W.S clamps
        Eq("v=2147483648", UnFToI(-3.0e10f, CVT_F2I), "f2i -overflow");
    }

    [Fact]
    public void DefaultFloatModeIsPlainIeeeLikeBdEmu()
    {
        // ee1DecExtra emits the ordinary .NET operators for ADD.S / MUL.S / DIV.S
        Assert.Equal(FpuMode.Ieee, new BdVm().FloatMode);
        Eq("v=Infinity", BinF(1.0f, 0.0f, DIVS), "1/0");
        Eq("v=-Infinity", BinF(-1.0f, 0.0f, DIVS), "-1/0");
        Eq("v=NaN", BinF(0.0f, 0.0f, DIVS), "0/0");
        Eq("v=Infinity", BinF(float.MaxValue, 2.0f, MULS), "max*2");
        Eq("v=0", BinF(1e-30f, 1e-30f, MULS), "underflow");
        Eq("v=1.401298E-45", BinF(BitConverter.Int32BitsToSingle(1), 1.0f, MULS), "denormal * 1");
    }

    [Fact]
    public void EeFpuModeHasNoInfinityOrDenormals()
    {
        // INST_E.pdf, DIV.S: x/0 and 0/0 give +-max, overflow gives +-max, underflow gives +-0
        const FpuMode ee = FpuMode.Ee;
        Eq("v=3.402823E+38", BinF(1.0f, 0.0f, DIVS, ee), "1/0");
        Eq("v=-3.402823E+38", BinF(-1.0f, 0.0f, DIVS, ee), "-1/0");
        Eq("v=3.402823E+38", BinF(0.0f, 0.0f, DIVS, ee), "0/0");
        Eq("v=3.402823E+38", BinF(float.MaxValue, 2.0f, MULS, ee), "max*2");
        Eq("v=0", BinF(1e-30f, 1e-30f, MULS, ee), "underflow");
        Eq("v=1", BinF(BitConverter.Int32BitsToSingle(1), 1.0f, ADDS, ee), "denormal + 1");
        Eq("v=0", BinF(BitConverter.Int32BitsToSingle(1), 1.0f, MULS, ee), "denormal * 1");

        Assert.Equal(0x7F7FFFFFu, EeFpu.Div(BitConverter.SingleToUInt32Bits(1.0f), 0u));
        Assert.Equal(0xFF7FFFFFu, EeFpu.Div(BitConverter.SingleToUInt32Bits(-1.0f), 0u));
        Assert.Equal(0xFF7FFFFFu, EeFpu.Div(0u, 0x80000000u)); // sign(x) xor sign(y)
        Assert.Equal(0x7F7FFFFFu, EeFpu.Normalize(0x7F800000u)); // +Inf
        Assert.Equal(0xFF7FFFFFu, EeFpu.Normalize(0xFFC00000u)); // NaN with sign
        Assert.Equal(0x80000000u, EeFpu.Normalize(0x80000001u)); // -denormal
    }

    [Theory]
    [InlineData(FpuMode.Ieee)]
    [InlineData(FpuMode.Ee)]
    public void MathOpsAreWiredUp(FpuMode mode)
    {
        Eq("v=0", Run.PutfExpr(Cat(Pushf(0.0f), Op(SIN)), mode), "sin 0");
        Eq("v=1", Run.PutfExpr(Cat(Pushf(0.0f), Op(COS)), mode), "cos 0");
        // case1.bdscript: degr(180) = -pi, degr(270) = -pi/2, degr(360) ~ 0, radd(pi) = 180
        Eq("v=-3.141593", Run.PutfExpr(Cat(Pushf(180.0f), Op(DEGR)), mode), "degr 180");
        Eq("v=-1.570796", Run.PutfExpr(Cat(Pushf(270.0f), Op(DEGR)), mode), "degr 270");
        Eq("v=1.570796", Run.PutfExpr(Cat(Pushf(90.0f), Op(DEGR)), mode), "degr 90");
        Eq("v=180", Run.PutfExpr(Cat(Pushf(MathF.PI), Op(RADD)), mode), "radd pi");
        // dup / drop
        Eq("v=42", Run.PutiExpr(Cat(Pushi(21), Op(DUP), Op(ADD))), "dup");
        Eq("v=5", Run.PutiExpr(Cat(Pushi(5), Pushi(9), Op(DROP))), "drop");
    }

    [Fact]
    public void FloatsArePrintedLikeNet48WithSevenDigits()
    {
        // net48's float.ToString() keeps 7 significant digits; .NET Core's default is the shortest round-trip form.
        Eq("v=3.141593", Run.PutfExpr(Pushf(MathF.PI)), "pi");
        Eq("v=1.5", Run.PutfExpr(Pushf(1.5f)), "1.5");
        Eq("v=0.1", Run.PutfExpr(Pushf(0.1f)), "0.1");
        Eq("v=100000", Run.PutfExpr(Pushf(100000.0f)), "1e5");
        Eq("v=1E+07", Run.PutfExpr(Pushf(1.0e7f)), "1e7");
        Eq("v=1234567", Run.PutfExpr(Pushf(1234567.0f)), "1234567");
        Eq("v=0.0001", Run.PutfExpr(Pushf(0.0001f)), "1e-4");
        Eq("v=1E-05", Run.PutfExpr(Pushf(0.00001f)), "1e-5");
        Eq("v=0", Run.PutfExpr(Pushf(-0.0f)), "-0"); // net48 prints 0 for negative zero, .NET Core prints -0
    }

    [Fact]
    public void Branches()
    {
        // beqz taken: skips pushi(111)
        Assert.Equal("v=222", Run.PutiExpr(Cat(Pushi(0), Beqz(3), Pushi(111), Pushi(222))));
        // bnez taken
        Assert.Equal("v=222", Run.PutiExpr(Cat(Pushi(7), Bnez(3), Pushi(111), Pushi(222))));
        // beqz not taken, then unconditional b over pushi(222)
        Assert.Equal("v=111", Run.PutiExpr(Cat(Pushi(1), Beqz(5), Pushi(111), B(3), Pushi(222))));
    }

    [Fact]
    public void BackwardBranchLoopSums1To5()
    {
        var body = Cat(
            PushDSp(4), PushDSp(0), Op(ADD), PopSp(4),
            PushDSp(0), Pushi(1), Op(SUB), PopSp(0));
        int bl = body.Length;
        var expr = Cat(
            Pushi(5), PopSp(0), Pushi(0), PopSp(4),
            PushDSp(0), Beqz(bl + 2),
            body,
            B(-(6 + bl)),
            PushDSp(4));
        Assert.Equal("v=15", Run.PutiExpr(expr));
    }

    [Fact]
    public void JalAndRetWithArgumentsOnTp()
    {
        foreach (bool wide in new[] { false, true })
        {
            var p = new Prog();
            int n = p.Str("v=");
            var tail = Cat(Syscall(0, 0), Op(RET));
            int t = tail.Length;
            var call = wide ? Jal32(2, t) : Jal(2, t);
            var code = Cat(
                PushBd(n), Pushi(21), call, tail,
                // callee: doubles the top of tp and returns
                Op(DUP), Op(ADD), Op(RET));
            var r = Run.Bdx(p.Build(code));
            Assert.Equal(BdStatus.Ret, r.Status);
            Assert.True(r.Log == "v=42", $"wide={wide}: '{r.Log}'");
        }
    }

    [Fact]
    public void UnbalancedSpOrTpIsReportedLikeBdEmu()
    {
        var p = new Prog();
        var e = Assert.Throws<BdVmException>(() => Run.Bdx(p.Build(Cat(Pushi(1), Op(RET)))));
        Assert.Contains("tp ", e.Message);
        Assert.Contains("must be", e.Message);

        // halt inside a callee leaves sp advanced
        e = Assert.Throws<BdVmException>(() => Run.Bdx(p.Build(Cat(Jal(2, 0), Op(HALT)))));
        Assert.Contains("sp ", e.Message);

        // an Error status with a value left on tp is also reported as an exception
        e = Assert.Throws<BdVmException>(() => Run.Bdx(p.Build(Cat(Pushi(1), Op(0x0045)))));
        Assert.Contains("tp ", e.Message);
    }

    [Fact]
    public void SpAndWpVariables()
    {
        Assert.Equal("v=77", Run.PutiExpr(Cat(Pushi(77), PopSp(8), PushDSp(8))));
        Assert.Equal("v=5", Run.PutiExpr(Cat(Pushi(5), PopWp(4), PushDWp(4))));
        // sp.d: *(sp) + imm
        var c = Cat(PushWp(8), PopSp(0), Pushi(33), PopSpD(4), PushDWp(12));
        Assert.Equal("v=33", Run.PutiExpr(c));
    }

    [Fact]
    public void UnalignedFourByteAccessIsLittleEndian()
    {
        var vm = new BdVm();
        var p = new Prog();
        vm.Run(p.Build(Cat(Pushi(0x11223344), PopWp(1), Op(RET))), 1);
        int wp = (int)BdVm.WpAddr;
        var mem = vm.Memory;
        Assert.Equal(0x44, mem[wp + 1]);
        Assert.Equal(0x33, mem[wp + 2]);
        Assert.Equal(0x22, mem[wp + 3]);
        Assert.Equal(0x11, mem[wp + 4]);
        Assert.Equal("v=287454020", Run.PutiExpr(Cat(Pushi(0x11223344), PopWp(1), PushDWp(1))));
    }

    [Fact]
    public void BdSpaceIsReadableAndWritableInPs2()
    {
        var p = new Prog();
        int n = p.Str("v=");
        int d = p.Dword(0xDEADBEEFu);
        var read = Cat(PushBd(n), PushDBd(d), Syscall(0, 0), Op(RET));
        Assert.Equal("v=3735928559", Run.Bdx(p.Build(read)).Log);
        // pop.bd really writes in PS2 (unlike the PS3 build)
        var write = Cat(PushBd(n), Pushi(9), PopBd(d), PushDBd(d), Syscall(0, 0), Op(RET));
        Assert.Equal("v=9", Run.Bdx(p.Build(write)).Log);
    }

    [Fact]
    public void MemcpyFamily()
    {
        var p = new Prog();
        int s = p.Str("Hi");
        int n = p.Str("v=");

        // memcpy.wp: size 3, offset 0, src pointer from tp
        var c = Cat(PushBd(s), MemcpySpace(1, 3, 0), PushWp(0), Syscall(0, 2), Op(RET));
        Assert.Equal("Hi", Run.Bdx(p.Build(c)).Log);

        // memcpy ssub=3: dest below, src on top
        c = Cat(PushWp(16), PushBd(s), Memcpy4(3), PushWp(16), Syscall(0, 2), Op(RET));
        Assert.Equal("Hi", Run.Bdx(p.Build(c)).Log);

        // memcpy ssub=0: 4-byte store of the top value
        c = Cat(PushBd(n), PushWp(32), Pushi(unchecked((int)0xCAFEBABEu)), Memcpy4(0), PushDWp(32), Syscall(0, 0), Op(RET));
        Assert.Equal("v=3405691582", Run.Bdx(p.Build(c)).Log);

        // push.d.pop: *(pop + imm)
        c = Cat(PushBd(n), Pushi(99), PopWp(44), PushWp(40), Fetch(4), Syscall(0, 0), Op(RET));
        Assert.Equal("v=99", Run.Bdx(p.Build(c)).Log);
    }

    [Fact]
    public void TrapsPutsAndPutf()
    {
        var p = new Prog();
        int s = p.Str("hello");
        Assert.Equal("hello", Run.Bdx(p.Build(Cat(PushBd(s), Syscall(0, 2), Op(RET)))).Log);

        // (char)byte conversion, like bdEmu
        var q = new Prog();
        int e = q.Raw(0xE9, 0x00);
        Assert.Equal("é", Run.Bdx(q.Build(Cat(PushBd(e), Syscall(0, 2), Op(RET)))).Log);
    }

    [Fact]
    public void UnknownTrapAndTrigger()
    {
        var p = new Prog();
        var e = Assert.Throws<BdVmException>(() => Run.Bdx(p.Build(Cat(Syscall(0, 5), Op(RET)))));
        Assert.Contains("not registered", e.Message);

        e = Assert.Throws<BdVmException>(() => new BdVm().Run(p.Build(Op(RET)), 99));
        Assert.Contains("Trigger 99 not found!", e.Message);
    }

    [Fact]
    public void PushArgumentsAndKeepLastState()
    {
        // arguments pushed before the run are consumed by the script
        var p = new Prog();
        int n = p.Str("v=");
        var code = Cat(
            PopSp(0), PopSp(4), // sp[0]=4, sp[4]=3
            PushBd(n), PushDSp(4), PushDSp(0), Op(SUB),
            Syscall(0, 0), Op(RET));
        var r = new BdVm().Run(p.Build(code), 1, new[] { 3, 4 });
        Assert.Equal("v=4294967295", r.Log);

        // wp survives only with keepLastState
        var store = p.Build(Cat(Pushi(5), PopWp(0), Op(RET)));
        var load = p.Build(Cat(PushBd(n), PushDWp(0), Syscall(0, 0), Op(RET)));
        var vm = new BdVm();
        vm.Run(store, 1);
        Assert.Equal("v=5", vm.Run(load, 1, null, keepLastState: true).Log);
        Assert.Equal("v=0", vm.Run(load, 1, null, keepLastState: false).Log);
    }

    [Fact]
    public void Trigger0ThenTrigger1KeepsState()
    {
        // The same sequence BdxCEmuTests runs: trigger 0 with a dummy pushed 0, then trigger 1 with keepLastState.
        // Trigger order matters: bdEmu stops at the first key == 0 entry while looking for another key.
        var data = new List<byte>(new byte[] { (byte)'v', (byte)'=', 0, 0 });
        int dataOff = 0x1c + 8 * 3; // 3 pairs (2 triggers + terminator)
        int strPc = (dataOff - 0x10) / 2;
        // trigger 0 receives the dummy 0 that is pushed for it, and stores 7 into wp[4]
        var code0 = Cat(PopWp(0), Pushi(7), PopWp(4), Op(RET));
        var code1 = Cat(PushBd(strPc), PushDWp(4), Syscall(0, 0), Op(RET));
        int pc0 = (dataOff + data.Count - 0x10) / 2;
        int pc1 = pc0 + code0.Length;

        var v = new List<byte>();
        var name = new byte[16];
        name[0] = (byte)'b';
        v.AddRange(name);
        v.AddRange(BitConverter.GetBytes(64));
        v.AddRange(BitConverter.GetBytes(256));
        v.AddRange(BitConverter.GetBytes(256));
        foreach (var (k, e) in new[] { (1, pc1), (0, pc0), (0, 0) })
        {
            v.AddRange(BitConverter.GetBytes(k));
            v.AddRange(BitConverter.GetBytes(e));
        }
        v.AddRange(data);
        foreach (ushort w in code0.Concat(code1))
        {
            v.AddRange(BitConverter.GetBytes(w));
        }
        var bdx = v.ToArray();

        var vm = new BdVm();
        var r0 = vm.Run(bdx, 0, new[] { 0 }, false);
        var r1 = vm.Run(bdx, 1, null, true);
        Assert.Equal(BdStatus.Ret, r0.Status);
        Assert.Equal(BdStatus.Ret, r1.Status);
        Assert.Equal("v=7", r1.Log);

        // the trigger 0 entry placed first hides every later key (same loop as bdEmu)
        Assert.Throws<BdVmException>(() => new BdVm().Run(BuildSwapped(bdx), 1));
    }

    private static byte[] BuildSwapped(byte[] bdx)
    {
        // swaps the first two (key, entry) pairs
        var b = (byte[])bdx.Clone();
        for (int i = 0; i < 8; i++)
        {
            (b[0x1c + i], b[0x24 + i]) = (b[0x24 + i], b[0x1c + i]);
        }
        return b;
    }

    [Fact]
    public void OutOfRangeAccessIsAnExceptionNotUb()
    {
        var p = new Prog();
        var c = Cat(Pushi(-16), Fetch(0), Op(DROP), Op(RET));
        var e = Assert.Throws<BdVmException>(() => Run.Bdx(p.Build(c)));
        Assert.Contains("out of range", e.Message);
    }

    [Fact]
    public void EndlessLoopHitsStepLimit()
    {
        var p = new Prog();
        var vm = new BdVm { StepLimit = 1000 };
        var e = Assert.Throws<BdVmException>(() => vm.Run(p.Build(B(-2)), 1));
        Assert.Contains("step limit", e.Message);
    }

    [Fact]
    public void G7HelperMatchesExpectation()
    {
        Assert.Equal("3.402823E+38", G7(EeFpu.Fmax));
    }
}
