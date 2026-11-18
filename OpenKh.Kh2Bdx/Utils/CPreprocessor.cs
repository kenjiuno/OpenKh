using OpenKh.Kh2Bdx.Utils.CStuff;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenKh.Kh2Bdx.Utils
{
    /// <summary>Finds and reads the files that `#include` names. The compiler does not have to run on a file system.</summary>
    public interface IIncludeProvider
    {
        /// <summary>
        /// Find the file of `#include "name"` (<paramref name="angled"/> is false) or `#include &lt;name&gt;`.
        /// Returns an id that is the same for the same file (it is used to include a file once, to find cycles and to
        /// name the file in the line markers), or null if there is no such file.
        /// </summary>
        /// <param name="includingId">the id of the file that has the `#include`; `"name"` is looked for next to it first</param>
        string? Find(string name, bool angled, string includingId);

        /// <summary>The text of a file that <see cref="Find"/> found.</summary>
        string Load(string id);
    }

    /// <summary>Files in directories: `"name"` is next to the including file, then in the include directories; `&lt;name&gt;` only in the directories.</summary>
    public sealed class DirectoryIncludeProvider : IIncludeProvider
    {
        private readonly string[] _directories;

        public DirectoryIncludeProvider(IEnumerable<string> includeDirectories)
        {
            _directories = includeDirectories.ToArray();
        }

        public string? Find(string name, bool angled, string includingId)
        {
            var candidates = new List<string>();
            if (!angled)
            {
                candidates.Add(Path.Combine(Path.GetDirectoryName(includingId) ?? "", name));
            }
            candidates.AddRange(_directories.Select(it => Path.Combine(it, name)));

            return candidates.Where(File.Exists).Select(Path.GetFullPath).FirstOrDefault();
        }

        public string Load(string id) => File.ReadAllText(id);
    }

    /// <summary>Files in memory (for tests, and for sources that do not come from files). The ids are the names with `/`.</summary>
    public sealed class MemoryIncludeProvider : IIncludeProvider
    {
        private readonly IReadOnlyDictionary<string, string> _files;

        public MemoryIncludeProvider(IReadOnlyDictionary<string, string> files)
        {
            _files = files;
        }

        public string? Find(string name, bool angled, string includingId)
        {
            var candidates = new List<string>();
            if (!angled)
            {
                var slash = includingId.LastIndexOf('/');
                candidates.Add(slash < 0 ? name : includingId.Substring(0, slash + 1) + name);
            }
            candidates.Add(name);

            return candidates.FirstOrDefault(_files.ContainsKey);
        }

        public string Load(string id) => _files[id];
    }

    public record CPreprocessorOptions
    {
        /// <summary>false: the source is compiled as it is (`#` lines are ignored)</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>where `#include` finds files; without it `#include` is an error</summary>
        public IIncludeProvider? IncludeProvider { get; set; }

        /// <summary>macros defined before the source (`-D NAME=value`): the name (with `(a, b)` for a function macro) and the replacement</summary>
        public IEnumerable<KeyValuePair<string, string>>? Defines { get; set; }

        /// <summary>called with the text of `#warning` and the like</summary>
        public Action<string>? Warning { get; set; }

        public int MaxIncludeDepth { get; set; } = 200;
    }

    /// <summary>
    /// A small C preprocessor.
    ///
    ///   #define #undef (object and function macros; no `#`, `##` and variable arguments yet)
    ///   #include (the files come from <see cref="IIncludeProvider"/>)
    ///   #if #ifdef #ifndef #elif #else #endif (integer expressions, `defined`)
    ///   #error #warning #pragma once
    ///
    /// `#pragma` (other than once), `#line` and the line markers are left in the text. The output has the same
    /// lines as the input, except for what an `#include` brings, which is between line markers (`# 1 "file.h" 1`
    /// ... `# 12 "main.c" 2`), so that the compiler reports the positions of the original files.
    /// </summary>
    public sealed class CPreprocessor
    {
        private readonly CPreprocessorOptions _options;

        public CPreprocessor(CPreprocessorOptions? options = null)
        {
            _options = options ?? new CPreprocessorOptions();
        }

        /// <param name="source">the text</param>
        /// <param name="name">the name of the text, for the errors, `__FILE__` and for `#include "..."` next to it</param>
        public string Process(string source, string name) => new Run(_options).Execute(source, name);

        private enum Kind { Identifier, Number, String, Punct, Other }

        private sealed class Tok
        {
            public Kind Kind;
            public string Text = "";
            /// <summary>spaces before the token (the column of the first one on a line)</summary>
            public int Pad;
            public int Line;
            public int Column;
            public string File = "";
            public ImmutableHashSet<string>? Hide;
            /// <summary>made by a macro expansion: it must not become one token with its neighbours</summary>
            public bool Synth;

            public Tok Clone() => (Tok)MemberwiseClone();
            public bool Is(string text) => Kind == Kind.Punct && Text == text;
            public SourceRef Position => new(File, Line, Column);
        }

        private sealed class LogicalLine
        {
            public List<Tok> Tokens = new();
            public int StartLine;
            /// <summary>lines of the text it takes (a backslash joins lines)</summary>
            public int PhysicalLines = 1;
            public bool IsDirective => Tokens.Count != 0 && Tokens[0].Is("#");
        }

        private sealed class Macro
        {
            public string Name = "";
            public List<string>? Params;
            /// <summary>`F(a, ...)`: the arguments after the named ones are `__VA_ARGS__`</summary>
            public bool Variadic;
            public List<Tok> Body = new();

            public bool SameAs(Macro other) =>
                (Params == null) == (other.Params == null)
                && Variadic == other.Variadic
                && (Params == null || Params.SequenceEqual(other.Params!))
                && Body.Count == other.Body.Count
                && Body.Zip(other.Body).All(it => it.First.Text == it.Second.Text && (it.First.Pad != 0) == (it.Second.Pad != 0));
        }

        private sealed class Cond
        {
            public bool Active;
            public bool Taken;
            public bool ParentActive;
            public bool SawElse;
            public Tok Start = null!;
        }

        private static readonly string[] Punctuators =
        {
            "...", "<<=", ">>=",
            "##", "->", "++", "--", "<<", ">>", "<=", ">=", "==", "!=", "&&", "||",
            "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "::",
        };

        private sealed class Run
        {
            private readonly CPreprocessorOptions _options;
            private readonly Dictionary<string, Macro> _macros = new();
            private readonly HashSet<string> _once = new();
            private readonly StringBuilder _output = new();

            public Run(CPreprocessorOptions options)
            {
                _options = options;
            }

            public string Execute(string source, string name)
            {
                foreach (var define in _options.Defines ?? Array.Empty<KeyValuePair<string, string>>())
                {
                    var line = Lex($"#define {define.Key} {define.Value}\n", "<command line>").Single();
                    Define(line.Tokens);
                }

                ProcessFile(source, name, 0);
                return _output.ToString();
            }

            // ---- reading the text ----

            private static bool IsIdentStart(char c) => c == '_' || char.IsLetter(c);
            private static bool IsIdentChar(char c) => c == '_' || char.IsLetterOrDigit(c);

            /// <summary>The lines of the text with the comments and the backslash-newlines taken out.</summary>
            private static List<LogicalLine> Lex(string source, string file)
            {
                var text = source.Replace("\r\n", "\n");
                var lines = new List<LogicalLine>();
                var current = new LogicalLine { StartLine = 1 };
                var line = 1;
                var lineStart = 0;
                var pad = 0;
                var pos = 0;

                while (pos < text.Length)
                {
                    var c = text[pos];
                    var next = pos + 1 < text.Length ? text[pos + 1] : '\0';

                    if (c == '\n')
                    {
                        lines.Add(current);
                        line++;
                        pos++;
                        lineStart = pos;
                        pad = 0;
                        current = new LogicalLine { StartLine = line };
                    }
                    else if (c == '\\' && next == '\n')
                    {
                        pos += 2;
                        line++;
                        current.PhysicalLines++;
                        lineStart = pos;
                        pad = 1;
                    }
                    else if (c is ' ' or '\t' or '\f' or '\v' or '\r')
                    {
                        pos++;
                        pad++;
                    }
                    else if (c == '/' && next == '*')
                    {
                        var end = text.IndexOf("*/", pos + 2, StringComparison.Ordinal);
                        if (end < 0)
                        {
                            throw new CompilerException(new SourceRef(file, line, pos - lineStart), "Unterminated comment");
                        }
                        end += 2;
                        var newlines = 0;
                        for (int x = pos; x < end; x++)
                        {
                            if (text[x] == '\n')
                            {
                                newlines++;
                                lineStart = x + 1;
                            }
                        }
                        line += newlines;
                        current.PhysicalLines += newlines;
                        pad = newlines == 0 ? pad + (end - pos) : end - lineStart;
                        pos = end;
                    }
                    else if (c == '/' && next == '/')
                    {
                        var start = pos;
                        while (pos < text.Length && text[pos] != '\n')
                        {
                            if (text[pos] == '\\' && pos + 1 < text.Length && text[pos + 1] == '\n')
                            {
                                pos += 2;
                                line++;
                                current.PhysicalLines++;
                                lineStart = pos;
                                continue;
                            }
                            pos++;
                        }
                        pad += 1;
                    }
                    else
                    {
                        var start = pos;
                        Kind kind;
                        if (IsIdentStart(c))
                        {
                            kind = Kind.Identifier;
                            while (pos < text.Length && IsIdentChar(text[pos])) pos++;
                        }
                        else if (char.IsDigit(c) || (c == '.' && char.IsDigit(next)))
                        {
                            kind = Kind.Number;
                            pos++;
                            while (pos < text.Length)
                            {
                                if (IsIdentChar(text[pos]) || text[pos] == '.')
                                {
                                    pos++;
                                }
                                else if ((text[pos] == '+' || text[pos] == '-') && "eEpP".IndexOf(text[pos - 1]) >= 0)
                                {
                                    pos++;
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                        else if (c == '"' || c == '\'')
                        {
                            var close = pos + 1;
                            while (close < text.Length && text[close] != c && text[close] != '\n')
                            {
                                close += (text[close] == '\\' && close + 1 < text.Length && text[close + 1] != '\n') ? 2 : 1;
                            }
                            if (close < text.Length && text[close] == c)
                            {
                                kind = Kind.String;
                                pos = close + 1;
                            }
                            else
                            {
                                // an apostrophe in the text of `#error don't`: not a literal
                                kind = Kind.Other;
                                pos++;
                            }
                        }
                        else
                        {
                            var punct = Punctuators.FirstOrDefault(it => string.CompareOrdinal(text, pos, it, 0, it.Length) == 0);
                            pos += punct?.Length ?? 1;
                            kind = (punct != null || (c < 128 && !char.IsControl(c))) ? Kind.Punct : Kind.Other;
                        }

                        current.Tokens.Add(new Tok
                        {
                            Kind = kind,
                            Text = text.Substring(start, pos - start),
                            Pad = current.Tokens.Count == 0 ? start - lineStart : pad,
                            Line = line,
                            Column = start - lineStart,
                            File = file,
                        });
                        pad = 0;
                    }
                }

                if (current.Tokens.Count != 0 || current.PhysicalLines != 1)
                {
                    lines.Add(current);
                }
                return lines;
            }

            // ---- the file ----

            private void ProcessFile(string text, string id, int depth)
            {
                var lines = Lex(text, id);
                var conditions = new Stack<Cond>();

                for (int index = 0; index < lines.Count;)
                {
                    var line = lines[index++];
                    var active = conditions.Count == 0 || conditions.Peek().Active;

                    if (line.IsDirective)
                    {
                        Directive(line, conditions, active, id, depth);
                        continue;
                    }

                    if (!active || line.Tokens.Count == 0)
                    {
                        Newlines(line.PhysicalLines);
                        continue;
                    }

                    // a call of a function macro can go on to the following lines
                    var extra = 0;
                    var expanded = Expand(
                        line.Tokens,
                        () =>
                        {
                            if (index < lines.Count && !lines[index].IsDirective)
                            {
                                var more = lines[index++];
                                extra += more.PhysicalLines;
                                return more.Tokens;
                            }
                            return null;
                        }
                    );
                    _output.Append(Render(expanded));
                    Newlines(line.PhysicalLines + extra);
                }

                if (conditions.Count != 0)
                {
                    throw new CompilerException(conditions.Peek().Start.Position, "Unterminated conditional directive: #endif is missing");
                }
            }

            private void Newlines(int count) => _output.Append('\n', count);

            private static string Render(IReadOnlyList<Tok> tokens)
            {
                var text = new StringBuilder();
                Tok? previous = null;
                foreach (var token in tokens)
                {
                    var pad = token.Pad;
                    if (pad == 0 && previous != null && (token.Synth || previous.Synth) && Glues(previous, token))
                    {
                        pad = 1;
                    }
                    text.Append(' ', pad).Append(token.Text);
                    previous = token;
                }
                return text.ToString();
            }

            /// <summary>Would the two tokens, put together, read as other tokens?</summary>
            private static bool Glues(Tok left, Tok right)
            {
                var a = left.Text[^1];
                var b = right.Text[0];
                if (IsIdentChar(a) && (IsIdentChar(b) || b == '.'))
                {
                    return true;
                }
                if (left.Kind == Kind.Punct && right.Kind == Kind.Punct)
                {
                    return "+-<>=&|!*/%^#.:".IndexOf(a) >= 0 && "+-<>=&|*/#.:".IndexOf(b) >= 0 || (a == '-' && b == '>');
                }
                return false;
            }

            // ---- directives ----

            private void Directive(LogicalLine line, Stack<Cond> conditions, bool active, string id, int depth)
            {
                var tokens = line.Tokens;
                if (tokens.Count == 1)
                {
                    Newlines(line.PhysicalLines); // the null directive
                    return;
                }

                var nameToken = tokens[1];
                var rest = tokens.Skip(2).ToList();

                if (nameToken.Kind == Kind.Number)
                {
                    // a line marker of a preprocessor that has run before (`# 12 "file.c" 1`): left as it is for the compiler
                    PassThrough(line);
                    return;
                }

                switch (nameToken.Text)
                {
                    case "if":
                        {
                            var parentActive = active;
                            var value = parentActive && Evaluate(rest, nameToken);
                            conditions.Push(new Cond { ParentActive = parentActive, Active = value, Taken = value || !parentActive, Start = nameToken });
                            break;
                        }
                    case "ifdef":
                    case "ifndef":
                        {
                            var name = SingleName(rest, nameToken);
                            var value = active && (_macros.ContainsKey(name) == (nameToken.Text == "ifdef"));
                            conditions.Push(new Cond { ParentActive = active, Active = value, Taken = value || !active, Start = nameToken });
                            break;
                        }
                    case "elif":
                        {
                            var cond = conditions.Count != 0 ? conditions.Peek() : throw new CompilerException(nameToken.Position, "#elif without #if");
                            if (cond.SawElse)
                            {
                                throw new CompilerException(nameToken.Position, "#elif after #else");
                            }
                            if (!cond.ParentActive || cond.Taken)
                            {
                                cond.Active = false;
                            }
                            else
                            {
                                cond.Active = cond.Taken = Evaluate(rest, nameToken);
                            }
                            break;
                        }
                    case "else":
                        {
                            var cond = conditions.Count != 0 ? conditions.Peek() : throw new CompilerException(nameToken.Position, "#else without #if");
                            if (cond.SawElse)
                            {
                                throw new CompilerException(nameToken.Position, "#else after #else");
                            }
                            cond.SawElse = true;
                            cond.Active = cond.ParentActive && !cond.Taken;
                            cond.Taken = true;
                            break;
                        }
                    case "endif":
                        {
                            if (conditions.Count == 0)
                            {
                                throw new CompilerException(nameToken.Position, "#endif without #if");
                            }
                            conditions.Pop();
                            break;
                        }
                    case "define" when active:
                        Define(tokens);
                        break;
                    case "undef" when active:
                        _macros.Remove(SingleName(rest, nameToken));
                        break;
                    case "include" when active:
                        Include(line, rest, nameToken, id, depth);
                        return;
                    case "error" when active:
                        throw new CompilerException(nameToken.Position, "#error " + Render(rest).Trim());
                    case "warning" when active:
                        _options.Warning?.Invoke($"{nameToken.File}({nameToken.Line}, {nameToken.Column}): #warning {Render(rest).Trim()}");
                        break;
                    case "pragma" when active:
                        if (rest.Count != 0 && rest[0].Text == "once")
                        {
                            _once.Add(id);
                            break;
                        }
                        PassThrough(line);
                        return;
                    case "line" when active:
                        PassThrough(line);
                        return;
                    case "define" or "undef" or "include" or "error" or "warning" or "pragma" or "line":
                        break; // in a part that is skipped
                    default:
                        if (active)
                        {
                            throw new CompilerException(nameToken.Position, $"Unknown preprocessing directive #{nameToken.Text}");
                        }
                        break;
                }

                Newlines(line.PhysicalLines);
            }

            private void PassThrough(LogicalLine line)
            {
                var tokens = line.Tokens.Select(it => it.Clone()).ToList();
                tokens[0].Pad = 0;
                _output.Append(Render(tokens));
                Newlines(line.PhysicalLines);
            }

            private static string SingleName(List<Tok> tokens, Tok directive)
            {
                if (tokens.Count != 1 || tokens[0].Kind != Kind.Identifier)
                {
                    throw new CompilerException(directive.Position, $"#{directive.Text} expects one name");
                }
                return tokens[0].Text;
            }

            private void Define(List<Tok> directive)
            {
                var nameToken = directive.Count > 2 ? directive[2] : throw new CompilerException(directive[1].Position, "#define expects a name");
                if (nameToken.Kind != Kind.Identifier)
                {
                    throw new CompilerException(nameToken.Position, "#define expects a name");
                }
                if (nameToken.Text is "defined" or "__LINE__" or "__FILE__")
                {
                    throw new CompilerException(nameToken.Position, $"'{nameToken.Text}' cannot be defined");
                }

                var macro = new Macro { Name = nameToken.Text };
                var bodyStart = 3;

                // a function macro has `(` right after the name
                if (directive.Count > 3 && directive[3].Is("(") && directive[3].Pad == 0)
                {
                    macro.Params = new List<string>();
                    var x = 4;
                    if (x < directive.Count && directive[x].Is(")"))
                    {
                        x++;
                    }
                    else
                    {
                        while (true)
                        {
                            if (x >= directive.Count)
                            {
                                throw new CompilerException(nameToken.Position, "Unterminated parameter list of the macro");
                            }
                            if (directive[x].Is("..."))
                            {
                                // `...` is the last parameter: `F(...)`, `F(a, b, ...)`
                                if (x + 1 >= directive.Count || !directive[x + 1].Is(")"))
                                {
                                    throw new CompilerException(directive[x].Position, "'...' must be the last parameter of the macro");
                                }
                                macro.Variadic = true;
                                x += 2;
                                break;
                            }
                            if (directive[x].Kind != Kind.Identifier || directive[x].Text == "__VA_ARGS__")
                            {
                                throw new CompilerException(directive[x].Position, "A parameter name is expected");
                            }
                            if (macro.Params.Contains(directive[x].Text))
                            {
                                throw new CompilerException(directive[x].Position, $"Parameter '{directive[x].Text}' is repeated");
                            }
                            macro.Params.Add(directive[x].Text);
                            x++;
                            if (x < directive.Count && directive[x].Is(","))
                            {
                                x++;
                                continue;
                            }
                            if (x < directive.Count && directive[x].Is(")"))
                            {
                                x++;
                                break;
                            }
                            throw new CompilerException(nameToken.Position, "Unterminated parameter list of the macro");
                        }
                    }
                    bodyStart = x;
                }

                foreach (var token in directive.Skip(bodyStart))
                {
                    var copy = token.Clone();
                    copy.Pad = Math.Min(token.Pad, 1);
                    macro.Body.Add(copy);
                }
                if (macro.Body.Count != 0)
                {
                    macro.Body[0].Pad = 0;
                }

                // `#` and `##` and `__VA_ARGS__` have to be used the way they are meant
                bool IsParameter(Tok token) =>
                    token.Kind == Kind.Identifier && macro.Params != null
                    && (macro.Params.Contains(token.Text) || (macro.Variadic && token.Text == "__VA_ARGS__"));

                for (int x = 0; x < macro.Body.Count; x++)
                {
                    var token = macro.Body[x];
                    if (token.Is("##") && (x == 0 || x == macro.Body.Count - 1))
                    {
                        throw new CompilerException(token.Position, "'##' cannot be at either end of the replacement of a macro");
                    }
                    if (macro.Params != null && token.Is("#") && !(x + 1 < macro.Body.Count && IsParameter(macro.Body[x + 1])))
                    {
                        throw new CompilerException(token.Position, "'#' is not followed by a macro parameter");
                    }
                    if (token.Kind == Kind.Identifier && token.Text == "__VA_ARGS__" && !macro.Variadic)
                    {
                        throw new CompilerException(token.Position, "__VA_ARGS__ can be used only in the replacement of a macro with '...'");
                    }
                }

                if (_macros.TryGetValue(macro.Name, out var old) && !old.SameAs(macro))
                {
                    _options.Warning?.Invoke($"{nameToken.File}({nameToken.Line}, {nameToken.Column}): '{macro.Name}' is redefined");
                }
                _macros[macro.Name] = macro;
            }

            private void Include(LogicalLine line, List<Tok> rest, Tok directive, string id, int depth)
            {
                string name;
                bool angled;

                bool TryHeaderName(List<Tok> tokens, out string headerName, out bool isAngled)
                {
                    headerName = "";
                    isAngled = false;
                    if (tokens.Count >= 1 && tokens[0].Kind == Kind.String && tokens[0].Text[0] == '"')
                    {
                        headerName = tokens[0].Text[1..^1];
                        return true;
                    }
                    if (tokens.Count >= 3 && tokens[0].Is("<"))
                    {
                        var end = tokens.FindIndex(1, it => it.Is(">"));
                        if (0 < end)
                        {
                            headerName = string.Concat(tokens.Skip(1).Take(end - 1).Select((it, i) => (i == 0 ? "" : new string(' ', it.Pad)) + it.Text));
                            isAngled = true;
                            return true;
                        }
                    }
                    return false;
                }

                if (!TryHeaderName(rest, out name, out angled) && !TryHeaderName(Expand(rest, null), out name, out angled))
                {
                    throw new CompilerException(directive.Position, "#include expects \"file\" or <file>");
                }

                var provider = _options.IncludeProvider
                    ?? throw new CompilerException(directive.Position, $"Cannot include '{name}': no include provider is given");
                var found = provider.Find(name, angled, id)
                    ?? throw new CompilerException(directive.Position, $"Cannot find the include file '{name}'");

                if (_once.Contains(found))
                {
                    Newlines(line.PhysicalLines);
                    return;
                }

                if (_options.MaxIncludeDepth < depth + 1)
                {
                    throw new CompilerException(directive.Position, $"#include is nested more than {_options.MaxIncludeDepth} levels deep");
                }

                _output.Append($"# 1 \"{Escape(found)}\" 1\n");
                ProcessFile(provider.Load(found), found, depth + 1);
                _output.Append($"# {line.StartLine + line.PhysicalLines} \"{Escape(id)}\" 2\n");
            }

            private static string Escape(string text) => text.Replace("\\", "\\\\").Replace("\"", "\\\"");

            // ---- macros ----

            private static ImmutableHashSet<string> Union(ImmutableHashSet<string>? a, ImmutableHashSet<string>? b, string? extra = null)
            {
                var result = a ?? ImmutableHashSet<string>.Empty;
                if (b != null)
                {
                    result = result.Union(b);
                }
                return extra != null ? result.Add(extra) : result;
            }

            /// <summary>
            /// Expand the macros in the tokens. A token that comes from a macro knows which macros it came from
            /// (a hide set), so that a macro is not expanded again inside its own expansion.
            /// </summary>
            /// <param name="pull">more tokens, when the call of a function macro goes on to the next line</param>
            private List<Tok> Expand(IReadOnlyList<Tok> input, Func<List<Tok>?>? pull)
            {
                var output = new List<Tok>();
                var stack = new List<Tok>(input.Reverse()); // the end is the next token

                void Push(IEnumerable<Tok> tokens) => stack.AddRange(tokens.Reverse());

                bool FillFromNextLines()
                {
                    while (stack.Count == 0 && pull != null)
                    {
                        var more = pull();
                        if (more == null)
                        {
                            return false;
                        }
                        Push(more);
                    }
                    return stack.Count != 0;
                }

                while (stack.Count != 0)
                {
                    var token = stack[^1];
                    stack.RemoveAt(stack.Count - 1);

                    if (token.Kind != Kind.Identifier)
                    {
                        output.Add(token);
                        continue;
                    }

                    if (token.Text == "__LINE__" || token.Text == "__FILE__")
                    {
                        var value = token.Clone();
                        value.Synth = true;
                        value.Kind = token.Text == "__LINE__" ? Kind.Number : Kind.String;
                        value.Text = token.Text == "__LINE__" ? token.Line.ToString() : $"\"{Escape(token.File)}\"";
                        output.Add(value);
                        continue;
                    }

                    if ((token.Hide?.Contains(token.Text) ?? false) || !_macros.TryGetValue(token.Text, out var macro))
                    {
                        output.Add(token);
                        continue;
                    }

                    if (macro.Params == null)
                    {
                        Push(Substitute(macro, null, token, Union(token.Hide, null, macro.Name)));
                        continue;
                    }

                    // a function macro is called only when `(` follows
                    if (!FillFromNextLines() || !stack[^1].Is("("))
                    {
                        output.Add(token);
                        continue;
                    }
                    stack.RemoveAt(stack.Count - 1);

                    var arguments = new List<List<Tok>> { new() };
                    var depth = 1;
                    Tok closing;
                    while (true)
                    {
                        if (!FillFromNextLines())
                        {
                            throw new CompilerException(token.Position, $"The call of the macro '{macro.Name}' is not closed");
                        }
                        var next = stack[^1];
                        stack.RemoveAt(stack.Count - 1);

                        if (next.Is("("))
                        {
                            depth++;
                        }
                        else if (next.Is(")"))
                        {
                            depth--;
                            if (depth == 0)
                            {
                                closing = next;
                                break;
                            }
                        }
                        else if (next.Is(",") && depth == 1 && !(macro.Variadic && arguments.Count > macro.Params.Count))
                        {
                            // (the commas of `__VA_ARGS__` stay in it)
                            arguments.Add(new List<Tok>());
                            continue;
                        }
                        arguments[^1].Add(next);
                    }

                    if (macro.Params.Count == 0 && arguments.Count == 1 && arguments[0].Count == 0)
                    {
                        arguments.Clear();
                    }
                    // a macro with `...` takes the named arguments and then any number (also none) for __VA_ARGS__
                    if (macro.Variadic ? arguments.Count < macro.Params.Count : arguments.Count != macro.Params.Count)
                    {
                        throw new CompilerException(
                            token.Position,
                            $"The macro '{macro.Name}' takes {(macro.Variadic ? "at least " : "")}{macro.Params.Count} argument(s) but {arguments.Count} given"
                        );
                    }

                    var hide = (token.Hide ?? ImmutableHashSet<string>.Empty)
                        .Intersect(closing.Hide ?? ImmutableHashSet<string>.Empty)
                        .Add(macro.Name);
                    Push(Substitute(macro, arguments, token, hide));
                }

                return output;
            }

            /// <summary>
            /// The replacement of a macro: its body, with the arguments in place of the parameters.
            ///
            ///   a parameter                 the argument, with its macros expanded first
            ///   #parameter                  the argument (as it is written) in a string
            ///   a ## b, parameter ## ...    the two tokens next to ## become one token; the arguments next to ## are not expanded
            ///   , ## __VA_ARGS__            the comma goes when there are no variable arguments (as `gcc` does)
            /// </summary>
            private List<Tok> Substitute(Macro macro, List<List<Tok>>? arguments, Tok call, ImmutableHashSet<string> hide)
            {
                int ParameterIndex(Tok token)
                {
                    if (arguments == null || token.Kind != Kind.Identifier)
                    {
                        return -1;
                    }
                    var index = macro.Params!.IndexOf(token.Text);
                    return 0 <= index ? index : (macro.Variadic && token.Text == "__VA_ARGS__" ? macro.Params.Count : -1);
                }

                // the argument as it is written; there may be none for __VA_ARGS__
                List<Tok> RawArgument(int index) => index < arguments!.Count ? arguments[index] : new List<Tok>();

                Tok FromBody(Tok bodyToken)
                {
                    var copy = bodyToken.Clone();
                    copy.Synth = true;
                    copy.Hide = Union(copy.Hide, hide);
                    // errors and __LINE__ are at the call
                    copy.Line = call.Line;
                    copy.Column = call.Column;
                    copy.File = call.File;
                    return copy;
                }

                IEnumerable<Tok> FromArgument(IEnumerable<Tok> tokens, int firstPad)
                {
                    var first = true;
                    foreach (var token in tokens)
                    {
                        var copy = token.Clone();
                        copy.Pad = first ? firstPad : Math.Min(copy.Pad, 1);
                        copy.Synth = true;
                        copy.Hide = Union(copy.Hide, hide);
                        first = false;
                        yield return copy;
                    }
                }

                var result = new List<Tok>();
                var body = macro.Body;
                var placemarker = false; // the last thing was a parameter that has no tokens: next to `##` it is nothing to paste to

                for (int i = 0; i < body.Count; i++)
                {
                    var token = body[i];

                    if (arguments != null && token.Is("#"))
                    {
                        // the operand is a parameter (checked when the macro was defined)
                        result.Add(Stringify(RawArgument(ParameterIndex(body[i + 1])), token, call, hide));
                        placemarker = false;
                        i++;
                        continue;
                    }

                    if (token.Is("##"))
                    {
                        var right = body[i + 1];
                        var rightIndex = ParameterIndex(right);
                        i++;

                        // `, ## __VA_ARGS__`
                        if (rightIndex == macro.Params?.Count && macro.Variadic && arguments != null
                            && !placemarker && result.Count != 0 && result[^1].Is(","))
                        {
                            var variable = RawArgument(rightIndex);
                            if (variable.Count == 0)
                            {
                                result.RemoveAt(result.Count - 1);
                            }
                            else
                            {
                                result.AddRange(FromArgument(variable, 1));
                            }
                            placemarker = false;
                            continue;
                        }

                        var rightTokens = 0 <= rightIndex ? FromArgument(RawArgument(rightIndex), 0).ToList() : new List<Tok> { FromBody(right) };
                        if (rightTokens.Count == 0)
                        {
                            placemarker = true;
                            continue;
                        }

                        if (result.Count == 0 || placemarker)
                        {
                            rightTokens[0].Pad = Math.Min(right.Pad, 1);
                            result.AddRange(rightTokens);
                        }
                        else
                        {
                            result[^1] = Paste(result[^1], rightTokens[0], call, hide);
                            result.AddRange(rightTokens.Skip(1));
                        }
                        placemarker = false;
                        continue;
                    }

                    var index = ParameterIndex(token);
                    if (0 <= index)
                    {
                        // next to `##` the argument is not expanded
                        var beforePaste = i + 1 < body.Count && body[i + 1].Is("##");
                        var tokens = beforePaste ? RawArgument(index) : Expand(RawArgument(index), null);
                        var copies = FromArgument(tokens, token.Pad).ToList();
                        result.AddRange(copies);
                        placemarker = copies.Count == 0;
                    }
                    else
                    {
                        result.Add(FromBody(token));
                        placemarker = false;
                    }
                }

                if (result.Count != 0)
                {
                    result[0].Pad = call.Pad;
                }
                return result;
            }

            /// <summary>`#x`: the argument as it is written, in a string (with a space where there was space).</summary>
            private static Tok Stringify(List<Tok> argument, Tok hash, Tok call, ImmutableHashSet<string> hide)
            {
                var text = new StringBuilder("\"");
                for (int x = 0; x < argument.Count; x++)
                {
                    var token = argument[x];
                    if (0 < x && 0 < token.Pad)
                    {
                        text.Append(' ');
                    }
                    // a string or a character keeps its quotes, with the backslashes and the double quotes escaped
                    text.Append(token.Kind == Kind.String ? token.Text.Replace("\\", "\\\\").Replace("\"", "\\\"") : token.Text);
                }
                text.Append('"');

                return new Tok
                {
                    Kind = Kind.String,
                    Text = text.ToString(),
                    Pad = hash.Pad,
                    Line = call.Line,
                    Column = call.Column,
                    File = call.File,
                    Synth = true,
                    Hide = Union(null, hide),
                };
            }

            /// <summary>`a ## b`: the two tokens written together must make one token.</summary>
            private static Tok Paste(Tok left, Tok right, Tok call, ImmutableHashSet<string> hide)
            {
                var text = left.Text + right.Text;
                var lines = Lex(text + "\n", call.File);
                if (lines.Count != 1 || lines[0].Tokens.Count != 1 || lines[0].Tokens[0].Text != text)
                {
                    throw new CompilerException(call.Position, $"Pasting '{left.Text}' and '{right.Text}' does not make a token");
                }

                var pasted = lines[0].Tokens[0];
                pasted.Pad = left.Pad;
                pasted.Line = call.Line;
                pasted.Column = call.Column;
                pasted.Synth = true;
                pasted.Hide = (left.Hide ?? ImmutableHashSet<string>.Empty).Intersect(right.Hide ?? ImmutableHashSet<string>.Empty).Union(hide);
                return pasted;
            }

            // ---- #if ----

            private bool Evaluate(List<Tok> condition, Tok directive)
            {
                if (condition.Count == 0)
                {
                    throw new CompilerException(directive.Position, $"#{directive.Text} expects an expression");
                }

                // `defined X` and `defined(X)` before the macros are expanded
                var replaced = new List<Tok>();
                for (int x = 0; x < condition.Count; x++)
                {
                    var token = condition[x];
                    if (token.Kind == Kind.Identifier && token.Text == "defined")
                    {
                        var parenthesis = x + 1 < condition.Count && condition[x + 1].Is("(");
                        var nameIndex = x + (parenthesis ? 2 : 1);
                        if (nameIndex >= condition.Count || condition[nameIndex].Kind != Kind.Identifier
                            || (parenthesis && (nameIndex + 1 >= condition.Count || !condition[nameIndex + 1].Is(")"))))
                        {
                            throw new CompilerException(token.Position, "`defined` expects a name");
                        }
                        var value = token.Clone();
                        value.Kind = Kind.Number;
                        value.Text = _macros.ContainsKey(condition[nameIndex].Text) ? "1" : "0";
                        replaced.Add(value);
                        x = nameIndex + (parenthesis ? 1 : 0);
                    }
                    else
                    {
                        replaced.Add(token);
                    }
                }

                var parser = new ExpressionParser(Expand(replaced, null), directive);
                var result = parser.Parse();
                return result != 0;
            }
        }

        /// <summary>The expression of `#if`: integers (64 bits), the operators of C, and 0 for a name.</summary>
        private sealed class ExpressionParser
        {
            private readonly List<Tok> _tokens;
            private readonly Tok _directive;
            private int _pos;

            public ExpressionParser(List<Tok> tokens, Tok directive)
            {
                _tokens = tokens;
                _directive = directive;
            }

            public long Parse()
            {
                var value = Conditional(true);
                if (_pos < _tokens.Count)
                {
                    throw Error(_tokens[_pos], $"Unexpected '{_tokens[_pos].Text}' in the expression");
                }
                return value;
            }

            private static CompilerException Error(Tok at, string message) => new(at.Position, message);

            private Tok? Peek => _pos < _tokens.Count ? _tokens[_pos] : null;

            private bool Match(string punct)
            {
                if (Peek is { } token && token.Is(punct))
                {
                    _pos++;
                    return true;
                }
                return false;
            }

            // `evaluate` is false in the side of an operator that is not used: a division by zero there is not an error
            private long Conditional(bool evaluate)
            {
                var condition = LogicalOr(evaluate);
                if (!Match("?"))
                {
                    return condition;
                }
                var whenTrue = Conditional(evaluate && condition != 0);
                if (!Match(":"))
                {
                    throw Error(Peek ?? _directive, "':' is expected in the expression");
                }
                var whenFalse = Conditional(evaluate && condition == 0);
                return condition != 0 ? whenTrue : whenFalse;
            }

            private long LogicalOr(bool evaluate)
            {
                var left = LogicalAnd(evaluate);
                while (Match("||"))
                {
                    var right = LogicalAnd(evaluate && left == 0);
                    left = (left != 0 || right != 0) ? 1 : 0;
                }
                return left;
            }

            private long LogicalAnd(bool evaluate)
            {
                var left = BitOr(evaluate);
                while (Match("&&"))
                {
                    var right = BitOr(evaluate && left != 0);
                    left = (left != 0 && right != 0) ? 1 : 0;
                }
                return left;
            }

            private long BitOr(bool evaluate)
            {
                var left = BitXor(evaluate);
                while (Peek is { } token && token.Is("|"))
                {
                    _pos++;
                    left |= BitXor(evaluate);
                }
                return left;
            }

            private long BitXor(bool evaluate)
            {
                var left = BitAnd(evaluate);
                while (Match("^"))
                {
                    left ^= BitAnd(evaluate);
                }
                return left;
            }

            private long BitAnd(bool evaluate)
            {
                var left = Equality(evaluate);
                while (Peek is { } token && token.Is("&"))
                {
                    _pos++;
                    left &= Equality(evaluate);
                }
                return left;
            }

            private long Equality(bool evaluate)
            {
                var left = Relational(evaluate);
                while (true)
                {
                    if (Match("==")) left = left == Relational(evaluate) ? 1 : 0;
                    else if (Match("!=")) left = left != Relational(evaluate) ? 1 : 0;
                    else return left;
                }
            }

            private long Relational(bool evaluate)
            {
                var left = Shift(evaluate);
                while (true)
                {
                    if (Match("<=")) left = left <= Shift(evaluate) ? 1 : 0;
                    else if (Match(">=")) left = left >= Shift(evaluate) ? 1 : 0;
                    else if (Match("<")) left = left < Shift(evaluate) ? 1 : 0;
                    else if (Match(">")) left = left > Shift(evaluate) ? 1 : 0;
                    else return left;
                }
            }

            private long Shift(bool evaluate)
            {
                var left = Additive(evaluate);
                while (true)
                {
                    if (Match("<<")) left <<= (int)(Additive(evaluate) & 63);
                    else if (Match(">>")) left >>= (int)(Additive(evaluate) & 63);
                    else return left;
                }
            }

            private long Additive(bool evaluate)
            {
                var left = Multiplicative(evaluate);
                while (true)
                {
                    if (Match("+")) left += Multiplicative(evaluate);
                    else if (Match("-")) left -= Multiplicative(evaluate);
                    else return left;
                }
            }

            private long Multiplicative(bool evaluate)
            {
                var left = Unary(evaluate);
                while (true)
                {
                    var at = Peek;
                    if (Match("*"))
                    {
                        left *= Unary(evaluate);
                    }
                    else if (Match("/") || Match("%"))
                    {
                        var divide = at!.Text == "/";
                        var right = Unary(evaluate);
                        if (right == 0)
                        {
                            if (evaluate)
                            {
                                throw Error(at, "Division by zero in the expression");
                            }
                            left = 0;
                        }
                        else
                        {
                            left = divide ? left / right : left % right;
                        }
                    }
                    else
                    {
                        return left;
                    }
                }
            }

            private long Unary(bool evaluate)
            {
                if (Match("+")) return Unary(evaluate);
                if (Match("-")) return -Unary(evaluate);
                if (Match("!")) return Unary(evaluate) == 0 ? 1 : 0;
                if (Match("~")) return ~Unary(evaluate);
                return Primary(evaluate);
            }

            private long Primary(bool evaluate)
            {
                var token = Peek ?? throw Error(_directive, "The expression is not complete");
                _pos++;

                if (token.Is("("))
                {
                    var value = Conditional(evaluate);
                    if (!Match(")"))
                    {
                        throw Error(Peek ?? token, "')' is expected in the expression");
                    }
                    return value;
                }

                switch (token.Kind)
                {
                    case Kind.Number:
                        return ParseNumber(token);
                    case Kind.String when token.Text[0] == '\'':
                        {
                            var bytes = ExpressionToVarRefVisitor.UnescapeStringLiteral(token.Text, token.Position);
                            return bytes.Length == 0 ? 0 : bytes[0];
                        }
                    case Kind.Identifier:
                        return 0; // a name that is not a macro
                    default:
                        throw Error(token, $"Unexpected '{token.Text}' in the expression");
                }
            }

            private static long ParseNumber(Tok token)
            {
                var text = token.Text.TrimEnd('u', 'U', 'l', 'L');
                try
                {
                    if (text.StartsWith("0x") || text.StartsWith("0X")) return Convert.ToInt64(text.Substring(2), 16);
                    if (text.StartsWith("0b") || text.StartsWith("0B")) return Convert.ToInt64(text.Substring(2), 2);
                    if (text.Length > 1 && text[0] == '0') return Convert.ToInt64(text.Substring(1), 8);
                    return long.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
                }
                catch (Exception e) when (e is FormatException or OverflowException or ArgumentException)
                {
                    throw Error(token, $"'{token.Text}' is not an integer");
                }
            }
        }
    }
}
