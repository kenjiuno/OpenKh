using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenKh.Kh2Bdx.Utils.CStuff
{
    /// <summary>
    /// Line markers: what a preprocessor (such as `cpp`) leaves in its output to say where the lines came from.
    ///
    ///   #line 100 "foo.c"      the next line is line 100 of foo.c ("foo.c" is optional)
    ///   # 100 "foo.c" 1 3      the same, as `cpp` writes it (the numbers after the file name are flags)
    ///
    /// The compiler reads only these. Every other line that starts with `#` is ignored as before, so the macros and
    /// includes are for the preprocessor. The positions of the errors then point to the lines of the original file.
    /// </summary>
    internal class LineMap
    {
        private record Marker(int PhysicalLine, int NextLine, string? File);

        private readonly List<Marker> _markers = new();
        private readonly string _defaultName;

        private LineMap(string defaultName)
        {
            _defaultName = defaultName;
        }

        /// <summary>The name and line to report for a line of the text (1 based).</summary>
        internal (string File, int Line) Resolve(int physicalLine)
        {
            // the last marker before the line (the markers are in order)
            Marker? last = null;
            string file = _defaultName;
            foreach (var marker in _markers)
            {
                if (physicalLine <= marker.PhysicalLine)
                {
                    break;
                }
                last = marker;
                file = marker.File ?? file;
            }

            return last == null
                ? (file, physicalLine)
                : (file, last.NextLine + (physicalLine - last.PhysicalLine - 1));
        }

        private static readonly Regex LineDirective = new(
            @"^\s*#\s*line\b(?<rest>.*)$", RegexOptions.Compiled);

        private static readonly Regex LineDirectiveBody = new(
            @"^\s+(?<number>\d+)(?:\s+""(?<file>(?:[^""\\]|\\.)*)"")?\s*(?://.*|/\*.*)?$", RegexOptions.Compiled);

        private static readonly Regex GnuMarker = new(
            @"^\s*#\s*(?<number>\d+)\s+""(?<file>(?:[^""\\]|\\.)*)""(?:\s+\d+)*\s*$", RegexOptions.Compiled);

        /// <summary>Find the line markers in the text. An invalid `#line` is an error.</summary>
        internal static LineMap Parse(string script, string defaultName)
        {
            var map = new LineMap(defaultName);
            var lines = script.Replace("\r\n", "\n").Split('\n');

            var inComment = false;
            var continued = false;
            for (int x = 0; x < lines.Length; x++)
            {
                var line = lines[x];
                var physicalLine = x + 1;

                var wasInComment = inComment;
                var wasContinued = continued;
                inComment = TrackBlockComment(line, inComment);
                continued = line.TrimEnd('\r').EndsWith("\\") && (wasContinued || line.TrimStart().StartsWith("#"));

                if (wasInComment || wasContinued)
                {
                    continue;
                }

                if (LineDirective.Match(line) is { Success: true } directive)
                {
                    var body = LineDirectiveBody.Match(directive.Groups["rest"].Value);
                    if (!body.Success
                        || !int.TryParse(body.Groups["number"].Value, out var number)
                        || number < 1)
                    {
                        throw new CompilerException(new SourceRef(defaultName, physicalLine, 0), "Invalid #line: a line number of 1 or more, and an optional \"file\", are expected");
                    }
                    map._markers.Add(new Marker(physicalLine, number, body.Groups["file"].Success ? Unescape(body.Groups["file"].Value) : null));
                }
                else if (GnuMarker.Match(line) is { Success: true } gnu && int.TryParse(gnu.Groups["number"].Value, out var gnuNumber))
                {
                    map._markers.Add(new Marker(physicalLine, gnuNumber, Unescape(gnu.Groups["file"].Value)));
                }
            }

            return map;
        }

        /// <summary>Is a block comment still open at the end of the line? (the text of strings is not looked into)</summary>
        internal static bool TrackBlockComment(string line, bool inComment)
        {
            for (int x = 0; x < line.Length; x++)
            {
                if (inComment)
                {
                    if (line[x] == '*' && x + 1 < line.Length && line[x + 1] == '/')
                    {
                        inComment = false;
                        x++;
                    }
                }
                else if (line[x] == '/' && x + 1 < line.Length && line[x + 1] == '/')
                {
                    break;
                }
                else if (line[x] == '/' && x + 1 < line.Length && line[x + 1] == '*')
                {
                    inComment = true;
                    x++;
                }
                else if (line[x] == '"' || line[x] == '\'')
                {
                    var quote = line[x];
                    for (x++; x < line.Length && line[x] != quote; x++)
                    {
                        if (line[x] == '\\')
                        {
                            x++;
                        }
                    }
                }
            }
            return inComment;
        }

        private static string Unescape(string text)
        {
            var result = new StringBuilder();
            for (int x = 0; x < text.Length; x++)
            {
                if (text[x] == '\\' && x + 1 < text.Length)
                {
                    x++;
                }
                result.Append(text[x]);
            }
            return result.ToString();
        }

        // the map of the text of a character stream, found from its tokens
        private static readonly ConditionalWeakTable<ICharStream, LineMap> Maps = new();

        internal static void Attach(ICharStream stream, LineMap map)
        {
            Maps.Remove(stream);
            Maps.Add(stream, map);
        }

        internal static LineMap? For(ICharStream stream) => Maps.TryGetValue(stream, out var map) ? map : null;
    }
}
