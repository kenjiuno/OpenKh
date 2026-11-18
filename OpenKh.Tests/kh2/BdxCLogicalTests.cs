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

            Assert.Contains("push 1\n jal 4, b\n", result);
            Assert.DoesNotContain("drop", result);
        }

        [Fact]
        public void NonVoidCallStatementDrops()
        {
            var result = new BdxCEncoder().EncodeC(
                "int f() { return 1; }\nvoid g() { f(); }\n",
                "drop.c"
            ).Bdscript.Replace("\r\n", "\n");

            Assert.Contains("jal 4, f\n drop", result);
        }

        [Fact]
        public void SyscallByTrapName()
        {
            var result = new BdxCEncoder().EncodeC(
                "void f() { trap_puti(1, 2); int r = trap_random_get(5); }",
                "sys.c"
            ).Bdscript.Replace("\r\n", "\n");

            // syscall args are pushed first-to-last (verified with bdEmu)
            Assert.Contains("push 1\n push 2\n syscall 0, 0 ; trap_puti\n", result);
            Assert.Contains("syscall 0, 16 ; trap_random_get\n", result);
        }

        [Fact]
        public void JalArgsArePushedLastToFirst()
        {
            var s = Compile("int sub(int a, int b) { return a - b; } int f() { return sub(10, 3); }");
            Assert.Contains(" push 3\n push 10\n jal ", s);
        }

        [Fact]
        public void RelationalOperatorsSubtractBeforeSignTest()
        {
            var s = Compile("int f(int a, int b) { return a < b; }");
            Assert.Contains(" push.d.sp 4 ; b\n sub\n sltz\n", s);
        }

        [Fact]
        public void ZeroAndVoidPointerConvertToPointer()
        {
            Compile("struct V { struct V *n; }; void f() { struct V a; void *v; a.n = 0; v = &a; a.n = v; }");
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
        [InlineData("void f() { struct Nope n; }")]
        [InlineData("void f(int x) { x.a = 1; }")]
        [InlineData("struct S { int a; }; void f(struct S *p) { p.a = 1; }")]
        public void BadStructUse(string code)
        {
            Assert.ThrowsAny<Exception>(() => Compile(code));
        }

        [Fact]
        public void PointerStoreIsMemcpyOfTheValueToThePointer()
        {
            var s = Compile("struct V { int x; int y; }; void f(struct V *p) { p->y = 3; }");
            // the value is dup'ed; pointer and value go to hidden slots to be pushed destination first
            Assert.Contains(
                " push.d.sp 0 ; p\n push 4\n add\n push 3\n memcpy 0\n",
                s
            );
            Assert.DoesNotContain("@@", s);
            Assert.DoesNotContain("pop.sp.d", s);
        }

        [Fact]
        public void BraceElisionFillsASubArrayFromTheFollowingElements()
        {
            var s = Compile("void f() { int m[2][2] = { 1, 2, 3, 4 }; }");
            Assert.Contains(" push 1\n pop.sp 0 ; m.0.0\n push 2\n pop.sp 4 ; m.0.1\n push 3\n pop.sp 8 ; m.1.0\n push 4\n pop.sp 12 ; m.1.1\n", s);
        }

        [Fact]
        public void BraceElisionZeroFillsTheRest()
        {
            var s = Compile("void f() { int m[2][2] = { 1, 2, 3 }; }");
            Assert.Contains(" push 3\n pop.sp 8 ; m.1.0\n push 0\n pop.sp 12 ; m.1.1\n", s);
        }

        [Fact]
        public void BraceElisionMixesWithBracedElements()
        {
            var s = Compile("void f() { int m[3][2] = { { 1 }, 5, 6, 7 }; }");
            // {1} is the first row (the rest zero), 5 and 6 the second, 7 starts the third
            Assert.Contains(" push 1\n pop.sp 0 ; m.0.0\n push 0\n pop.sp 4 ; m.0.1\n push 5\n pop.sp 8 ; m.1.0\n push 6\n pop.sp 12 ; m.1.1\n push 7\n pop.sp 16 ; m.2.0\n", s);
        }

        [Fact]
        public void BraceElisionOfStructMembersAndStructArrays()
        {
            var s = Compile("struct P { int x; int y; }; struct L { struct P a; struct P b; int t; }; void f() { struct L l = { 1, 2, 3, 4, 5 }; struct P ps[2] = { 1, 2, 3, 4 }; }");
            Assert.Contains(" push 3\n pop.sp 8 ; l.b.x\n push 4\n pop.sp 12 ; l.b.y\n push 5\n pop.sp 16 ; l.t\n", s);
            Assert.Contains(" push 3\n pop.sp 28 ; ps.1.x\n", s);
        }

        [Fact]
        public void ArrayLengthIsInferredFromElidedBraces()
        {
            // 5 values in rows of 3: two rows, so n is after 6 slots
            var s = Compile("void f() { int a[][3] = { 1, 2, 3, 4, 5 }; int n; n = 1; }");
            Assert.Contains(" pop.sp 24 ; n\n", s);
            // a struct value is one element
            var p = Compile("struct P { int x; int y; }; void f() { struct P q; struct P a[] = { q, q }; int n; n = 1; }");
            Assert.Contains(" pop.sp 24 ; n\n", p);
        }

        [Fact]
        public void BraceElisionInCharAndShortMembers()
        {
            var s = Compile("struct N { int id; char name[4]; short v[2]; }; void f() { struct N n = { 9, \"abc\", 5, -6 }; }");
            Assert.Contains(" push 9\n pop.sp 0 ; n.id\n push 6513249\n pop.sp 4 ; n.name.0\n push -393211\n pop.sp 8 ; n.v.0\n", s);
        }

        [Theory]
        [InlineData("void f() { int m[2][2] = { 1, 2, 3, 4, 5 }; }")]
        [InlineData("struct P { int x; int y; }; void f() { struct P p = { 1, 2, 3 }; }")]
        [InlineData("struct P { int x; int y; }; void f() { struct P a[1] = { 1, 2, 3 }; }")]
        public void TooManyInitializersAfterElision(string code)
        {
            Assert.ThrowsAny<Exception>(() => Compile(code));
        }

        [Fact]
        public void ShortStoreCopiesTwoBytes()
        {
            var s = Compile("void f(short *c) { *c = 1; }");
            Assert.Contains(" push.d.sp 0 ; c\n push 1\n push 65535\n and\n pop.sp 4\n push.sp 4\n memcpy 2\n", s);
        }

        [Fact]
        public void ShortLoadKeepsTwoBytesAndExtendsTheSign()
        {
            var s = Compile("int f(short *c) { return *c; }");
            Assert.Contains(" push.d.pop 0 ; deref short\n push 65535\n and\n push 32768\n xor\n push 32768\n sub\n", s);
            var u = Compile("int f(unsigned short *c) { return *c; }");
            Assert.Contains(" push.d.pop 0 ; deref short\n push 65535\n and\n", u);
            Assert.DoesNotContain("xor", u);
        }

        [Fact]
        public void ShortArrayPacksTwoElementsInASlot()
        {
            // s: 2 slots (3 shorts), then n
            var s = Compile("void f() { short s[3]; int n; n = 1; }");
            Assert.Contains(" pop.sp 8 ; n\n", s);
            var p = Compile("short *f(short *c) { return c + 3; }");
            Assert.Contains(" push 3\n push 2\n mul\n add\n", p);
        }

        [Fact]
        public void BracedShortInitializerPacksLittleEndianPairs()
        {
            var s = Compile("void f() { short h[3] = { 1, -2, 7 }; }");
            // 0xFFFE0001, 0x0007
            Assert.Contains(" push -131071\n pop.sp 0 ; h.0\n push 7\n pop.sp 4 ; h.1\n", s);
        }

        [Fact]
        public void SizeofShortArrayIsTwoBytesPerElement()
        {
            var s = Compile("int f() { short h[5]; return sizeof(h); }");
            Assert.Contains(" push 10\n", s);
        }

        [Fact]
        public void GotoJumpsToAFunctionLabel()
        {
            var s = Compile("int f(int a) { if (a) { goto out; } a = 1; out: return a; }");
            Assert.Contains(" b __f_L_out\n", s);
            Assert.Contains("__f_L_out:\n", s);
        }

        [Fact]
        public void SameLabelNameInTwoFunctionsIsFine()
        {
            Compile("void f() { goto x; x: ; } void g() { goto x; x: ; }");
        }

        [Theory]
        [InlineData("void f() { goto nowhere; }")]
        [InlineData("void f() { x: ; x: ; }")]
        [InlineData("void f() { { x: ; } } void g() { goto x; }")]
        public void BadGoto(string code)
        {
            Assert.ThrowsAny<Exception>(() => Compile(code));
        }

        [Theory]
        [InlineData("int", 4)]
        [InlineData("char", 1)]
        [InlineData("short", 2)]
        [InlineData("float", 4)]
        [InlineData("char *", 4)]
        [InlineData("struct P", 8)]
        [InlineData("struct P *", 4)]
        public void SizeofTypeIsAConstant(string type, int expected)
        {
            var s = Compile($"struct P {{ int x; int y; }}; int f() {{ return sizeof({type}); }}");
            Assert.Contains($" push {expected}\n", s);
        }

        [Fact]
        public void SizeofVariableAndArrays()
        {
            var s = Compile("int f() { int a[5]; char c[13]; int m[2][3]; return sizeof(a) + sizeof c + sizeof(m) + sizeof(m[0]); }");
            // 20, 13 (a char array is its length), 24, 12
            Assert.Contains(" push 20\n", s);
            Assert.Contains(" push 13\n", s);
            Assert.Contains(" push 24\n", s);
            Assert.Contains(" push 12\n", s);
        }

        [Fact]
        public void SizeofDoesNotEvaluateItsOperand()
        {
            var s = Compile("int f(int x) { return sizeof(x++); }");
            // only the parameter pop and the constant: no increment code
            Assert.Contains("f: ; func f\n pop.sp 0\n push 4\n ret\n", s);
        }

        [Fact]
        public void SizeofIsAConstantExpression()
        {
            var s = Compile("struct P { int x; int y; }; int g = sizeof(struct P); int a[sizeof(int) * 2]; void f() { a[7] = 1; }");
            Assert.Contains("_initc:\n push 8\n pop.wp g\n ret\n", s);
            // a has 8 elements
            Assert.ThrowsAny<Exception>(() => Compile("int a[sizeof(int) * 2]; void f() { a[8] = 1; }"));
        }

        [Theory]
        [InlineData("int f() { return sizeof(void); }")]
        [InlineData("struct S; int f() { return sizeof(struct S); }")]
        [InlineData("int f(int a[]) { int b[] ; return sizeof(b); }")]
        public void BadSizeof(string code)
        {
            Assert.ThrowsAny<Exception>(() => Compile(code));
        }

        [Fact]
        public void LogicalAndSkipsTheRightSideWhenTheLeftIsZero()
        {
            var s = Compile("int f(int a, int b) { return a && b; }");
            Assert.Contains(" push.d.sp 0 ; a\n dup\n beqz __f1endlogic\n push.d.sp 4 ; b\n land\n__f1endlogic:\n", s);
        }

        [Fact]
        public void LogicalOrNormalizesALeftSideThatIsNotAComparison()
        {
            var s = Compile("int f(int a, int b) { return a || b; }");
            Assert.Contains(" push.d.sp 0 ; a\n snez\n dup\n bnez __f1endlogic\n push.d.sp 4 ; b\n lor\n__f1endlogic:\n", s);
            // the result of a comparison is already 0 or 1
            var c = Compile("int f(int a, int b) { return a < b || b; }");
            Assert.Contains(" sltz\n dup\n bnez ", c);
        }

        [Fact]
        public void LogicalChainsFoldToTheLeft()
        {
            var s = Compile("int f(int a, int b, int c) { return a && b && c; }");
            Assert.Contains("__f1endlogic:\n dup\n beqz __f2endlogic\n push.d.sp 8 ; c\n land\n__f2endlogic:\n", s);
        }

        [Fact]
        public void ConditionalExpressionBranchesAroundTheUnusedSide()
        {
            var s = Compile("int f(int c, int a, int b) { return c ? a : b; }");
            Assert.Contains(
                " push.d.sp 0 ; c\n beqz __f1else\n push.d.sp 4 ; a\n b __f1endsel\n__f1else:\n push.d.sp 8 ; b\n__f1endsel:\n",
                s
            );
        }

        [Fact]
        public void ConditionalExpressionConvertsToTheCommonType()
        {
            var s = Compile("float f(int c, int a, float b) { return c ? a : b; }");
            Assert.Contains(" push.d.sp 4 ; a\n cvt.w.s\n b ", s);
        }

        [Theory]
        [InlineData("struct S { int a; }; int f(struct S s) { return s && 1; }")]
        [InlineData("struct S { int a; }; void f(int c, struct S a, struct S b) { c ? a : b; }")]
        [InlineData("void g() { } int f() { return g() || 1; }")]
        [InlineData("int a = 1 && 2; void f() { }")]
        public void BadLogicalOperands(string code)
        {
            Assert.ThrowsAny<Exception>(() => Compile(code));
        }

        [Fact]
        public void CompoundAssignmentThroughPointerEvaluatesTheAddressOnce()
        {
            var s = Compile("void f(int *q) { *q += 2; }");
            // address, dup, load, operand, op, store: as real data does
            Assert.Contains(" push.d.sp 0 ; q\n dup\n push.d.pop 0 ; deref\n push 2\n add\n memcpy 0\n", s);
        }

        [Fact]
        public void PostIncrementThroughPointerStatementIsAddressLoadAddStore()
        {
            var s = Compile("void f(int *q) { (*q)++; }");
            Assert.Contains(" push.d.sp 0 ; q\n dup\n push.d.pop 0 ; deref\n push 1\n add\n memcpy 0\n", s);
            Assert.DoesNotContain("drop", s);
        }

        [Fact]
        public void AssignmentThroughPointerKeepsTheValueWhenItIsUsed()
        {
            var s = Compile("int f(int *q) { return *q = 3; }");
            Assert.Contains(" push.d.sp 0 ; q\n push 3\n dup\n pop.sp 4\n memcpy 0\n push.d.sp 4\n", s);
        }

        [Fact]
        public void PreIncrementIsTheNewValueAndPostIncrementTheOldOne()
        {
            var pre = Compile("int f(int a) { return ++a; }");
            Assert.Contains(" push.d.sp 0 ; a\n push 1\n add\n dup\n pop.sp 0 ; a\n", pre);
            var post = Compile("int f(int a) { return a++; }");
            Assert.Contains(" push.d.sp 0 ; a\n dup\n push 1\n add\n pop.sp 0 ; a\n", post);
        }

        [Fact]
        public void PointerStoreDoesNotBorrowTheVariableAtSp0()
        {
            var s = Compile("void f() { int a; int *p = &a; *p = 5; }");
            Assert.Contains(" push.sp 0 ; &a\n", s);
            Assert.DoesNotContain(" pop.sp 0 ; pointer", s);
        }

        [Fact]
        public void CharStoreCopiesOneByteWithMemcpy1()
        {
            var s = Compile("void f(char *c) { *c = 1; }");
            Assert.Contains(
                " push.d.sp 0 ; c\n push 1\n push 255\n and\n pop.sp 4\n push.sp 4\n memcpy 1\n",
                s
            );
        }

        [Fact]
        public void CharLoadKeepsTheLowestByteAndExtendsTheSign()
        {
            var s = Compile("int f(char *c) { return *c; }");
            Assert.Contains(" push.d.pop 0 ; deref char\n push 255\n and\n push 128\n xor\n push 128\n sub\n", s);
            var u = Compile("int f(unsigned char *c) { return *c; }");
            Assert.Contains(" push.d.pop 0 ; deref char\n push 255\n and\n", u);
            Assert.DoesNotContain("xor", u);
        }

        [Fact]
        public void CharPointerMovesByOneByte()
        {
            var s = Compile("char *f(char *c) { return c + 3; }");
            Assert.Contains(" push 3\n push 1\n mul\n add\n", s);
            var p = Compile("int f(char *c) { return c[2]; }");
            Assert.Contains(" push 2\n push 1\n mul\n add\n push.d.pop 0 ; deref char\n", p);
        }

        [Fact]
        public void CharArrayPacksFourBytesInASlot()
        {
            // c: 2 slots (5 bytes), then n
            var s = Compile("void f() { char c[5]; int n; n = 1; }");
            Assert.Contains(" pop.sp 8 ; n\n", s);
        }

        [Fact]
        public void StringInitializerFillsWholeSlotsLittleEndian()
        {
            var s = Compile("void f() { char s[8] = \"abcde\"; }");
            // 'abcd' = 0x64636261, 'e' = 0x65, the rest is zero
            Assert.Contains(" push 1684234849\n pop.sp 0 ; s.0\n push 101\n pop.sp 4 ; s.1\n", s);
        }

        [Fact]
        public void CharArrayLengthComesFromTheStringWithItsNul()
        {
            // "xyz" + NUL = 4 bytes = 1 slot, so n is at 4
            var s = Compile("void f() { char t[] = \"xyz\"; int n; n = 1; }");
            Assert.Contains(" pop.sp 4 ; n\n", s);
            var w = Compile("void f() { char t[] = \"xyzw\"; int n; n = 1; }");
            Assert.Contains(" pop.sp 8 ; n\n", w);
        }

        [Fact]
        public void BracedCharInitializerPacksConstants()
        {
            var s = Compile("void f() { char b[4] = { 'a', 'b', 0, 1 }; }");
            // 0x01006261
            Assert.Contains(" push 16802401\n pop.sp 0 ; b.0\n", s);
        }

        [Fact]
        public void GlobalCharArrayHasALabelPerSlot()
        {
            var s = Compile("char g[6]; void f() { }");
            Assert.Contains("section .bss\ng:\ng.0:\n resb 4\ng.1:\n resb 4", s);
        }

        [Fact]
        public void GlobalStringInitializerGoesToInitc()
        {
            var s = Compile("char g[4] = \"hi\"; void f() { }");
            Assert.Contains("_initc:\n push 26984\n pop.wp g.0\n ret\n", s);
        }

        [Fact]
        public void StringsInCharArraysOfAnArray()
        {
            // char names[2][4]: a row is a slot
            var s = Compile("void f() { char n[2][4] = { \"ab\", \"cd\" }; }");
            Assert.Contains(" push 25185\n pop.sp 0 ; n.0.0\n push 25699\n pop.sp 4 ; n.1.0\n", s);
        }

        [Fact]
        public void CastToPointerKeepsThePointer()
        {
            var s = Compile("char *f(int a) { return (char *)(a + 1); }");
            Assert.DoesNotContain("push 255", s);
        }

        [Fact]
        public void CharIsPromotedToIntInArithmetic()
        {
            // 1000 must not be narrowed to a char (1000 & 255 = 232)
            var s = Compile("int f(char *c) { return *c * 1000; }");
            Assert.DoesNotContain("push 255\n and\n push.d", s);
            Assert.Contains(" push 1000\n mul\n", s);
            Assert.DoesNotContain(" push 1000\n push 255", s);
        }

        [Fact]
        public void CharArrayMemberIsCopiedWithTheStruct()
        {
            var s = Compile("struct N { int id; char name[6]; }; void f() { struct N a; struct N b; b = a; }");
            // id and the 2 slots of name
            // id and the 2 slots of name are one memcpy of 12 bytes
            Assert.Contains(" push.sp 0 ; &a\n memcpy.sp 12, 12 ; struct N\n", s);
        }
        [Fact]
        public void PointerStoreThroughIntPointer()
        {
            var s = Compile("void f(int *q) { *q = 1; *q += 2; }");
            Assert.Contains("memcpy 0", s);
        }

        [Theory]
        [InlineData(0, 4)]
        [InlineData(1, 4)]
        [InlineData(2, 4)]
        [InlineData(3, 8)]
        [InlineData(6, 8)]
        [InlineData(7, 12)]
        public void FrameSizeIsAlignedTo16Bytes(int numLocals, int expectedJalCount)
        {
            var locals = string.Concat(Enumerable.Range(0, numLocals).Select(i => $"int v{i}; "));
            var s = Compile($"void g() {{ }} void f() {{ {locals} g(); }}");
            Assert.Contains($"jal {expectedJalCount}, g", s);
        }

        [Fact]
        public void PointerStoreHiddenSlotsAreNotInTheFrameOfACall()
        {
            var s = Compile("void g() { } void f(int *q, int r) { *q = 1; g(); }");
            // the hidden slots are dead at the jal: q, r + 2 system DWORDs = 4
            Assert.Contains("jal 4, g", s);
        }

        [Fact]
        public void NoHiddenSlotWithoutPointerStore()
        {
            var s = Compile("void g() { } int f(int *q, int r) { g(); return *q; }");
            Assert.Contains("jal 4, g", s);
        }


        [Fact]
        public void GlobalStructInitializerGoesToInitc()
        {
            var s = Compile("struct P { int x; int y; }; struct P gp = { 3, 4 }; void f() { }");
            Assert.Contains("_initc:\n push 3\n pop.wp gp.x\n push 4\n pop.wp gp.y\n ret\n", s);
        }

        [Fact]
        public void PartialStructInitializerZeroFillsTheRest()
        {
            var s = Compile("struct P { int x; int y; int z; }; struct P g = { 9 }; void f() { }");
            Assert.Contains("_initc:\n push 9\n pop.wp g.x\n push 0\n pop.wp g.y\n push 0\n pop.wp g.z\n ret\n", s);

            var empty = Compile("struct P { int x; int y; }; void f() { struct P p = { }; }");
            Assert.Contains(" push 0\n pop.sp 0 ; p.x\n push 0\n pop.sp 4 ; p.y\n", empty);
        }

        [Fact]
        public void NestedStructInitializerAndConversions()
        {
            var s = Compile("struct P { int x; int y; }; struct L { struct P a; float f; int tail; }; void f() { struct L l = { { 1, 2 }, 3, }; }");
            Assert.Contains(" push 1\n pop.sp 0 ; l.a.x\n push 2\n pop.sp 4 ; l.a.y\n push 3\n cvt.w.s\n pop.sp 8 ; l.f\n push 0\n pop.sp 12 ; l.tail\n", s);
        }

        [Fact]
        public void ScalarBracedInitializer()
        {
            Assert.Contains("_initc:\n push 7\n pop.wp g\n ret\n", Compile("int g = { 7 }; void f() { }"));
        }

        [Theory]
        [InlineData("struct P { int x; }; struct P p = { 1, 2 };")]
        [InlineData("struct P { int x; }; struct P p = 1;")]
        [InlineData("struct P { int x; }; struct P p = { { 1 } , 2 };")]
        [InlineData("int a = { 1, 2 };")]
        public void BadBracedInitializers(string code)
        {
            Assert.ThrowsAny<Exception>(() => Compile(code + " void f() { }"));
        }

        [Fact]
        public void StructAssignmentCopiesMembers()
        {
            var s = Compile("struct P { int x; int y; }; void f() { struct P a; struct P b; b = a; }");
            // one memcpy for the whole struct, as real data does
            Assert.Contains(" push.sp 0 ; &a\n memcpy.sp 8, 8 ; struct P\n", s);
            // no value is left on tp, so there is no dup / drop
            Assert.DoesNotContain("dup", s);
            Assert.DoesNotContain("drop", s);
        }

        [Fact]
        public void StructInitializedByStructValue()
        {
            var s = Compile("struct P { int x; int y; }; void f() { struct P a; struct P b = a; }");
            Assert.Contains(" push.sp 0 ; &a\n memcpy.sp 8, 8 ; struct P\n", s);
        }

        [Fact]
        public void StructAssignmentThroughPointers()
        {
            var s = Compile("struct P { int x; }; void f(struct P *p, struct P *q) { *p = *q; }");
            // destination pointer, source pointer, then one memcpy of the struct
            Assert.Contains(" push.d.sp 0 ; p\n push.d.sp 4 ; q\n memcpy 4 ; struct P\n", s);
            Assert.DoesNotContain("push.d.pop", s);
        }

        [Fact]
        public void NestedStructAssignment()
        {
            var s = Compile("struct P { int x; int y; }; struct L { struct P a; int t; }; void f() { struct L l; struct P p; p = l.a; l.a = p; }");
            Assert.Contains(" memcpy.sp 8, 12 ; struct P\n", s);
            Assert.Contains(" push.sp 12 ; &p\n memcpy.sp 8, 0 ; struct P\n", s);
        }

        [Theory]
        [InlineData("struct A { int x; }; struct B { int x; }; void f() { struct A a; struct B b; a = b; }")]
        [InlineData("struct A { int x; }; void f() { struct A a; a = 1; }")]
        [InlineData("struct A { int x; }; void f() { struct A a; struct A b; a += b; }")]
        [InlineData("struct A { int x; }; void f() { struct A a; struct A b; struct A c; a = b = c; }")]
        public void BadStructAssignments(string code)
        {
            Assert.ThrowsAny<Exception>(() => Compile(code));
        }

        [Fact]
        public void SwitchDispatchesWithSubAndBeqz()
        {
            var s = Compile("int f(int n) { switch (n) { case 1: return 10; case 2: return 20; default: return 0; } }");
            // value saved once, compared to each case in order, then default
            Assert.Contains(" pop.sp 4 ; switch value\n", s);
            Assert.Contains(" push.d.sp 4 ; switch value\n push 1\n sub\n beqz __f1case0\n", s);
            Assert.Contains(" push.d.sp 4 ; switch value\n push 2\n sub\n beqz __f1case1\n", s);
            Assert.Contains(" b __f1default\n", s);
            Assert.Contains("__f1case0:\n", s);
            Assert.Contains("__f1endswitch:\n", s);
        }

        [Fact]
        public void SwitchWithoutDefaultJumpsToEnd()
        {
            var s = Compile("void f(int n) { switch (n) { case 1: break; } }");
            Assert.Contains(" b __f1endswitch\n", s);
        }

        [Fact]
        public void BreakInSwitchLeavesSwitchButContinueGoesToLoop()
        {
            var s = Compile("void f() { int i; for (i = 0; i < 3; i++) { switch (i) { case 1: continue; case 2: break; } } }");
            Assert.Contains(" b __f1continue\n", s);
            Assert.Contains(" b __f2endswitch\n", s);
        }

        [Theory]
        [InlineData("void f() { break; }")]
        [InlineData("void f() { switch (1) { case 1: continue; } }")]
        [InlineData("void f() { case 1: ; }")]
        [InlineData("void f() { default: ; }")]
        [InlineData("void f(int n) { switch (n) { case 1: break; case 1: break; } }")]
        [InlineData("void f(int n) { switch (n) { default: break; default: break; } }")]
        [InlineData("void f(float n) { switch (n) { case 1: break; } }")]
        [InlineData("void f(int n) { switch (n) { case 1.5: break; } }")]
        public void BadSwitchUse(string code)
        {
            Assert.ThrowsAny<Exception>(() => Compile(code));
        }

        [Fact]
        public void StructParameterIsCopiedByMemcpy()
        {
            var s = Compile("struct P { int x; int y; }; int f(int k, struct P p) { return p.y; }");
            // k is popped; the address of the caller's struct is popped by memcpy.sp into the slots of p
            Assert.Contains("f: ; func f\n pop.sp 0\n memcpy.sp 8, 4 ; p\n", s);
            Assert.Contains(" push.d.sp 8 ; p.y\n", s);
        }

        [Fact]
        public void StructArgumentIsPushedAsItsAddress()
        {
            var s = Compile("struct P { int x; int y; }; int g(int k, struct P p) { return k; } void f() { struct P a; g(5, a); }");
            // last argument first; a struct is passed as its address
            Assert.Contains(" push.sp 0 ; &a\n push 5\n jal ", s);
        }

        [Fact]
        public void NestedStructArgumentIsItsAddress()
        {
            var s = Compile("struct P { int x; int y; }; struct L { struct P a; int t; }; void g(struct L l) { } void f(struct L l) { g(l); }");
            Assert.Contains(" push.sp 0 ; &l\n jal ", s);
            Assert.Contains("g: ; func g\n memcpy.sp 12, 0 ; l\n", s);
        }

        [Fact]
        public void StructArgumentThroughPointer()
        {
            var s = Compile("struct P { int x; int y; }; void g(struct P p) { } void f(struct P *q) { g(*q); }");
            // the pointer itself is the address of the struct
            Assert.Contains(" push.d.sp 0 ; q\n jal ", s);
        }

        [Theory]
        [InlineData("struct A { int x; }; struct B { int x; }; void g(struct A a) { } void f() { struct B b; g(b); }")]
        [InlineData("struct A { int x; }; void g(struct A a) { } void f() { g(1); }")]
        [InlineData("struct A { int x; }; void g(int a) { } void f() { struct A a; g(a); }")]
        [InlineData("struct A { int x; }; void f() { struct A a; trap_puti(a, 1); }")]
        public void BadStructArguments(string code)
        {
            Assert.ThrowsAny<Exception>(() => Compile(code));
        }

        [Fact]
        public void ReturnOfLocalStructPushesItsAddress()
        {
            var s = Compile("struct P { int x; int y; }; struct P f() { struct P r; r.x = 1; return r; }");
            // one pointer into the callee's own (dead) frame
            Assert.Contains(" push.sp 0 ; &r\n ret\n", s);
        }

        [Fact]
        public void ReturnOfParameterStructPushesItsAddress()
        {
            var s = Compile("struct P { int x; int y; }; struct P f(int k, struct P p) { return p; }");
            Assert.Contains(" push.sp 4 ; &p\n ret\n", s);
        }

        [Fact]
        public void ReturnOfGlobalStructIsCopiedToHiddenLocal()
        {
            var s = Compile("struct P { int x; int y; }; struct P g; struct P f() { return g; }");
            Assert.Contains(" push.wp g\n memcpy.sp 8, 0 ; struct P\n push.sp 0 ; (return value)\n ret\n", s);
        }

        [Fact]
        public void ReturnOfCallResultPassesThePointerThrough()
        {
            var s = Compile("struct P { int x; }; struct P g() { struct P r; return r; } struct P f() { return g(); }");
            Assert.Contains("f: ; func f\n jal 4, g\n ret\n", s);
        }

        [Fact]
        public void CallResultStoredInLocalIsJalThenMemcpySp()
        {
            var s = Compile("struct P { int x; int y; }; struct P g() { struct P r; return r; } void f() { struct P a; a = g(); }");
            Assert.Contains(" jal 4, g\n memcpy.sp 8, 0 ; struct P\n", s);
        }

        [Fact]
        public void UnusedStructResultIsDropped()
        {
            var s = Compile("struct P { int x; }; struct P g() { struct P r; return r; } void f() { g(); }");
            Assert.Contains(" jal 4, g\n drop\n", s);
        }

        [Fact]
        public void StructResultMemberAndArgumentAreCopiedFirst()
        {
            var s = Compile("struct P { int x; int y; }; struct P g() { struct P r; return r; } int h(struct P p) { return p.x; } int f() { return h(g()) + g().y; }");
            // the returned struct is copied out of the dead frame before the call that would overwrite it; its address is the argument
            Assert.Contains("jal 4, g\n memcpy.sp 8, 0 ; g()\n push.sp 0 ; &g()\n jal 4, h\n", s);
        }

        [Theory]
        [InlineData("struct A { int x; }; struct B { int x; }; struct A f() { struct B b; return b; }")]
        [InlineData("struct A { int x; }; struct A f() { return 1; }")]
        [InlineData("struct A { int x; }; int f() { struct A a; return a; }")]
        [InlineData("struct A { int x; }; void f() { struct A a; a = 1 + f2(); } struct A f2() { struct A a; return a; }")]
        public void BadStructReturns(string code)
        {
            Assert.ThrowsAny<Exception>(() => Compile(code));
        }

        [Fact]
        public void JalCountIsWhatIsAliveAtThatPoint()
        {
            // the same pattern as real data: jal 4 (no temporary yet), the 16 bytes result is copied to sp+16,
            // then jal 12 (locals 8 + temporary 16 + system 8 = 32 -> 48 bytes)
            var s = Compile(
                "struct V { int a; int b; int c; int d; }; struct V m(int k) { struct V v; return v; } " +
                "int cmp(struct V a, struct V b) { return a.a; } int f(int self, int i) { return cmp(m(i), m(self)); }"
            );
            Assert.Contains(" push.d.sp 0 ; self\n jal 4, m\n memcpy.sp 16, 16 ; m()\n", s);
            Assert.Contains(" push.d.sp 4 ; i\n jal 12, m\n memcpy.sp 16, 32 ; m()\n", s);
            Assert.Contains(" jal 16, cmp\n", s);
        }

        [Fact]
        public void LocalsDeclaredLaterDoNotEnlargeEarlierJals()
        {
            var s = Compile("void g() { } void f() { int a; g(); int b; int c; int d; int e; g(); }");
            Assert.Contains(" jal 4, g\n", s);
            Assert.Contains(" jal 8, g\n", s);
        }

        [Fact]
        public void TemporariesAreReleasedWhenTheStatementIsDone()
        {
            var s = Compile(
                "struct P { int x; int y; }; struct P g() { struct P r; return r; } " +
                "int f() { int a = g().x; int b = g().y; return a + b; }"
            );
            // both results use the same slots (16 bytes aligned), and b is declared after the first temporary was released
            Assert.Contains(" memcpy.sp 8, 16 ; g()\n push.d.sp 16 ; g().x\n pop.sp 0 ; a\n", s);
            Assert.Contains(" memcpy.sp 8, 16 ; g()\n push.d.sp 20 ; g().y\n pop.sp 4 ; b\n", s);
        }

        [Fact]
        public void TemporariesOfAConditionAreReleasedBeforeTheBody()
        {
            var s = Compile(
                "struct P { int x; int y; }; struct P g() { struct P r; return r; } " +
                "void f() { if (g().x) { int a = g().y; } while (g().x) { int b = g().y; } }"
            );
            // the temporary of each condition is at sp+0 or sp+16 (16 bytes aligned), never accumulated
            Assert.DoesNotContain("memcpy.sp 8, 32", s);
            Assert.DoesNotContain("memcpy.sp 8, 48", s);
        }

        [Fact]
        public void FunctionWithPointerStoreCoversTheSaveSlotInEveryJal()
        {
            // the save slot is above everything, and a jal in the pointer expression runs while it is alive
            var s = Compile("int *p() { return 0; } void g() { } void f() { g(); *p() = 1; }");
            Assert.Contains(" jal 4, g\n", s);
            Assert.Contains(" jal 4, p\n", s);
        }

        [Fact]
        public void AssignmentVisitsRightSideFirst()
        {
            // the copy of g() is alive while the call on the left side runs, so that call must cover it
            var s = Compile(
                "struct P { int x; int y; }; struct P g() { struct P r; return r; } struct P *q() { return 0; } " +
                "void f() { *q() = g(); }"
            );
            var jalQ = System.Text.RegularExpressions.Regex.Match(s, @"jal (\d+), q");
            Assert.True(jalQ.Success);
            Assert.True(int.Parse(jalQ.Groups[1].Value) >= 4);
        }

        [Fact]
        public void ConstantIndexOfLocalArrayIsItsSlot()
        {
            var s = Compile("void f() { int a[3]; a[1] = 5; }");
            Assert.Contains(" push 5\n dup\n pop.sp 4 ; a.1\n drop\n", s);
        }

        [Fact]
        public void DynamicIndexAddsIndexTimesElementSize()
        {
            var s = Compile("int f(int i) { int a[3]; return a[i]; }");
            Assert.Contains(" push.sp 4 ; &a\n push.d.sp 0 ; i\n push 4\n mul\n add\n push.d.pop 0 ; deref\n", s);
        }

        [Fact]
        public void DynamicIndexOfStructArrayUsesStructSize()
        {
            var s = Compile("struct P { int x; int y; }; int f(int i) { struct P a[3]; return a[i].y; }");
            Assert.Contains(" push 8\n mul\n add\n push.d.pop 4\n", s);
        }

        [Fact]
        public void PointerArithmeticMovesByThePointee()
        {
            Assert.Contains(" push 1\n push 8\n mul\n add\n", Compile("struct P { int x; int y; }; void f(struct P *p) { struct P *q = p + 1; }"));
            Assert.Contains(" push 1\n push 4\n mul\n add\n", Compile("void f(int *p) { int *q = p + 1; }"));
        }

        [Fact]
        public void GlobalArrayHasLabelsForEveryElement()
        {
            var s = Compile("int g[2][3]; void f() { g[1][2] = 4; }");
            Assert.Contains(" pop.wp g.1.2\n", s);
            Assert.Contains("g.1:\ng.1.0:\n resb 4\ng.1.1:\n resb 4\ng.1.2:\n resb 4", s);
            var r = new BdxCEncoder().EncodeC("int g[2][3]; void f() { }", "w.c");
            Assert.Equal(24, r.WorkSize);
        }

        [Fact]
        public void ArrayTakesOneSlotPerElementInTheFrame()
        {
            // 3 slots + 2 system DWORDs = 5, aligned to 8
            Assert.Contains("jal 8, g", Compile("void g() { } void f() { int a[3]; g(); }"));
        }

        [Fact]
        public void ArrayDecaysToPointerWhenPassed()
        {
            var s = Compile("int sum(int *p) { return p[0]; } int f() { int a[2]; return sum(a); }");
            Assert.Contains(" push.sp 0 ; &a\n jal ", s);
        }

        [Fact]
        public void ArrayParameterIsPointer()
        {
            var s = Compile("int f(int a[], int i) { return a[i]; }");
            Assert.Contains(" push.d.sp 0 ; a\n push.d.sp 4 ; i\n push 4\n mul\n add\n push.d.pop 0 ; deref\n", s);
        }

        [Fact]
        public void ArrayInitializerFillsTheRestWithZero()
        {
            var s = Compile("void f() { int a[3] = { 1 }; }");
            Assert.Contains(" push 1\n pop.sp 0 ; a.0\n push 0\n pop.sp 4 ; a.1\n push 0\n pop.sp 8 ; a.2\n", s);
        }

        [Fact]
        public void ArrayLengthComesFromTheInitializer()
        {
            var s = Compile("int g[] = { 5, 6 }; void f() { }");
            Assert.Contains("_initc:\n push 5\n pop.wp g.0\n push 6\n pop.wp g.1\n ret\n", s);
            Assert.Equal(8, new BdxCEncoder().EncodeC("int g[] = { 5, 6 }; void f() { }", "w.c").WorkSize);
        }

        [Fact]
        public void NestedArrayInitializer()
        {
            var s = Compile("void f() { int m[2][2] = { { 1, 2 }, { 3 } }; }");
            Assert.Contains(" push 1\n pop.sp 0 ; m.0.0\n push 2\n pop.sp 4 ; m.0.1\n push 3\n pop.sp 8 ; m.1.0\n push 0\n pop.sp 12 ; m.1.1\n", s);
        }

        [Fact]
        public void ArrayMemberOfStructIsCopiedWithTheStruct()
        {
            var s = Compile("struct B { int id; int v[2]; }; void f() { struct B a; struct B b; b = a; }");
            // the array member is part of the one memcpy of the struct
            Assert.Contains(" push.sp 0 ; &a\n memcpy.sp 12, 12 ; struct B\n", s);
        }

        [Fact]
        public void ArrayMemberIsPassedByValueWithTheStruct()
        {
            var s = Compile("struct B { int v[2]; }; void g(struct B b) { } void f() { struct B a; g(a); }");
            Assert.Contains(" push.sp 0 ; &a\n jal ", s);
        }

        [Fact]
        public void TypedefNameIsTheType()
        {
            var s = Compile("typedef int s32; typedef unsigned short u16; s32 g; s32 f(u16 a) { s32 n; n = a; return n; }");
            Assert.Contains("section .bss\ng:\n resb 4", s);
            Assert.Contains("f: ; func f\n pop.sp 0\n", s);
        }

        [Fact]
        public void TypedefOfPointersArraysAndStructs()
        {
            var s = Compile(
                "typedef struct P { int x; int y; } P; typedef struct { int a; } A, *PA; typedef int Row[3]; typedef Row Grid[2]; typedef P *PP;" +
                "int f() { PA pa; Grid g; PP pp; return sizeof(Grid) + sizeof(P) + sizeof(A) + sizeof(PP); }"
            );
            // 24 + 8 + 4 + 4
            Assert.Contains(" push 24\n", s);
            Assert.Contains(" push 8\n", s);
        }

        [Fact]
        public void TypedefOfAnonymousStructsAreDistinctTypes()
        {
            Assert.ThrowsAny<Exception>(() => Compile("typedef struct { int a; } A; typedef struct { int a; } B; void f() { A x; B y; x = y; }"));
            Compile("typedef struct { int a; } A; void f() { A x; A y; x = y; }");
        }

        [Fact]
        public void TypedefOfEnumAndLocalTypedef()
        {
            var s = Compile("typedef enum { OFF, ON = 4 } Mode; Mode g = ON; int f() { typedef int Local; Local v = 3; return v; }");
            Assert.Contains("_initc:\n push 4\n pop.wp g\n ret\n", s);
        }

        [Fact]
        public void TypedefNameWorksInCallsAndCasts()
        {
            Compile("typedef int s32; s32 id(s32 a) { return a; } s32 f(s32 b) { id(b); return (s32)b; }");
        }

        [Theory]
        [InlineData("typedef int s32 = 3;")]
        [InlineData("typedef Unknown T;")]
        [InlineData("void f() { Nope x; }")]
        public void BadTypedef(string code)
        {
            Assert.ThrowsAny<Exception>(() => Compile(code));
        }

        [Fact]
        public void EnumeratorsAreIntConstants()
        {
            var s = Compile("enum Color { RED, GREEN = 5, BLUE }; int f() { return RED + GREEN + BLUE; }");
            Assert.Contains(" push 0\n push 5\n add\n push 6\n add\n", s);
        }

        [Fact]
        public void EnumValuesCanUseEarlierEnumeratorsAndNegatives()
        {
            var s = Compile("enum L { LOW = 10, MID = 20, HIGH = LOW + MID, NEG = -5, AFTER }; int f() { return HIGH; } int g() { return AFTER; }");
            Assert.Contains("f: ; func f\n push 30\n", s);
            Assert.Contains("g: ; func g\n push -4\n", s);
        }

        [Fact]
        public void EnumVariablesTypesAndSizeofAreInt()
        {
            var s = Compile("enum Color { RED, GREEN }; enum Color g = GREEN; enum Color f(enum Color c) { return c; } int n() { return sizeof(enum Color); }");
            Assert.Contains("_initc:\n push 1\n pop.wp g\n ret\n", s);
            Assert.Contains("n: ; func n\n push 4\n", s);
        }

        [Fact]
        public void EnumeratorsWorkAsCaseLabelsAndArrayLengths()
        {
            Compile("enum E { A, B, C }; int t[C + 1]; int f(int x) { switch (x) { case A: return 1; case C: return 3; } return t[C]; }");
        }

        [Fact]
        public void ALocalVariableHidesAnEnumerator()
        {
            var s = Compile("enum E { A = 7 }; int f() { int A = 1; return A; }");
            Assert.Contains(" push.d.sp 0 ; A\n", s);
        }

        [Theory]
        [InlineData("enum E { A, A };")]
        [InlineData("enum E { A }; enum E { B };")]
        [InlineData("void f() { enum Nope n; }")]
        [InlineData("enum class E { A };")]
        [InlineData("int n; enum E { A = n };")]
        [InlineData("enum E { A }; void f() { A = 1; }")]
        public void BadEnum(string code)
        {
            Assert.ThrowsAny<Exception>(() => Compile(code));
        }

        [Fact]
        public void SizeofOfAStructGivesAnArrayLength()
        {
            var s = Compile("struct P { int x; int y; }; void f() { int a[sizeof(struct P) / 4]; int n; n = 1; }");
            // a has 2 elements
            Assert.Contains(" pop.sp 8 ; n\n", s);
            Assert.ThrowsAny<Exception>(() => Compile("struct P { int x; int y; }; void f() { int a[sizeof(struct P) / 4]; a[2] = 1; }"));
        }

        [Fact]
        public void SizeofOfAVariableGivesAnArrayLength()
        {
            var s = Compile("int base[5]; int copy[sizeof(base) / sizeof(base[0])]; void f() { copy[4] = 1; }");
            Assert.Contains("copy.4:\n", s);
            Assert.ThrowsAny<Exception>(() => Compile("int base[5]; int copy[sizeof(base) / sizeof(base[0])]; void f() { copy[5] = 1; }"));
        }

        [Fact]
        public void SizeofOfAStructGivesTheLengthOfAMemberArray()
        {
            var s = Compile("struct P { int x; int y; }; struct Q { int v[sizeof(struct P)]; }; void f() { struct Q q; int n; q.v[7] = 1; n = 1; }");
            Assert.Contains(" pop.sp 32 ; n\n", s);
        }

        [Fact]
        public void AVariableIsNotAConstantInAnArrayLength()
        {
            Assert.ThrowsAny<Exception>(() => Compile("int n; void f() { int a[n]; }"));
            Assert.ThrowsAny<Exception>(() => Compile("int n; void f() { int a[sizeof(n) + n]; }"));
        }

        [Theory]
        [InlineData("void f() { int a[0]; }")]
        [InlineData("void f() { int a[-1]; }")]
        [InlineData("void f(int n) { int a[n]; }")]
        [InlineData("void f() { int a[]; }")]
        [InlineData("void f() { int a[2][]; }")]
        [InlineData("void f() { int a[2]; a[2] = 1; }")]
        [InlineData("void f() { int a[2]; a[-1] = 1; }")]
        [InlineData("void f() { int a[2]; int *p = &a; }")]
        [InlineData("void f() { int a[2]; int b[2]; a = b; }")]
        [InlineData("void f() { int a[2] = { 1, 2, 3 }; }")]
        [InlineData("void f() { int a[2] = 1; }")]
        [InlineData("void f() { int a[2]; float x; a[x] = 1; }")]
        [InlineData("void f() { int a; a[0] = 1; }")]
        [InlineData("void f() { char s[2] = \"abc\"; }")]
        [InlineData("void f() { char s[2] = { 1, 2, 3 }; }")]
        [InlineData("void f(int n) { char s[2] = { n }; }")]
        [InlineData("void f() { char s[2]; char t[2]; s = t; }")]
        [InlineData("void f() { char s[2]; s[2] = 1; }")]
        [InlineData("void f() { char s[]; }")]
        public void BadArrays(string code)
        {
            Assert.ThrowsAny<Exception>(() => Compile(code));
        }

        [Fact]
        public void InitcIsGlobalInitializersThenRet()
        {
            var s = new BdxCEncoder().EncodeC("int g = 5; void main() { }", "i.c").Bdscript.Replace("\r\n", "\n");
            Assert.Contains("_initc:\n push 5\n pop.wp g\n ret\n", s);
        }

        [Fact]
        public void WorkSizeCountsBss()
        {
            var r = new BdxCEncoder().EncodeC("int a; struct S { int x; int y; } s; void f() { }", "w.c");
            Assert.Equal(12, r.WorkSize);
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

        [Fact]
        public void AssignmentValueIsStoredValue()
        {
            var s = Compile("int f() { int a; int b; a = b = 3; return a; }");
            // b = 3 leaves the stored value (dup) for the outer assignment to consume
            Assert.Contains(" push 3\n dup\n pop.sp 4 ; b\n dup\n pop.sp 0 ; a\n drop\n", s);
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
