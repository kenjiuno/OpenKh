using McMaster.Extensions.CommandLineUtils;
using OpenKh.Kh2Bdx.Utils;
using System.ComponentModel.DataAnnotations;

namespace OpenKh.Command.Bdxio.Commands
{
    [HelpOption]
    [Command(Description = "run the c preprocessor only (#define, #include, #if ...). The output keeps the lines of the input and has line markers for the included files")]
    internal class CppCommand
    {
        [Required]
        [FileExists]
        [Argument(0, Description = "Input c file")]
        public string? InputFile { get; set; }

        [Argument(1, Description = "Output file (default: standard output)")]
        public string? OutputFile { get; set; }

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

        protected int OnExecute(CommandLineApplication app)
        {
            if (InputFile == null)
            {
                throw new NullReferenceException("InputFile must be set!");
            }

            var text = new CPreprocessor(CreateOptions(IncludeDirectories, Defines))
                .Process(File.ReadAllText(InputFile), InputFile);

            if (OutputFile == null)
            {
                Console.Out.Write(text);
            }
            else
            {
                File.WriteAllText(Path.GetFullPath(OutputFile), text);
            }
            return 0;
        }

        /// <summary>The options of -I and -D.</summary>
        internal static CPreprocessorOptions CreateOptions(string[] includeDirectories, string[] defines)
        {
            return new CPreprocessorOptions
            {
                IncludeProvider = new BuiltInIncludeProvider(new DirectoryIncludeProvider(includeDirectories)),
                Defines = defines
                    .Select(
                        it =>
                        {
                            var equals = it.IndexOf('=');
                            return equals < 0
                                ? new KeyValuePair<string, string>(it, "1")
                                : new KeyValuePair<string, string>(it.Substring(0, equals), it.Substring(equals + 1));
                        }
                    )
                    .ToArray(),
                Warning = message => Console.Error.WriteLine(message),
            };
        }
    }
}
