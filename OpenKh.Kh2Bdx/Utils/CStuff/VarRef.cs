using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenKh.Kh2Bdx.Utils.CStuff
{
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
        string? DiscardCode = null);
}
