using OpenKh.Kh2Bdx.Utils;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using YamlDotNet.Serialization;

namespace OpenKh.Tests.kh2
{
    public class BdxCLogicalTests
    {
        private static readonly string _ymlFile = "kh2/res/bdx-c-regressiontests.yml";
        private static readonly IDeserializer _deser = new DeserializerBuilder().Build();
        private static readonly ISerializer _ser = new SerializerBuilder().Build();

        [Theory]
        [InlineData("max", "int max(int a, int b) { if (a < b) { return b; } else { return a; } }")]
        [InlineData("min", "int min(int a, int b) { if (a > b) { return b; } else { return a; } }")]
        [InlineData("fmax", "int fmax(float a, float b) { if (a < b) { return b; } else { return a; } }")]
        [InlineData("add3", "int add3(int a, int b, int c) { return a + b + c; }")]
        [InlineData("addsub", "int addsub(int a, int b, int c) { return a + b - c; }")]
        [InlineData("fmuldiv", "float muldiv(float a, float b, float c) { return a * b / c; }")]
        [InlineData("mod", "float mod(float a, float b) { return a % b; }")]
        [InlineData("brac", "int brac() { return (1 + 2) * 3; }")]
        [InlineData("shift", "int shift() { return (1 << 1) >> 2; }")]
        [InlineData("uminus", "int uminus() { return -1; }")]
        [InlineData("lnot", "int lnot() { return !1; }")]
        [InlineData("voidpp", "void *voidpp(void **p) { return *p; }")]
        [InlineData("voidp", "void one(void *p) { }")]
        //[InlineData("e_voidp", "void one(void *p) { p * 2 / 2; }")]
        [InlineData("int_rp", "int *one(int a) { return &a; }")]
        [InlineData("bnot", "int bnot() { return ~1; }")]
        [InlineData("andorxor", "int andorxor() { return 1 & 2 | 3 ^ 4; }")]
        [InlineData("assign", "void assign(int a) { a = 1; }")]
        [InlineData("declassign", "void declassign(int a) { int b = 1; }")]
        [InlineData("decl", "void decl(int a) { int b; }")]
        [InlineData("camma", "void camma() { 1, 2, 3; }")]
        [InlineData("lcamma", "void lcamma(int a) { (1, a) = 2; }")]
        [InlineData("comparators", "void comparators() { 1 < 2; 1 <= 2; 1 == 2; 1 != 2; 1 > 2; 1 >= 2; }")]
        [InlineData("assigns", "void assigns(int a) { a += 1; a -= 1; a /= 1; a *= 1; a %= 1; a &= 1; a |= 1; a ^= 1;a <<= 1; a >>= 1; }")]
        [InlineData("cast", "void cast(int a) { (float)a; }")]
        [InlineData("cast2", "void cast(int a) { (unsigned char)a; }")]
        [InlineData("globalint", "int a=0;")]
        [InlineData("staticint", "static int a=0;")]
        [InlineData("incdec", "void incdec(int a) { a++; ++a; a--; --a; }")]
        [InlineData("multivars", "int a, b, c;")]
        [InlineData("struct", "struct S { int a; int b; int c; } s;")]
        [InlineData("multivars2", "int a, b = 2, c;")]
        [InlineData("array", "int a[1], b[2], b[3];")]
        public void CompileOnly(string outPrefix, string code)
        {
            var result = new BdxCEncoder().EncodeC(code, $"{outPrefix}.c");
            File.WriteAllText($"{outPrefix}-compiled.bdscript", result.Bdscript);
        }

        [Fact]
        public void LocalCallUsesFrameSizeAndReverseArgOrder()
        {
            var result = new BdxCEncoder().EncodeC(
                "int sub(int a, int b) { return a - b; }\n" +
                "int caller(int x) { int y = 1; return sub(x, y); }\n",
                "call.c"
            );

            // caller: x, y = 2 locals => frame = 2 + 2 = 4
            Assert.Contains("push.d.sp 4 ; y\n push.d.sp 0 ; x\n jal 4, sub", result.Bdscript.Replace("\r\n", "\n"));
        }

