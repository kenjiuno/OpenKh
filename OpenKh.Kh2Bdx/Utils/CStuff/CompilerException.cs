using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenKh.Kh2Bdx.Utils.CStuff
{
    public class CompilerException : Exception
    {
        internal CompilerException(SourceRef sourceRef, string message)
            : base($"{sourceRef.SourceName}({sourceRef.Line}, {sourceRef.Column}): {message}") { }
    }
}
