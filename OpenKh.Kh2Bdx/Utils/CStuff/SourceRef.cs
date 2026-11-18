using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenKh.Kh2Bdx.Utils.CStuff
{
    internal record SourceRef(
        string SourceName,
        int Line,
        int Column)
    {
        /// <summary>The position of a token, as the line markers (`#line`) of the text say.</summary>
        internal static SourceRef FromToken(IToken token)
        {
            var stream = token.InputStream;
            if (stream != null && LineMap.For(stream) is { } map)
            {
                var (file, line) = map.Resolve(token.Line);
                return new SourceRef(file, line, token.Column);
            }

            return new SourceRef(stream?.SourceName ?? token.TokenSource.SourceName, token.Line, token.Column);
        }
    }
}