        [Fact]
        public void ForwardCallAndVoidStatementDoNotDrop()
        {
            var result = new BdxCEncoder().EncodeC(
                "void a() { b(1); }\nvoid b(int v) { }\n",
                "fwd.c"
            ).Bdscript.Replace("\r\n", "\n");

            Assert.Contains("push 1\n jal 2, b\n", result);
            Assert.DoesNotContain("drop", result);
        }

        [Fact]
        public void NonVoidCallStatementDrops()
        {
            var result = new BdxCEncoder().EncodeC(
                "int f() { return 1; }\nvoid g() { f(); }\n",
                "drop.c"
            ).Bdscript.Replace("\r\n", "\n");

            Assert.Contains("jal 2, f\n drop", result);
        }

        [Fact]
        public void SyscallByTrapName()
        {
            var result = new BdxCEncoder().EncodeC(
                "void f() { trap_puti(1, 2); int r = trap_random_get(5); }",
                "sys.c"
            ).Bdscript.Replace("\r\n", "\n");

            Assert.Contains("push 2\n push 1\n syscall 0, 0 ; trap_puti\n", result);
            Assert.Contains("syscall 0, 16 ; trap_random_get\n", result);
        }

        private static string Compile(string code) =>
            new BdxCEncoder().EncodeC(code, "t.c").Bdscript.Replace("\r\n", "\n");

        [Fact]
        public void WhileLoopWithBreakAndContinue()
        {
            var s = Compile("void f(int a) { while (a) { if (a == 1) { break; } if (a == 2) { continue; } a--; } }");
            Assert.Contains("__f1loop:\n__f1continue:", s);
            Assert.Contains(" jz __f1endloop", s);
            Assert.Contains(" b __f1endloop", s);
            Assert.Contains(" b __f1continue", s);
            Assert.Contains(" b __f1loop\n__f1endloop:", s);
        }

        [Fact]
        public void DoWhileLoop()
        {
            var s = Compile("void f(int a) { do { a--; } while (a); }");
            Assert.Contains("loop:", s);
            Assert.Contains("continue:", s);
            Assert.Contains(" jnz __f1loop\n__f1endloop:", s);
        }

        [Fact]
        public void ForLoopWithDeclaration()
        {
            var s = Compile("int f() { int sum = 0; for (int i = 0; i < 10; i++) { sum += i; } return sum; }");
            Assert.Contains("loop:", s);
            Assert.Contains("continue:", s);
            Assert.Contains(" b __f1loop\n__f1endloop:", s);
        }

        [Fact]
        public void ForLoopWithCallsAndEmptyParts()
        {
            Compile("void g() { } void f() { for (;;) { break; } for (g(); ; g()) { break; } }");
        }

        [Theory]
        [InlineData("void f() { break; }")]
        [InlineData("void f() { continue; }")]
        public void BreakOutsideLoopFails(string code)
        {
            Assert.ThrowsAny<Exception>(() => Compile(code));
        }

        [Fact]
        public void StringLiteralBecomesBdData()
        {
            var s = Compile("void f() { char *p = \"Hi\\n\"; char *q = \"Hi\\n\"; trap_puts(\"x\"); }");
            Assert.Contains(" push.bd __str1\n", s);
            Assert.Contains("__str1:\n db 'Hi',10,0\n", s);
            Assert.Contains("__str2:\n db 'x',0", s);
            Assert.DoesNotContain("__str3", s);
        }

        [Fact]
        public void StringLiteralOddLengthIsPadded()
        {
            var s = Compile("void f() { char *p = \"abc\"; char *q = \"it's\"; }");
            Assert.Contains(" db 'abc',0\n", s);
            Assert.Contains(" db 'it',39,'s',0,0", s);
        }

        [Fact]
        public void StructGlobalMembersUseLabels()
        {
            var s = Compile("struct S { int a; float b; } g; void f() { g.a = 1; g.b = g.b + 2.0; }");
            Assert.Contains("section .bss\ng:\ng.a:\n resb 4\ng.b:\n resb 4", s);
            Assert.Contains(" pop.wp g.a\n", s);
            Assert.Contains(" push.d.wp g.b\n", s);
        }

        [Fact]
        public void StructLocalMembersUseSlots()
        {
            var s = Compile("struct S { int a; int b; int c; }; int f() { struct S s; s.b = 5; return s.b + s.c; }");
            Assert.Contains(" pop.sp 4 ; s.b\n", s);
            Assert.Contains(" push.d.sp 8 ; s.c\n", s);
        }

