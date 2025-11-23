using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenKh.Kh2Bdx.Utils.CStuff
{
    internal record LLComparators(
        string? Seqz,
        string? Sgez,
        string? Sgtz,
        string? Slez,
        string? Sltz,
        string? Snez
        )
    {
        public static LLComparators IntVer = new LLComparators(
            Seqz: "seqz",
            Sgez: "sgez",
            Sgtz: "sgtz",
            Slez: "slez",
            Sltz: "sltz",
            Snez: "snez"
            );

        public static LLComparators FloatVer = new LLComparators(
            Seqz: "seqz.s",
            Sgez: "sgez.s",
            Sgtz: "sgtz.s",
            Slez: "slez.s",
            Sltz: "sltz.s",
            Snez: "snez.s"
            );
    }
}
