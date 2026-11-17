using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenKh.Kh2Bdx.Utils.CStuff
{
    /// <param name="ApplyNot">`~a`</param>
    internal record LLMath(
        string? ApplyNeg,
        string? ApplyAdd,
        string? ApplySub,
        string? ApplyDiv,
        string? ApplyMul,
        string? ApplyMod,
        string? ApplyNot,
        string? ApplyOr,
        string? ApplyAnd,
        string? ApplyXor,
        string? ApplySll,
        string? ApplySra,
        string? ApplyLAnd,
        string? ApplyLOr
        )
    {
        public static LLMath IntVer = new LLMath(
            ApplyNeg: "neg",
            ApplyAdd: "add",
            ApplySub: "sub",
            ApplyDiv: "div",
            ApplyMul: "mul",
            ApplyMod: "mod",
            ApplyNot: "not",
            ApplyOr: "or",
            ApplyAnd: "and",
            ApplyXor: "xor",
            ApplySll: "sll",
            ApplySra: "sra",
            ApplyLAnd: "land",
            ApplyLOr: "lor"
            );

        public static LLMath FloatVer = new LLMath(
            ApplyNeg: "neg.s",
            ApplyAdd: "add.s",
            ApplySub: "sub.s",
            ApplyDiv: "div.s",
            ApplyMul: "mul.s",
            ApplyMod: "mod.s",
            ApplyNot: null,
            ApplyOr: null,
            ApplyAnd: null,
            ApplyXor: null,
            ApplySll: null,
            ApplySra: null,
            ApplyLAnd: "land",
            ApplyLOr: "lor"
            );

        private static readonly Dictionary<int, LLMath> _pointerVers = new();

        /// <summary>
        /// Pointer arithmetic. `p + n` moves n elements, so n is multiplied by the size of one element in bytes.
        /// </summary>
        public static LLMath PointerVerOf(int stride)
        {
            lock (_pointerVers)
            {
                if (!_pointerVers.TryGetValue(stride, out var math))
                {
                    _pointerVers[stride] = math = PointerVerCore(stride);
                }
                return math;
            }
        }

        public static LLMath PointerVer => PointerVerOf(4);

        private static LLMath PointerVerCore(int stride) => new LLMath(
            ApplyNeg: null,
            ApplyAdd: $"push {stride}\nmul\nadd",
            ApplySub: $"push {stride}\nmul\nsub",
            ApplyDiv: null,
            ApplyMul: null,
            ApplyMod: null,
            ApplyNot: null,
            ApplyOr: null,
            ApplyAnd: null,
            ApplyXor: null,
            ApplySll: null,
            ApplySra: null,
            ApplyLAnd: "land",
            ApplyLOr: "lor"
            );
    }
}
