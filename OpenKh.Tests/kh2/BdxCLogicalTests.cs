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
