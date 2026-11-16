using System;
using System.Linq;
using System.Text;

namespace OpenKh.Kh2Bdx.Utils
{
    /// <summary>
    /// Makes a bdscript file, which `encode` can turn into a bdx, from the result of the C compiler.
    /// </summary>
    public static class BdxCScript
    {
        /// <summary>
        /// Prepend the YAML header, the OBJ_INIT stub and `section .text` to the compiled body.
        /// </summary>
        /// <param name="sourceName">name of the C source, for messages and the default <paramref name="name"/></param>
        /// <param name="trigger">trigger number that starts <paramref name="entry"/>. 0 is reserved for OBJ_INIT.</param>
        /// <param name="entry">name of the C function the trigger starts</param>
        /// <param name="name">name in the header, up to 16 characters (default: source file name without extension)</param>
        public static string AddHeader(
            BdxCEncoder.Result result,
            string sourceName,
            int trigger = 1,
            string entry = "main",
            string? name = null,
            int stackSize = 512,
            int tempSize = 512)
        {
            if (trigger <= 0)
            {
                throw new ArgumentException("--trigger must be 1 or greater: trigger 0 is OBJ_INIT and runs the global initializers.");
            }

            if (!result.Bdscript.Replace("\r\n", "\n").Split('\n').Any(line => line.StartsWith(entry + ":")))
            {
                throw new InvalidOperationException($"Entry function '{entry}' is not defined in {sourceName}. Specify it with --entry.");
            }

            name ??= System.IO.Path.GetFileNameWithoutExtension(sourceName);
            if (16 < name.Length)
            {
                name = name.Substring(0, 16);
            }

            var text = new StringBuilder();
            text.AppendLine("---");
            text.AppendLine($"WorkSize: {result.WorkSize}");
            text.AppendLine($"StackSize: {stackSize}");
            text.AppendLine($"TempSize: {tempSize}");
            text.AppendLine("Triggers:");
            // Keys are in descending order like the real data. Key 0 (OBJ_INIT) must come last: the VM
            // scans the table and treats a key of 0 as its end (after checking it for a match), so an
            // earlier key 0 would hide the following triggers.
            text.AppendLine($"- Key: {trigger}");
            text.AppendLine($"  Addr: {entry}");
            text.AppendLine("- Key: 0");
            text.AppendLine("  Addr: OBJ_INIT");
            text.AppendLine($"Name: {name}");
            text.AppendLine("---");
            text.AppendLine(" section .text");
            // OBJ_INIT is called with `this` (YS::OBJ *) on tp, which the C program does not use.
            // Run the global initializers, then discard `this` and return.
            text.AppendLine("OBJ_INIT:");
            text.AppendLine($" jal {BdxCEncoder.InitFrameSize}, _initc");
            text.AppendLine(" drop");
            text.AppendLine(" ret");
            text.AppendLine(result.Bdscript);
            return text.ToString();
        }
    }
}
