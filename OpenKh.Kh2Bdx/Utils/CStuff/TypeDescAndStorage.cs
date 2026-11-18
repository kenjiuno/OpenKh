using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenKh.Kh2Bdx.Utils.CStuff
{
    /// <param name="Typedef">`typedef T name;`: the declarators name types, not variables</param>
    internal record TypeDescAndStorage(
        TypeDesc TypeDesc,
        bool Static,
        bool Typedef = false)
    {
    }
}
