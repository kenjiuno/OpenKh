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
            Description = "Trigger number that starts the entry function (default: 1 = OBJ_MAIN). 0 is reserved: it is OBJ_INIT, which always runs the global initializers (_initc)")]
        public int Trigger { get; set; } = 1;

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

        [Option(
            CommandOptionType.MultipleValue,
            LongName = "include-dir",
            ShortName = "I",
            Description = "Directory to look for the files of #include (can be repeated)")]
        public string[] IncludeDirectories { get; set; } = Array.Empty<string>();

        [Option(
            CommandOptionType.MultipleValue,
            LongName = "define",
            ShortName = "D",
            Description = "Define a macro: NAME, NAME=value or NAME(a,b)=body (can be repeated). NAME alone is 1")]
        public string[] Defines { get; set; } = Array.Empty<string>();

        [Option(
            LongName = "no-preprocess",
            ShortName = "",
            Description = "Do not run the preprocessor: #define, #include and so on are ignored")]
        public bool NoPreprocess { get; set; }

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
                    InputFile,
                    preprocessor: CppCommand.CreateOptions(IncludeDirectories, Defines) with { Enabled = !NoPreprocess }
                );

            var text = WithHeader
                ? BdxCScript.AddHeader(result, InputFile, Trigger, Entry, Name, StackSize, TempSize)
                : result.Bdscript + Environment.NewLine;

            File.WriteAllText(outFile, text);
            return 0;
        }
    }
}
