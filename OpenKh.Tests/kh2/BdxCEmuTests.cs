using OpenKh.Kh2Bdx.Models;
using OpenKh.Kh2Bdx.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;
using YamlDotNet.Serialization;

namespace OpenKh.Tests.kh2
{
    /// <summary>
    /// Runs the C programs in kh2/res/bdx-c-e2e on bdEmu, an emulator of the KH2 AI VM, and compares what they print.
    ///
    /// The other tests compare the generated text only, so they cannot notice a program that is compiled to
    /// wrong code, while the expectation was made from it. These tests run the code.
    ///
    /// bdEmu (kenjiuno/khkh_xldM) and MobileAiExec.bin (made from the game) are not part of this repository,
    /// so the tests are skipped unless both are configured:
    ///
    ///   BDEMU_EXE          path to bdEmu.exe
    ///   MOBILEAIEXECBIN    path to MobileAiExec.bin (read by bdEmu itself)
    ///
    /// Each program defines `main`, which runs as trigger 1 after trigger 0 (OBJ_INIT, the global initializers),
    /// and prints with trap_puti / trap_putf / trap_puts. NAME.expected is the whole text it must print.
    /// </summary>
    public class BdxCEmuTests
    {
        private const string ResDir = "kh2/res/bdx-c-e2e";

        public static IEnumerable<object[]> GetPrograms() =>
            Directory.GetFiles(ResDir, "*.c")
                .OrderBy(it => it, StringComparer.Ordinal)
                .Select(it => new object[] { Path.GetFileNameWithoutExtension(it) });

        [SkippableTheory]
        [MemberData(nameof(GetPrograms))]
        public void ProgramPrintsWhatIsExpected(string name)
        {
            var bdEmu = Environment.GetEnvironmentVariable("BDEMU_EXE");
            var vmBin = Environment.GetEnvironmentVariable("MOBILEAIEXECBIN");
            Skip.If(string.IsNullOrEmpty(bdEmu) || !File.Exists(bdEmu), "BDEMU_EXE is not set to bdEmu.exe");
            Skip.If(string.IsNullOrEmpty(vmBin) || !File.Exists(vmBin), "MOBILEAIEXECBIN is not set to MobileAiExec.bin");

            var cFile = Path.Combine(ResDir, name + ".c");
            var expected = File.ReadAllText(Path.Combine(ResDir, name + ".expected")).Trim();

            // C -> bdscript (with header) -> bdx, the same as `Bdxio encode-c --header` and `encode`
            var bdscript = BdxCScript.AddHeader(
                new BdxCEncoder().EncodeC(File.ReadAllText(cFile), cFile),
                cFile
            );
            var ascii = BdxAsciiModel.ParseText(bdscript);
            var bdx = new BdxEncoder(
                header: new DeserializerBuilder().Build().Deserialize<BdxHeader>(ascii.Header ?? ""),
                script: ascii.GetLineNumberRetainedScriptBody(),
                scriptName: cFile,
                loadScript: fileName => File.ReadAllText(fileName)
            ).Content;

            var workDir = Path.Combine(Path.GetTempPath(), "OpenKh.Tests.BdxCEmu", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workDir);
            try
            {
                var bdxFile = Path.Combine(workDir, name + ".bdx");
                var prefix = Path.Combine(workDir, name);
                File.WriteAllBytes(bdxFile, bdx);

                using (var process = Process.Start(new ProcessStartInfo
                {
                    FileName = bdEmu,
                    ArgumentList = { "exec", bdxFile, "1", "--trigger0", "--prefix", prefix },
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                })!)
                {
                    var stdout = process.StandardOutput.ReadToEndAsync();
                    var stderr = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(60_000))
                    {
                        process.Kill(entireProcessTree: true);
                        Assert.Fail($"{name}: bdEmu did not finish in 60 seconds");
                    }
                    process.WaitForExit();
                    Assert.True(File.Exists(prefix + ".json"), $"{name}: bdEmu wrote no result. {stdout.Result} {stderr.Result}");
                }

                using var json = JsonDocument.Parse(File.ReadAllText(prefix + ".json"));
                Assert.True(
                    json.RootElement.ValueKind == JsonValueKind.Array,
                    $"{name}: bdEmu failed: {json.RootElement.GetRawText()}"
                );

                // trigger 0 (OBJ_INIT) then trigger 1 (main); both must return normally
                var runs = json.RootElement.EnumerateArray().ToArray();
                Assert.Equal(2, runs.Length);
                Assert.All(runs, run => Assert.Equal("Ret", run.GetProperty("StatusCodeName").GetString()));

                Assert.Equal(expected, runs[1].GetProperty("ConsoleOut").GetString());
            }
            finally
            {
                try
                {
                    Directory.Delete(workDir, recursive: true);
                }
                catch (IOException)
                {
                    // a leftover temp directory does not matter
                }
            }
        }
    }
}
