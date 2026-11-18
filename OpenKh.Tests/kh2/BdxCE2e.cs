using OpenKh.Kh2Bdx.Models;
using OpenKh.Kh2Bdx.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;

namespace OpenKh.Tests.kh2
{
    /// <summary>The C programs in kh2/res/bdx-c-e2e, compiled to bdx, for the tests that run them (on bdEmu and on bdSim).</summary>
    internal static class BdxCE2e
    {
        public const string ResDir = "kh2/res/bdx-c-e2e";

        public static IEnumerable<object[]> GetPrograms() =>
            Directory.GetFiles(ResDir, "*.c")
                .OrderBy(it => it, StringComparer.Ordinal)
                .Select(it => new object[] { Path.GetFileNameWithoutExtension(it) });

        /// <summary>The whole text the program must print (NAME.expected).</summary>
        public static string Expected(string name) =>
            File.ReadAllText(Path.Combine(ResDir, name + ".expected")).Trim();

        /// <summary>C -> bdscript (with header) -> bdx, the same as `Bdxio encode-c --header` and `encode`.</summary>
        public static byte[] CompileToBdx(string name)
        {
            var cFile = Path.Combine(ResDir, name + ".c");
            var bdscript = BdxCScript.AddHeader(
                new BdxCEncoder().EncodeC(File.ReadAllText(cFile), cFile),
                cFile
            );
            var ascii = BdxAsciiModel.ParseText(bdscript);
            return new BdxEncoder(
                header: new DeserializerBuilder().Build().Deserialize<BdxHeader>(ascii.Header ?? ""),
                script: ascii.GetLineNumberRetainedScriptBody(),
                scriptName: cFile,
                loadScript: fileName => File.ReadAllText(fileName)
            ).Content;
        }
    }
}
