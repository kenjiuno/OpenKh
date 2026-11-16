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

        private record VarDef(
            TypeDesc Type,
            int Pointers,
            int Count);

        public Result EncodeC(
            string script,
            string scriptName,
            TextWriter? output = null,
            TextWriter? errorOutput = null
        )
        {
            var logger = LogManager.GetLogger("BdxCEncoder");

            //TODO #pragma defsyscall trap_puti 0 0

            var stream = FromString(script, scriptName);
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
                // _initc has no locals: frame = 2 system DWORDs
                writer.WriteLine(result.InitCodeSeg.ToString()?.Replace(CHelper.FrameSizePlaceholder, "2"));
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

            var start = token.Start;

            return new SourceRef(
                SourceName: start.InputStream.SourceName,
                Line: start.Line,
                Column: start.Column);
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
