using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenKh.Kh2Bdx.Utils.CStuff
{
    /// <summary>
    /// Integer constants. The code of a constant expression is only pushes and arithmetic,
    /// so it is evaluated by running that code on a small stack.
    /// </summary>
    internal static class ConstEval
    {
        internal static int Evaluate(
            CPP14Parser.ConstantExpressionContext constantExpression,
            Func<Antlr4.Runtime.ParserRuleContext, SourceRef> getSourceRef)
        {
            var cHelper = new CHelper(
                GetVar: name => throw new CompilerException(getSourceRef(constantExpression), $"'{name}' is not a constant"),
                DeclareVar: (_, sourceRef) => throw new CompilerException(sourceRef, "Not a constant expression"),
                DeclareStaticVar: (_, sourceRef) => throw new CompilerException(sourceRef, "Not a constant expression"),
                AllocateStackItems: _ => throw new CompilerException(getSourceRef(constantExpression), "Not a constant expression")
            );
            var value = new ExpressionToVarRefVisitor(getSourceRef, cHelper, new ReduceTypeDesc(getSourceRef))
                .VisitConstantExpression(constantExpression);

            if (value.Getter == null || !TryEvaluateCode(value.Getter, out var result))
            {
                throw new CompilerException(getSourceRef(constantExpression), "Not a constant expression");
            }
            return result;
        }

        /// <summary>The length in `a[N]`: a constant that is positive.</summary>
        internal static int EvaluateArrayLength(
            CPP14Parser.ConstantExpressionContext constantExpression,
            Func<Antlr4.Runtime.ParserRuleContext, SourceRef> getSourceRef)
        {
            var length = Evaluate(constantExpression, getSourceRef);
            if (length <= 0)
            {
                throw new CompilerException(getSourceRef(constantExpression), "Array length must be positive");
            }
            return length;
        }

        /// <summary>Run code that only pushes constants and calculates. False if it is anything else.</summary>
        internal static bool TryEvaluateCode(string code, out int result)
        {
            result = 0;
            var stack = new Stack<int>();

            bool Binary(Func<int, int, int> f)
            {
                if (stack.Count < 2)
                {
                    return false;
                }
                var b = stack.Pop();
                var a = stack.Pop();
                stack.Push(f(a, b));
                return true;
            }

            foreach (var rawLine in code.Replace("\r\n", "\n").Split('\n'))
            {
                var line = rawLine;
                var comment = line.IndexOf(';');
                if (0 <= comment)
                {
                    line = line.Substring(0, comment);
                }
                line = line.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (line.StartsWith("push "))
                {
                    if (!int.TryParse(line.Substring(5).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                    {
                        return false;
                    }
                    stack.Push(n);
                    continue;
                }

                var ok = line switch
                {
                    "add" => Binary((a, b) => a + b),
                    "sub" => Binary((a, b) => a - b),
                    "mul" => Binary((a, b) => a * b),
                    "div" => Binary((a, b) => b == 0 ? throw new DivideByZeroException() : a / b),
                    "mod" => Binary((a, b) => b == 0 ? throw new DivideByZeroException() : a % b),
                    "and" => Binary((a, b) => a & b),
                    "or" => Binary((a, b) => a | b),
                    "xor" => Binary((a, b) => a ^ b),
                    "sll" => Binary((a, b) => a << (b & 31)),
                    "sra" => Binary((a, b) => a >> (b & 31)),
                    "neg" => stack.Count != 0 && Push(stack, -stack.Pop()),
                    "not" => stack.Count != 0 && Push(stack, ~stack.Pop()),
                    _ => false,
                };
                if (!ok)
                {
                    return false;
                }
            }

            if (stack.Count != 1)
            {
                return false;
            }
            result = stack.Pop();
            return true;
        }

        private static bool Push(Stack<int> stack, int value)
        {
            stack.Push(value);
            return true;
        }
    }

    internal static class ArrayTypes
    {
        /// <summary>
        /// The array type, after checking that its elements can be laid out.
        /// Elements are 4-byte slots (the memory is word based), so arrays of char or short are refused:
        /// writing one element would clobber its neighbours.
        /// </summary>
        /// <param name="length">-1 if the length is omitted (`int a[]`)</param>
        internal static TypeDesc Make(TypeDesc elementType, int length, SourceRef sourceRef)
        {
            if (length == 0 || length < -1)
            {
                throw new CompilerException(sourceRef, "Array length must be positive");
            }

            if (elementType.IsArray && elementType.ArrayLength < 0)
            {
                throw new CompilerException(sourceRef, "Only the first dimension of an array can be omitted");
            }

            var wordSized = elementType.IsArray
                || elementType.PointerDepth != 0
                || (elementType.IsStruct && elementType.Members != null)
                || elementType.Size == 4;
            if (!wordSized)
            {
                throw new CompilerException(
                    sourceRef,
                    $"Arrays of {elementType.Display} are not supported: memory is word based, and one element would clobber its neighbours"
                );
            }

            return elementType.MakeArrayType(length);
        }
    }
}
