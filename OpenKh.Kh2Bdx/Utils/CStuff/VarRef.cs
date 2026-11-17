using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenKh.Kh2Bdx.Utils.CStuff
{
    /// <param name="Elements">the elements of an array variable that is laid out at known places (locals, globals, members)</param>
    /// <param name="Slots">the word slots of a char array, whose bytes are packed 4 per slot (it has no Elements)</param>
    /// <param name="Prelude">
    /// Code that must run once before the members of a struct value can be read. Used by the result of a call that
    /// returns a struct: the call and the copy of the returned struct into a hidden local.
    /// </param>
    /// <param name="DiscardCode">
    /// Code of such a call that leaves the returned pointer on tp (no copy); it is followed by `drop` when the result is unused.
    /// </param>
    internal record VarRef(
        TypeDesc Type,
        string? Getter = null,
        string? Setter = null,
        string? GetPointer = null,
        IReadOnlyDictionary<string, VarRef>? Members = null,
        string? Prelude = null,
        string? DiscardCode = null,
        IReadOnlyList<VarRef>? Elements = null,
        IReadOnlyList<VarRef>? Slots = null,
        PointerTarget? Target = null);

    /// <summary>
    /// A variable that is reached through a pointer. Real data stores to it with `memcpy`: the destination address is
    /// pushed first, then the value.
    /// </summary>
    /// <param name="Address">code that pushes the address of the variable</param>
    /// <param name="Load">code that replaces the address on tp with the value</param>
    /// <param name="Size">4, or 1 / 2 for a char / short: stored with `memcpy 1` / `memcpy 2` from a slot</param>
    internal record PointerTarget(string Address, string Load, int Size = 4);
}
