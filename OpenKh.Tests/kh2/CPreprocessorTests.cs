using OpenKh.Kh2Bdx.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenKh.Tests.kh2
{
    public class CPreprocessorTests
    {
        private static string Run(string source, IDictionary<string, string>? files = null, IDictionary<string, string>? defines = null, List<string>? warnings = null)
        {
            return new CPreprocessor(
                new CPreprocessorOptions
                {
                    IncludeProvider = new MemoryIncludeProvider((files ?? new Dictionary<string, string>()).ToDictionary(it => it.Key, it => it.Value)),
                    Defines = defines,
                    Warning = warnings != null ? warnings.Add : null,
                }
            ).Process(source, "main.c");
        }

        private static string Error(string source, IDictionary<string, string>? files = null) =>
            Assert.ThrowsAny<Exception>(() => Run(source, files)).Message;

        [Fact]
        public void TextWithoutDirectivesIsUnchanged()
        {
            var text = "int main() {\n    return 1; /* c */\n}\n";
            // the comment is gone and the columns of the rest are kept
            Assert.Equal("int main() {\n    return 1;\n}\n", Run(text));
        }

        [Fact]
        public void ObjectMacroIsReplaced()
        {
            Assert.Equal("\nint a[10];\n", Run("#define N 10\nint a[N];\n"));
        }

        [Fact]
        public void MacroIsNotReplacedInStringsAndComments()
        {
            Assert.Equal("\nchar *s = \"N\";\nint n = 3;\n", Run("#define N 3\nchar *s = \"N\"; // N\nint n = N;\n"));
        }

        [Fact]
        public void FunctionMacroTakesArguments()
        {
            Assert.Equal("\nint m = ((1) > (2) ? (1) : (2));\n", Run("#define MAX(a, b) ((a) > (b) ? (a) : (b))\nint m = MAX(1, 2);\n"));
        }

        [Fact]
        public void FunctionMacroNameWithoutParenthesisIsNotACall()
        {
            Assert.Equal("\nint F; int x = F;\n", Run("#define F(a) a\nint F; int x = F;\n").Replace("int x = F ;", "int x = F;"));
        }

        [Fact]
        public void ArgumentsAreExpandedAndParenthesesAreCounted()
        {
            Assert.Equal("\n\nint x = (2 + (3, 4));\n", Run("#define K 2\n#define ID(x) (x)\nint x = ID(K + (3, 4));\n"));
        }

        [Fact]
        public void MacroInMacroAndNoRecursion()
        {
            Assert.Equal("\n\nint x = 1 + 2; int y = 1 + 2;\n", Run("#define A 1 + B\n#define B 2\nint x = A; int y = A;\n"));
        }

        [Fact]
        public void AMacroIsNotExpandedInsideItself()
        {
            Assert.Equal("\n\nint x = A + B;\n", Run("#define A B\n#define B A + B\nint x = A;\n").Replace("A + B", "A + B"));
        }

        [Fact]
        public void CallOfAFunctionMacroCanSpanLinesAndKeepsTheLineCount()
        {
            Assert.Equal("\nint x = (1 + 2);\n\n\nint y;\n", Run("#define ADD(a, b) (a + b)\nint x = ADD(1,\n 2);\n\nint y;\n"));
        }

        [Fact]
        public void MacroArgumentCountIsChecked()
        {
            Assert.Contains("takes 2 argument(s) but 1 given", Error("#define ADD(a, b) a + b\nint x = ADD(1);\n"));
            Assert.Contains("is not closed", Error("#define F(a) a\nint x = F(1;\n"));
        }

        [Fact]
        public void HashMakesAStringOfTheArgumentAsItIsWritten()
        {
            var text = Run("#define STR(x) #x\nchar *a = STR(a  +  b); char *b = STR(  x  ); char *c = STR();\n");
            Assert.Equal("\nchar *a = \"a + b\"; char *b = \"x\"; char *c = \"\";\n", text);
        }

        [Fact]
        public void HashEscapesStringsAndCharactersInTheArgument()
        {
            var text = Run("#define STR(x) #x\nchar *a = STR(\"hi\\n\" 'c' '\\'');\n");
            // "hi\n" 'c' '\''  becomes  "\"hi\\n\" 'c' '\\''"
            Assert.Equal("\nchar *a = \"\\\"hi\\\\n\\\" 'c' '\\\\''\";\n", text);
        }

        [Fact]
        public void HashDoesNotExpandTheArgumentButAMacroInBetweenDoes()
        {
            var text = Run("#define N 3\n#define STR(x) #x\n#define XSTR(x) STR(x)\nchar *a = STR(N); char *b = XSTR(N);\n");
            Assert.Equal("\n\n\nchar *a = \"N\"; char *b = \"3\";\n", text);
        }

        [Fact]
        public void HashIsAnOrdinaryTokenInAnObjectMacro()
        {
            Assert.Equal("\nint a = 1 # 2;\n", Run("#define H 1 # 2\nint a = H;\n"));
        }

        [Fact]
        public void DoubleHashPastesTwoTokensIntoOne()
        {
            var text = Run("#define CAT(a, b) a ## b\nint CAT(foo, 1) = CAT(1, 2); int x = CAT(+, +) CAT(<, <=) 1;\n");
            Assert.Equal("\nint foo1 = 12; int x = ++ <<= 1;\n", text);
        }

        [Fact]
        public void DoubleHashWithAnEmptyArgumentIsTheOtherSide()
        {
            // (a macro that is nothing takes the space before it too)
            Assert.Equal("\nint a = x; int b = x; int c =;\n", Run("#define CAT(a, b) a ## b\nint a = CAT(, x); int b = CAT(x, ); int c = CAT(, );\n"));
        }

        [Fact]
        public void DoubleHashPastesBeforeTheArgumentsAreExpanded()
        {
            var text = Run("#define V 7\n#define N1 5\n#define CAT(a, b) a ## b\n#define XCAT(a, b) CAT(a, b)\nint a = CAT(V, 1); int b = XCAT(V, 1); int c = CAT(N, 1);\n");
            Assert.Equal("\n\n\n\nint a = V1; int b = 71; int c = 5;\n", text);
        }

        [Fact]
        public void DoubleHashInAnObjectMacro()
        {
            Assert.Equal("\nint ab;\n", Run("#define X a ## b\nint X;\n"));
        }

        [Fact]
        public void DoubleHashPastesTheEndsOfArgumentsWithSeveralTokens()
        {
            // the last token of the left argument and the first of the right one are pasted: 2 and 1 make 21
            Assert.Equal("\nint a = x + 21 + y;\n", Run("#define J(a, b) a ## b\nint a = J(x + 2, 1 + y);\n"));
        }

        [Fact]
        public void VariableArgumentsAreAllTheArgumentsAfterTheNamedOnes()
        {
            var text = Run("#define LOG(fmt, ...) printf(fmt, __VA_ARGS__)\nLOG(\"x\", 1, 2);\nLOG(\"y\", (3, 4), 5);\n");
            Assert.Equal("\nprintf(\"x\", 1, 2);\nprintf(\"y\", (3, 4), 5);\n", text);
        }

        [Fact]
        public void VariableArgumentsCanBeTheOnlyParameterAndCanBeEmpty()
        {
            var text = Run("#define ALL(...) f(__VA_ARGS__)\nint a = ALL(1, 2, 3); int b = ALL(); int c = ALL(x);\n");
            Assert.Equal("\nint a = f(1, 2, 3); int b = f(); int c = f(x);\n", text);
        }

        [Fact]
        public void VariableArgumentsMayBeLeftOut()
        {
            Assert.Equal("\nint a = g();\n", Run("#define CALL(f, ...) f(__VA_ARGS__)\nint a = CALL(g);\n"));
        }

        [Fact]
        public void CommaBeforeVariableArgumentsGoesWhenThereAreNone()
        {
            var text = Run("#define LOG(fmt, ...) p(fmt, ## __VA_ARGS__)\nLOG(\"a\"); LOG(\"a\", 1); LOG(\"a\", 1, 2);\n");
            Assert.Equal("\np(\"a\"); p(\"a\", 1); p(\"a\", 1, 2);\n", text);
        }

        [Fact]
        public void HashOfVariableArgumentsKeepsTheCommas()
        {
            Assert.Equal("\nchar *s = \"a, b\";\n", Run("#define SV(...) #__VA_ARGS__\nchar *s = SV(a, b);\n"));
        }

        [Fact]
        public void VariableArgumentsGoThroughAnotherMacroAndAreExpanded()
        {
            var text = Run("#define N 2\n#define ALL(...) f(__VA_ARGS__)\n#define W(...) ALL(__VA_ARGS__)\nint a = W(N, 3);\n");
            Assert.Equal("\n\n\nint a = f(2, 3);\n", text);
        }

        [Fact]
        public void RedefiningWithOrWithoutVariableArgumentsIsDifferent()
        {
            var warnings = new List<string>();
            Run("#define F(a, ...) a\n#define F(a, ...) a\n#define F(a) a\n", warnings: warnings);
            Assert.Single(warnings);
        }

        [Fact]
        public void UndefRemovesAMacro()
        {
            Assert.Equal("\n\nint a = N;\n", Run("#define N 1\n#undef N\nint a = N;\n"));
        }

        [Fact]
        public void PredefinedLineAndFile()
        {
            Assert.Equal("int a = 1; char *f = \"main.c\";\nint b = 2;\n", Run("int a = __LINE__; char *f = __FILE__;\nint b = __LINE__;\n"));
        }

        [Fact]
        public void DefinesGivenByTheCaller()
        {
            var text = Run("int a = N; int b = SQ(3);\n", defines: new Dictionary<string, string> { ["N"] = "7", ["SQ(x)"] = "((x) * (x))" });
            Assert.Equal("int a = 7; int b = ((3) * (3));\n", text);
        }

        [Fact]
        public void ConditionalsKeepTheLineCount()
        {
            var text = Run("#define A 1\n#if A\nint yes;\n#else\nint no;\n#endif\nint after;\n");
            Assert.Equal("\n\nint yes;\n\n\n\nint after;\n", text);
        }

        [Theory]
        [InlineData("#if 1 + 1 == 2\nint a;\n#endif\n", true)]
        [InlineData("#if 0\nint a;\n#endif\n", false)]
        [InlineData("#if (3 << 2) == 12 && !(5 % 3 == 2)\nint a;\n#endif\n", false)]
        [InlineData("#if 0x10 == 16 && 010 == 8 && 'a' == 97\nint a;\n#endif\n", true)]
        [InlineData("#if UNKNOWN\nint a;\n#endif\n", false)]
        [InlineData("#if UNKNOWN == 0 && -1 < 0 && ~0 == -1\nint a;\n#endif\n", true)]
        [InlineData("#if 1 ? 0 : 1\nint a;\n#endif\n", false)]
        [InlineData("#if 0 && (1 / 0)\nint a;\n#endif\n", false)]
        [InlineData("#if 1 || (1 / 0)\nint a;\n#endif\n", true)]
        [InlineData("#define X 3\n#if X > 2 && defined(X) && defined X && !defined(Y)\nint a;\n#endif\n", true)]
        [InlineData("#ifdef X\nint a;\n#endif\n", false)]
        [InlineData("#ifndef X\nint a;\n#endif\n", true)]
        public void IfExpressions(string source, bool included)
        {
            Assert.Equal(included, Run(source).Contains("int a;"));
        }

        [Fact]
        public void ElifAndElseAreChosenOnce()
        {
            var source = "#define V 2\n#if V == 1\nint one;\n#elif V == 2\nint two;\n#elif V == 2\nint two_again;\n#else\nint other;\n#endif\n";
            var text = Run(source);
            Assert.Contains("int two;", text);
            Assert.DoesNotContain("one", text);
            Assert.DoesNotContain("two_again", text);
            Assert.DoesNotContain("other", text);
        }

        [Fact]
        public void NestedConditionalsInASkippedPartAreNotEvaluated()
        {
            var text = Run("#if 0\n#if 1 / 0\n#error no\n#else\nint x;\n#endif\n#define Z 1\n#else\nint y;\n#endif\nint z = Z;\n");
            Assert.Contains("int y;", text);
            Assert.DoesNotContain("int x;", text);
            Assert.Contains("int z = Z;", text);
        }

        [Fact]
        public void DirectivesInASkippedPartAreIgnored()
        {
            Run("#if 0\n#include \"nowhere.h\"\n#bogus\n#error x\n#define\n#endif\n");
        }

        [Fact]
        public void ErrorDirectiveStopsWithItsMessage()
        {
            Assert.StartsWith("main.c(2, 1): #error stop here", Error("int a;\n#error stop here\n"));
        }

        [Fact]
        public void WarningDirectiveGoesToTheCallback()
        {
            var warnings = new List<string>();
            Run("#warning careful\n", warnings: warnings);
            Assert.Single(warnings);
            Assert.Contains("#warning careful", warnings[0]);
        }

        [Fact]
        public void RedefinitionWarnsOnlyWhenDifferent()
        {
            var warnings = new List<string>();
            Run("#define A 1\n#define A 1\n#define A 2\n", warnings: warnings);
            Assert.Single(warnings);
            Assert.Contains("'A' is redefined", warnings[0]);
        }

        [Theory]
        [InlineData("#if 1\nint a;\n", "#endif is missing")]
        [InlineData("#endif\n", "#endif without #if")]
        [InlineData("#else\n", "#else without #if")]
        [InlineData("#elif 1\n", "#elif without #if")]
        [InlineData("#if 1\n#else\n#else\n#endif\n", "#else after #else")]
        [InlineData("#if 1\n#else\n#elif 1\n#endif\n", "#elif after #else")]
        [InlineData("#if\n#endif\n", "expects an expression")]
        [InlineData("#if 1 +\n#endif\n", "The expression is not complete")]
        [InlineData("#if 1 / 0\n#endif\n", "Division by zero")]
        [InlineData("#if (1\n#endif\n", "')' is expected")]
        [InlineData("#define\n", "#define expects a name")]
        [InlineData("#define 3 x\n", "#define expects a name")]
        [InlineData("#define defined 1\n", "cannot be defined")]
        [InlineData("#define F(a, a) a\n", "is repeated")]
        [InlineData("#define F(a, ..., b) a\n", "'...' must be the last parameter")]
        [InlineData("#define F(__VA_ARGS__) a\n", "A parameter name is expected")]
        [InlineData("#define V __VA_ARGS__\n", "__VA_ARGS__ can be used only")]
        [InlineData("#define F(a) __VA_ARGS__\n", "__VA_ARGS__ can be used only")]
        [InlineData("#define S(a) #b\n", "'#' is not followed by a macro parameter")]
        [InlineData("#define S(a) #\n", "'#' is not followed by a macro parameter")]
        [InlineData("#define C ## a\n", "either end")]
        [InlineData("#define C a ##\n", "either end")]
        [InlineData("#define P(a, b) a ## b\nint x = P(+, -);\n", "does not make a token")]
        [InlineData("#define P(a, b) a ## b\nint x = P(/, /);\n", "does not make a token")]
        [InlineData("#define F(a, b, ...) a\nint x = F(1);\n", "takes at least 2 argument(s) but 1 given")]
        [InlineData("#ifdef\n#endif\n", "expects one name")]
        [InlineData("#bogus\n", "Unknown preprocessing directive #bogus")]
        [InlineData("/* never closed\n", "Unterminated comment")]
        public void Errors(string source, string expected)
        {
            Assert.Contains(expected, Error(source));
        }

        [Fact]
        public void IncludeBringsTheFileBetweenLineMarkers()
        {
            var text = Run(
                "int a;\n#include \"x.h\"\nint b;\n",
                new Dictionary<string, string> { ["x.h"] = "int from_header;\n" }
            );
            Assert.Equal("int a;\n# 1 \"x.h\" 1\nint from_header;\n# 3 \"main.c\" 2\nint b;\n", text);
        }

        [Fact]
        public void IncludeSeesTheMacrosAndGivesItsOwn()
        {
            var text = Run(
                "#define BEFORE 1\n#include \"x.h\"\nint a = AFTER + FROM;\n",
                new Dictionary<string, string> { ["x.h"] = "#define AFTER 2\nint h = BEFORE;\n" }
            );
            Assert.Contains("int h = 1;", text);
            Assert.Contains("int a = 2 + FROM;", text);
        }

        [Fact]
        public void IncludeNameCanBeAMacro()
        {
            var text = Run("#define HEADER \"x.h\"\n#include HEADER\n", new Dictionary<string, string> { ["x.h"] = "int h;\n" });
            Assert.Contains("int h;", text);
        }

        [Fact]
        public void QuotedIncludeIsNextToTheIncludingFileThenInTheDirectories()
        {
            var files = new Dictionary<string, string>
            {
                ["dir/a.h"] = "#include \"b.h\"\n",
                ["dir/b.h"] = "int next_to;\n",
                ["b.h"] = "int global_b;\n",
                ["c.h"] = "int plain;\n",
            };
            var text = Run("#include \"dir/a.h\"\n#include \"c.h\"\n", files);
            Assert.Contains("int next_to;", text);
            Assert.DoesNotContain("global_b", text);
            Assert.Contains("int plain;", text);
        }

        [Fact]
        public void AngledIncludeSkipsTheIncludingFileDirectory()
        {
            var files = new Dictionary<string, string>
            {
                ["dir/a.h"] = "#include <b.h>\n",
                ["dir/b.h"] = "int next_to;\n",
                ["b.h"] = "int global_b;\n",
            };
            var text = Run("#include \"dir/a.h\"\n", files);
            Assert.Contains("int global_b;", text);
            Assert.DoesNotContain("next_to", text);
        }

        [Fact]
        public void PragmaOnceIncludesAFileOnce()
        {
            var files = new Dictionary<string, string> { ["once.h"] = "#pragma once\nint once;\n" };
            var text = Run("#include \"once.h\"\n#include \"once.h\"\n", files);
            Assert.Equal(1, text.Split("int once;").Length - 1);
        }

        [Fact]
        public void IncludeGuardsWork()
        {
            var files = new Dictionary<string, string> { ["g.h"] = "#ifndef G_H\n#define G_H\nint guarded;\n#endif\n" };
            var text = Run("#include \"g.h\"\n#include \"g.h\"\n", files);
            Assert.Equal(1, text.Split("int guarded;").Length - 1);
        }

        [Fact]
        public void ErrorsInAnIncludedFileNameThatFile()
        {
            var files = new Dictionary<string, string> { ["x.h"] = "\n#error in header\n" };
            Assert.StartsWith("x.h(2, 1): #error in header", Error("#include \"x.h\"\n", files));
        }

        [Fact]
        public void IncludeErrors()
        {
            Assert.Contains("Cannot find the include file 'nope.h'", Error("#include \"nope.h\"\n"));
            Assert.Contains("expects \"file\" or <file>", Error("#include nope\n"));
            Assert.Contains("no include provider", Assert.ThrowsAny<Exception>(() => new CPreprocessor().Process("#include \"x.h\"\n", "main.c")).Message);
        }

        [Fact]
        public void RecursiveIncludeStopsAtTheDepthLimit()
        {
            var files = new Dictionary<string, string> { ["r.h"] = "#include \"r.h\"\n" };
            Assert.Contains("nested more than", Error("#include \"r.h\"\n", files));
        }

        [Fact]
        public void PragmaAndLineAreLeftForTheCompiler()
        {
            var text = Run("#pragma defsyscall trap_puti 0 0\n#line 40 \"orig.c\"\nint a;\n");
            Assert.Equal("#pragma defsyscall trap_puti 0 0\n#line 40 \"orig.c\"\nint a;\n", text);
        }

        [Fact]
        public void LineMarkersOfAnEarlierPreprocessorAreLeftAsTheyAre()
        {
            var text = Run("# 1 \"orig.c\"\nint a;\n");
            Assert.Equal("# 1 \"orig.c\"\nint a;\n", text);
        }

        [Fact]
        public void BackslashJoinsLinesOfADirectiveAndKeepsTheLineCount()
        {
            Assert.Equal("\n\nint a = 1 + 2;\n", Run("#define SUM \\\n  1 + 2\nint a = SUM;\n"));
        }

        [Fact]
        public void ColumnsOfTextWithoutMacrosAreKept()
        {
            Assert.Equal("  int   a = 1;\n", Run("  int   a = 1;\n"));
        }

        [Fact]
        public void TokensFromMacrosDoNotGlue()
        {
            Assert.Equal("\n\nint x = + + 1; int y = a b;\n", Run("#define P +\n#define J a\nint x = P+ 1; int y = J b;\n"));
        }
    }
}
