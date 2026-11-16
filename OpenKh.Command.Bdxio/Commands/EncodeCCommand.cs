using McMaster.Extensions.CommandLineUtils;
using NLog;
using OpenKh.Kh2Bdx.Utils;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace OpenKh.Command.Bdxio.Commands
{
    [HelpOption]
    [Command(Description = "compile c to bdscript text (use `encode` to make a bdx file from it)")]
    internal class EncodeCCommand
    {
        [Required]
        [FileExists]
        [Argument(0, Description = "Input c file")]
        public string? InputFile { get; set; }

        [Argument(1, Description = "Output bdscript file (default: input file name with .bdscript)")]
        public string? OutputFile { get; set; }

        [Option(
            LongName = "header",
            ShortName = "H",
            Description = "Prepend a YAML header (WorkSize, StackSize, TempSize, Triggers, Name) so that the output can be passed to `encode` as is")]
        public bool WithHeader { get; set; }

        [Option(
            LongName = "trigger",
            ShortName = "t",
            Description = "Trigger number written into the header (default: 0)")]
        public int Trigger { get; set; } = 0;

        [Option(
            LongName = "entry",
            ShortName = "e",
            Description = "Name of the C function the trigger starts (default: main)")]
        public string Entry { get; set; } = "main";

        [Option(
            LongName = "name",
            ShortName = "",
            Description = "Name written into the header, up to 16 characters (default: input file name without extension)")]
        public string? Name { get; set; }

        [Option(LongName = "stack-size", ShortName = "", Description = "StackSize written into the header (default: 512)")]
        public int StackSize { get; set; } = 512;

        [Option(LongName = "temp-size", ShortName = "", Description = "TempSize written into the header (default: 512)")]
        public int TempSize { get; set; } = 512;

        protected int OnExecute(CommandLineApplication app)
        {
            var logger = LogManager.GetLogger("EncodeC");

            if (InputFile == null)
            {
                throw new NullReferenceException("InputFile must be set!");
            }

            var outFile = Path.GetFullPath(OutputFile ?? Path.GetFileName(Path.ChangeExtension(InputFile, ".bdscript")));

            logger.Debug($"Saving to: {outFile}");

            var result = new BdxCEncoder()
                .EncodeC(
                    File.ReadAllText(InputFile),
                    InputFile
                );

            var text = new StringBuilder();

            if (WithHeader)
            {
                if (!result.Bdscript.Replace("\r\n", "\n").Split('\n').Any(line => line.StartsWith(Entry + ":")))
                {
                    throw new InvalidOperationException($"Entry function '{Entry}' is not defined in {InputFile}. Specify it with --entry.");
                }

                var name = Name ?? Path.GetFileNameWithoutExtension(InputFile);
                if (16 < name.Length)
                {
                    name = name.Substring(0, 16);
                }

                text.AppendLine("---");
                text.AppendLine($"WorkSize: {result.WorkSize}");
                text.AppendLine($"StackSize: {StackSize}");
                text.AppendLine($"TempSize: {TempSize}");
                text.AppendLine("Triggers:");
                text.AppendLine($"- Key: {Trigger}");
                text.AppendLine($"  Addr: {Entry}");
                text.AppendLine($"Name: {name}");
                text.AppendLine("---");
                text.AppendLine(" section .text");
            }

            text.AppendLine(result.Bdscript);

            File.WriteAllText(outFile, text.ToString());
            return 0;
        }
    }
}
