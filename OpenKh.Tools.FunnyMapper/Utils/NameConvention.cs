using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenKh.Tools.FunnyMapper.Utils
{
    static class NameConvention
    {
        public static string NormalizeFileNameForId(string name)
        {
            return Regex.Replace(name, "[" + Regex.Escape("." + new string(Path.GetInvalidPathChars())) + "]", "_");
        }
    }
}
