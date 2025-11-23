using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenKh.Kh2Bdx.Utils.CStuff
{
    internal record VarRef(
        TypeDesc Type,
        string? Getter = null,
        string? Setter = null,
        string? GetPointer = null);
}
