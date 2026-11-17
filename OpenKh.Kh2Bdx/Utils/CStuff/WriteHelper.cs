using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenKh.Kh2Bdx.Utils.CStuff
{
    internal record WriteHelper(
        TextWriter CodeSeg,
        TextWriter InitCodeSeg,
        int ExpectedOuts,
        Func<Func<string, string>> NextUniqueLabelGenerator,
        CHelper CHelper,
        LoopLabels? Loop = null,
        TypeDesc? RetType = null,
        IReadOnlyDictionary<CPP14Parser.LabeledStatementContext, string>? CaseLabels = null,
        IReadOnlyDictionary<string, string>? UserLabels = null
        );

    /// <param name="Break">where `break` jumps to (end of the loop or switch)</param>
    /// <param name="Continue">where `continue` jumps to, or null inside a switch that is not in a loop</param>
    internal record LoopLabels(string Break, string? Continue);
}
