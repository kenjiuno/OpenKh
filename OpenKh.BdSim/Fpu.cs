namespace OpenKh.BdSim;

/// <summary>Which single-precision arithmetic <see cref="BdVm"/> uses for the *.s instructions, sin/cos/degr/radd.</summary>
public enum FpuMode
{
    /// <summary>
    /// Plain host IEEE-754 arithmetic. This is what bdEmu does: ee1DecExtra turns ADD.S / MUL.S / DIV.S
    /// into the ordinary .NET + * / operators (the vuDouble clamp of Custeera is only used by VU code).
    /// x / 0 gives Infinity, overflow gives Infinity, 0 / 0 gives NaN.
    /// </summary>
    Ieee = 0,

    /// <summary>EE Core FPU rules from INST_E.pdf, see <see cref="EeFpu"/> (no Inf/NaN/denormals).</summary>
    Ee = 1,
}

/// <summary>Dispatches the float operations of <see cref="BdVm"/> on <see cref="FpuMode"/>.</summary>
internal static class Fpu
{
    public static float Operand(FpuMode m, uint bits) =>
        m == FpuMode.Ee ? EeFpu.Operand(bits) : BitConverter.UInt32BitsToSingle(bits);

    public static uint Result(FpuMode m, float f) =>
        m == FpuMode.Ee ? EeFpu.Result(f) : BitConverter.SingleToUInt32Bits(f);

    public static uint Add(FpuMode m, uint a, uint b) => Result(m, Operand(m, a) + Operand(m, b));

    public static uint Sub(FpuMode m, uint a, uint b) => Result(m, Operand(m, a) - Operand(m, b));

    public static uint Mul(FpuMode m, uint a, uint b) => Result(m, Operand(m, a) * Operand(m, b));

    public static uint Div(FpuMode m, uint a, uint b) =>
        m == FpuMode.Ee ? EeFpu.Div(a, b) : Result(m, Operand(m, a) / Operand(m, b));

    /// <summary>fmodf. Not an FPU instruction (game library code), so neither mode is verified.</summary>
    public static uint Mod(FpuMode m, uint a, uint b) =>
        m == FpuMode.Ee ? EeFpu.Mod(a, b) : Result(m, Operand(m, a) % Operand(m, b));
}
