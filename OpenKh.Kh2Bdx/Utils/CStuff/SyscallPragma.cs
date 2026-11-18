using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace OpenKh.Kh2Bdx.Utils.CStuff
{
    /// <summary>`#pragma defsyscall name table index [override]`: the function `name` is the trap `index` of the table `table`.</summary>
    internal record SyscallPragma(string Name, int Table, int Index, bool Override, SourceRef Position);

    /// <summary>
    /// The `#pragma defsyscall` lines of the (preprocessed) text. The traps of the trap table need no pragma: it is for the
    /// ones that are not in the table, and for another name (or another number) of one that is (with `override`).
    /// A prototype gives the types of the arguments and of the result.
    /// </summary>
    internal static class SyscallPragmas
    {
        private static readonly Regex Pragma = new(@"^\s*#\s*pragma\s+defsyscall\b(?<rest>.*)$", RegexOptions.Compiled);
        private static readonly Regex Name = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        internal static List<SyscallPragma> Parse(string text, LineMap lineMap)
        {
            var pragmas = new List<SyscallPragma>();
            var inComment = false;
            var lines = text.Replace("\r\n", "\n").Split('\n');

            for (int x = 0; x < lines.Length; x++)
            {
                var line = lines[x];
                var wasInComment = inComment;
                inComment = LineMap.TrackBlockComment(line, inComment);
                if (wasInComment || Pragma.Match(line) is not { Success: true } match)
                {
                    continue;
                }

                var (file, number) = lineMap.Resolve(x + 1);
                var position = new SourceRef(file, number, 0);

                var rest = match.Groups["rest"].Value;
                var comment = rest.IndexOf("//", StringComparison.Ordinal);
                if (0 <= comment)
                {
                    rest = rest.Substring(0, comment);
                }
                var words = rest.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

                var isOverride = words.Length == 4 && words[3] == "override";
                if (!(words.Length == 3 || isOverride)
                    || !Name.IsMatch(words[0])
                    || !int.TryParse(words[1], out var table) || table < 0 || 1023 < table
                    || !int.TryParse(words[2], out var index) || index < 0 || 65535 < index)
                {
                    throw new CompilerException(
                        position,
                        "Invalid #pragma defsyscall: `#pragma defsyscall name table index [override]` is expected (table 0 to 1023, index 0 to 65535)"
                    );
                }

                pragmas.Add(new SyscallPragma(words[0], table, index, isOverride, position));
            }

            return pragmas;
        }
    }
}
