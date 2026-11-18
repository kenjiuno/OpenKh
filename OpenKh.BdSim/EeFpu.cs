namespace OpenKh.BdSim;

/// <summary>
/// Single precision arithmetic with the EE Core FPU conventions described in
/// "EE Core Instruction Set Manual" (INST_E.pdf): there are no Inf/NaN/denormals.
///
/// - an operand with exponent 0 (zero or denormal) is treated as +-0
/// - an operand with exponent 255 (Inf/NaN) is treated as +-Fmax (0x7F7FFFFF)
/// - an exponent overflow gives +-Fmax, an exponent underflow gives +-0
/// - x / 0 (and 0 / 0) gives +-Fmax, the sign being sign(x) xor sign(y)
///
/// Used by <see cref="FpuMode.Ee"/> only. bdEmu itself does NOT apply these rules: it emits plain .NET
/// operators for ADD.S / MUL.S / DIV.S, which is <see cref="FpuMode.Ieee"/> (the default of BdVm).
/// NOT modelled: the EE rounds toward zero, this class uses the host's round-to-nearest.
/// </summary>
public static class EeFpu
{
    public const uint Fmax = 0x7F7FFFFFu;
    private const uint SignBit = 0x80000000u;
    private const uint ExpMask = 0x7F800000u;

    /// <summary>Clamps raw bits to the value set the EE FPU can hold.</summary>
    public static uint Normalize(uint bits)
    {
        uint exp = bits & ExpMask;
        if (exp == 0)
        {
            return bits & SignBit;
        }
        if (exp == ExpMask)
        {
            return (bits & SignBit) | Fmax;
        }
        return bits;
    }

    public static float Operand(uint bits) => BitConverter.UInt32BitsToSingle(Normalize(bits));

    public static uint Result(float f) => Normalize(BitConverter.SingleToUInt32Bits(f));

    public static uint Add(uint a, uint b) => Result(Operand(a) + Operand(b));

    public static uint Sub(uint a, uint b) => Result(Operand(a) - Operand(b));

    public static uint Mul(uint a, uint b) => Result(Operand(a) * Operand(b));

    public static uint Div(uint a, uint b)
    {
        float y = Operand(b);
        if (y == 0.0f)
        {
            return (((a ^ b) & SignBit) != 0) ? (SignBit | Fmax) : Fmax;
        }
        return Result(Operand(a) / y);
    }

    /// <summary>fmodf (truncated remainder). Not an FPU instruction: x % 0 is treated like x / 0. Unverified.</summary>
    public static uint Mod(uint a, uint b)
    {
        float y = Operand(b);
        if (y == 0.0f)
        {
            return (((a ^ b) & SignBit) != 0) ? (SignBit | Fmax) : Fmax;
        }
        return Result(Operand(a) % y);
    }

    /// <summary>CVT.W.S: truncation; a biased exponent above 0x9D is clamped by sign.</summary>
    public static uint CvtW(uint bits)
    {
        if ((bits & ExpMask) <= 0x4E800000u)
        {
            return unchecked((uint)(int)BitConverter.UInt32BitsToSingle(bits));
        }
        return (bits & SignBit) != 0 ? 0x80000000u : 0x7FFFFFFFu;
    }

    /// <summary>CVT.S.W: signed 32-bit integer to single.</summary>
    public static uint CvtS(uint bits) => BitConverter.SingleToUInt32Bits((float)unchecked((int)bits));
}