        [Fact]
        public void StructPointerMemberAccess()
        {
            var s = Compile("struct S { int a; int b; }; int f(struct S *p) { return p->b + (*p).a; }");
            Assert.Contains(" push.d.sp 0 ; p\n push.d.pop 4\n", s);
            Assert.Contains(" push.d.pop 0\n", s);
        }

        [Fact]
        public void AddressOfStructPassedToFunction()
        {
            var s = Compile("struct S { int a; int b; }; void g(struct S *p) { } void f() { struct S s; g(&s); }");
            Assert.Contains(" push.sp 0 ; &s\n jal 4, g", s);
        }

        [Fact]
        public void NestedStruct()
        {
            var s = Compile("struct I { int x; int y; }; struct O { int h; struct I i; }; void f() { struct O o; o.i.y = 3; }");
            Assert.Contains(" pop.sp 8 ; o.i.y\n", s);
        }

        [Fact]
        public void SelfReferentialStructLinkedList()
        {
            var s = Compile(
                "struct V { int x; struct V *next; };" +
                "int sum(struct V *p) { int t = 0; while (p) { t += p->x; p = p->next; } return t; }"
            );
            Assert.Contains(" push.d.pop 0\n", s);
            Assert.Contains(" push.d.pop 4\n", s);
        }

        [Fact]
        public void StructTagWithoutKeyword()
        {
            Compile("struct S { int a; }; S g; void f() { g.a = 1; }");
        }

        [Theory]
        [InlineData("struct S { int a; }; void f() { struct S s; s.zzz = 1; }")]
        [InlineData("struct S { int a; }; void f(struct S s) { }")]
        [InlineData("struct S { int a; }; struct S f() { }")]
        [InlineData("void f() { struct Nope n; }")]
        [InlineData("void f(int x) { x.a = 1; }")]
        [InlineData("struct S { int a; }; void f(struct S *p) { p.a = 1; }")]
        public void BadStructUse(string code)
        {
            Assert.ThrowsAny<Exception>(() => Compile(code));
        }

        [Fact]
        public void CharLiteral()
        {
            Assert.Contains(" push 65\n", Compile("int f() { return 'A'; }"));
        }

        [Theory]
        [InlineData("void f() { unknown(); }")]
        [InlineData("int g(int a) { return a; } void f() { g(); }")]
        [InlineData("void f() { trap_puti(1); }")]
        public void BadCalls(string code)
        {
            Assert.ThrowsAny<Exception>(() => new BdxCEncoder().EncodeC(code, "bad.c"));
        }

        [Theory]
        [MemberData(nameof(GetRegressionTests))]
        public void RegressionTests(string name, string c, string expected)
        {
            var encoder = new BdxCEncoder();

            var result = encoder.EncodeC(c, $"{name}.c");

            Assert.Equal(
                expected: expected,
                actual: result.Bdscript,
                ignoreLineEndingDifferences: true
            );
        }

        private class BdxCLogicalYml
        {
            [YamlMember(Alias = "patterns")] public Pattern[] Patterns { get; set; } = null!;
        }

        public class Pattern
        {
            [YamlMember(Alias = "name")] public string Name { get; set; } = null!;
            [YamlMember(Alias = "c")] public string C { get; set; } = null!;
            [YamlMember(Alias = "expected")] public string? Expected { get; set; }
        }

        public static IEnumerable<object[]> GetRegressionTests()
        {
            var model = _deser.Deserialize<BdxCLogicalYml>(File.ReadAllText(_ymlFile));

            foreach (var pattern in model.Patterns)
            {
                yield return new object[] { pattern.Name, pattern.C, pattern.Expected, };
            }
        }

        [Fact(Skip = "Private usage")]
        public void GenerateRegressionTestsExpected()
        {
            var model = _deser.Deserialize<BdxCLogicalYml>(File.ReadAllText(_ymlFile));

            var encoder = new BdxCEncoder();

            foreach (var pattern in model.Patterns)
            {
                var result = encoder.EncodeC(pattern.C, $"{pattern.Name}.c");

                pattern.Expected = result.Bdscript;
            }

            File.WriteAllText(
                _ymlFile,
                _ser.Serialize(model)
            );
        }
    }
}
