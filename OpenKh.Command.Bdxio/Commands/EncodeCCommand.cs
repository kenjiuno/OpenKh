using McMaster.Extensions.CommandLineUtils;
using NLog;
using OpenKh.Kh2Bdx.Utils;
using System.ComponentModel.DataAnnotations;

namespace OpenKh.Command.Bdxio.Commands
{
    [HelpOption]
    [Command(Description = "encode c")]
    internal class EncodeCCommand
    {
        [Required]
        [FileExists]
        [Argument(0, Description = "Input c file")]
        public string? InputFile { get; set; }

        [Argument(1, Description = "Output bdx file")]
        public string? OutputFile { get; set; }

        protected int OnExecute(CommandLineApplication app)
        {
            var logger = LogManager.GetLogger("Encode");

            if (InputFile == null)
            {
                throw new NullReferenceException("InputFile must be set!");
            }

            var outFile = Path.GetFullPath(OutputFile ?? Path.GetFileName(Path.ChangeExtension(InputFile, ".bdx")));

            logger.Debug($"Saving to: {outFile}");

            var result = new BdxCEncoder()
                .EncodeC(
                    File.ReadAllText(InputFile),
                    InputFile
                );
            File.WriteAllText(
                outFile,
                result.Bdscript
            );
            return 0;
        }
    }
}
