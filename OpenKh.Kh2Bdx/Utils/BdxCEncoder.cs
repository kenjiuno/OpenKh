using Antlr4.Runtime;
using NLog;
using OpenKh.Kh2Bdx.Utils.CStuff;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenKh.Kh2Bdx.Utils
{
    public class BdxCEncoder
    {
        /// <param name="Bdscript">Generated bdscript body (no YAML header).</param>
        /// <param name="WorkSize">Total bytes reserved in .bss (wp).</param>
        public record Result(string Bdscript, int WorkSize = 0);

        /// <summary>Frame size (DWORDs) used to jal the `_initc` function: no locals, aligned to 16 bytes.</summary>
        public static int InitFrameSize => CHelper.FrameSizeOf(0);

        private record VarDef(
            TypeDesc Type,
            int Pointers,
            int Count);

        public Result EncodeC(
            string script,
            string scriptName,
            TextWriter? output = null,
            TextWriter? errorOutput = null,
            CPreprocessorOptions? preprocessor = null
        )
        {
            var logger = LogManager.GetLogger("BdxCEncoder");

            //TODO #pragma defsyscall trap_puti 0 0

            // the preprocessor runs first (unless it is disabled); its line markers give the errors the lines of the original files
            var preprocessorOptions = preprocessor ?? new CPreprocessorOptions();
            if (preprocessorOptions.Enabled)
            {
                script = new CPreprocessor(preprocessorOptions).Process(script, scriptName);
            }

            var stream = FromString(script, scriptName);

            // the line markers a preprocessor leaves (`#line 12 "file.c"`) say where the errors are to be reported
            LineMap.Attach(stream, LineMap.Parse(script, scriptName));
            var lexer = new CPP14Lexer(
                input: stream, 
                output: output ?? TextWriter.Null, 
                errorOutput: errorOutput ?? TextWriter.Null
            );
            var tokens = new CommonTokenStream(lexer);
            var parser = new CPP14Parser(
                input: tokens, 
                output: output ?? TextWriter.Null, 
                errorOutput: errorOutput ?? TextWriter.Null
            );
            var root = parser.translationUnit();

            var result = new TranslationUnitToCVisitor(
                GetSourceRef,
                new ReduceTypeDesc(GetSourceRef)
            )
                .Visit(root);

            using var writer = new StringWriter();

            {
                writer.WriteLine("_initc:");
                // _initc: global initializers, then ret. Called by the OBJ_INIT stub (see Bdxio encode-c --header).
                // It has no locals: frame = 2 system DWORDs, aligned to 16 bytes
                writer.WriteLine(result.InitCodeSeg.ToString()?.Replace(CHelper.FrameSizePlaceholder, CHelper.FrameSizeOf(0).ToString()));
                writer.WriteLine(" ret");
            }

            writer.WriteLine(result.CodeSeg);

            writer.WriteLine(result.StaticDataSeg);

            {
                var code = result.BssSeg.ToString();
                if (code?.Length != 0)
                {
                    writer.WriteLine("section .bss");
                    writer.WriteLine(code);
                }
            }

            return new Result(
                Bdscript: Cleanup(writer.ToString()),
                WorkSize: System.Text.RegularExpressions.Regex.Matches(result.BssSeg.ToString() ?? "", @"\bresb\s+(\d+)")
                    .Sum(it => int.Parse(it.Groups[1].Value)));
        }

        private string Cleanup(string body)
        {
            var lines = body
                .Replace("\r\n", "\n")
                .Split('\n')
                .Select(
                    line =>
                    {
                        if (line.Trim().Length == 0)
                        {
                            return "";
                        }
                        else
                        {
                            var labelMark = line.IndexOf(':');
                            var commentMark = line.IndexOf(';');
                            if (1 <= labelMark && (commentMark < 0 || labelMark < commentMark))
                            {
                                return line.Trim();
                            }
                            else
                            {
                                return " " + line.Trim();
                            }
                        }
                    }
                )
                .Where(line => line.Length != 0);

            return string.Join("\n", lines);
        }

        private SourceRef GetSourceRef(ParserRuleContext token)
        {
            if (token == null)
            {
                throw new Exception(nameof(token));
            }

            return SourceRef.FromToken(token.Start);
        }

        private static ICharStream FromString(string script, string sourceName)
        {
            var stream = CharStreams.fromString(script);
            if (stream is CodePointCharStream charStream)
            {
                charStream.name = sourceName;
            }
            return stream;
        }
    }
}
