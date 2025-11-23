using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenKh.Kh2Bdx.Utils.CStuff
{
    internal record CHelper(
        Func<string, VarRef?> GetVar,
        Func<ArgPair, SourceRef, VarRef> DeclareVar,
        Func<ArgPair, SourceRef, VarRef> DeclareStaticVar,
        Func<int, int> AllocateStackItems
        )
    {
        private readonly IEnumerable<(TypeDesc FromType, TypeDesc ToType, string? Converter)> _knownConverters = GetKnownConverters()
            .ToImmutableArray();

        private static IEnumerable<(TypeDesc FromType, TypeDesc ToType, string? Converter)> GetKnownConverters()
        {
            yield return (TypeDesc.Int8, TypeDesc.Int8, "");
            yield return (TypeDesc.UInt8, TypeDesc.Int8, "");
            yield return (TypeDesc.Int16, TypeDesc.Int8, "push 255\nand\n");
            yield return (TypeDesc.UInt16, TypeDesc.Int8, "push 255\nand\n");
            yield return (TypeDesc.Int32, TypeDesc.Int8, "push 255\nand\n");
            yield return (TypeDesc.UInt32, TypeDesc.Int8, "push 255\nand\n");
            yield return (TypeDesc.Float32, TypeDesc.Int8, "cvt.s.w\npush 255\nand\n"); // not sure

            yield return (TypeDesc.Int8, TypeDesc.UInt8, "");
            yield return (TypeDesc.UInt8, TypeDesc.UInt8, "");
            yield return (TypeDesc.Int16, TypeDesc.UInt8, "push 255\nand\n");
            yield return (TypeDesc.UInt16, TypeDesc.UInt8, "push 255\nand\n");
            yield return (TypeDesc.Int32, TypeDesc.UInt8, "push 255\nand\n");
            yield return (TypeDesc.UInt32, TypeDesc.UInt8, "push 255\nand\n");
            yield return (TypeDesc.Float32, TypeDesc.UInt8, "cvt.s.w\npush 255\nand\n");

            yield return (TypeDesc.Int8, TypeDesc.Int16, "push 24\nsll\npush 24\nsra\n");
            yield return (TypeDesc.UInt8, TypeDesc.Int16, "push 255\nand\n");
            yield return (TypeDesc.Int16, TypeDesc.Int16, "");
            yield return (TypeDesc.UInt16, TypeDesc.Int16, "");
            yield return (TypeDesc.Int32, TypeDesc.Int16, "push 65535\nand\n");
            yield return (TypeDesc.UInt32, TypeDesc.Int16, "push 65535\nand\n");
            yield return (TypeDesc.Float32, TypeDesc.Int16, "cvt.s.w\npush 65535\nand\n"); // not sure

            yield return (TypeDesc.Int8, TypeDesc.UInt16, "push 24\nsll\npush 24\nsra\n");
            yield return (TypeDesc.UInt8, TypeDesc.UInt16, "push 255\nand\n");
            yield return (TypeDesc.Int16, TypeDesc.UInt16, "");
            yield return (TypeDesc.UInt16, TypeDesc.UInt16, "");
            yield return (TypeDesc.Int32, TypeDesc.UInt16, "push 65535\nand\n");
            yield return (TypeDesc.UInt32, TypeDesc.UInt16, "push 65535\nand\n");
            yield return (TypeDesc.Float32, TypeDesc.UInt16, "cvt.s.w\npush 65535\nand\n");

            yield return (TypeDesc.Int8, TypeDesc.Int32, "push 24\nsll\npush 24\nsra\n");
            yield return (TypeDesc.UInt8, TypeDesc.Int32, "push 255\nand\n");
            yield return (TypeDesc.Int16, TypeDesc.Int32, "push 16\nsll\npush 16\nsra\n");
            yield return (TypeDesc.UInt16, TypeDesc.Int32, "push 65535\nand\n");
            yield return (TypeDesc.Int32, TypeDesc.Int32, "");
            yield return (TypeDesc.UInt32, TypeDesc.Int32, "");
            yield return (TypeDesc.Float32, TypeDesc.Int32, "cvt.s.w");

            yield return (TypeDesc.Int8, TypeDesc.UInt32, "push 24\nsll\npush 24\nsra\n");
            yield return (TypeDesc.UInt8, TypeDesc.UInt32, "push 255\nand\n");
            yield return (TypeDesc.Int16, TypeDesc.UInt32, "push 16\nsll\npush 16\nsra\n");
            yield return (TypeDesc.UInt16, TypeDesc.UInt32, "push 65535\nand\n");
            yield return (TypeDesc.Int32, TypeDesc.UInt32, "");
            yield return (TypeDesc.UInt32, TypeDesc.UInt32, "");
            yield return (TypeDesc.Float32, TypeDesc.UInt32, "cvt.s.w"); // not sure

            yield return (TypeDesc.Int8, TypeDesc.Float32, "push 24\nsll\npush 24\nsra\ncvt.w.s\n");
            yield return (TypeDesc.UInt8, TypeDesc.Float32, "push 255\nand\ncvt.w.s\n");
            yield return (TypeDesc.Int16, TypeDesc.Float32, "push 16\nsll\npush 16\nsra\ncvt.w.s\n");
            yield return (TypeDesc.UInt16, TypeDesc.Float32, "push 65535\nand\ncvt.w.s\n");
            yield return (TypeDesc.Int32, TypeDesc.Float32, "cvt.w.s");
            yield return (TypeDesc.UInt32, TypeDesc.Float32, "cvt.w.s"); // not sure
            yield return (TypeDesc.Float32, TypeDesc.Float32, "");
        }

        internal string? GetConverter(TypeDesc FromType, TypeDesc ToType)
        {
            if (FromType == ToType)
            {
                return "";
            }

            var match = _knownConverters.FirstOrDefault(x => x.FromType == FromType && x.ToType == ToType);
            if (match != default)
            {
                return match.Converter;
            }

            return null;
        }

        /// <summary>
        /// Combine the getter of a VarRef with additional instructions.
        /// 
        /// In this case, setter is no more relevant, so it is set to null.
        /// </summary>
        internal VarRef CombineGetterWithInstruction(VarRef lval, string? lines)
        {
            return new VarRef(
                Type: lval.Type,
                Getter: lval.Getter is null ? null : lval.Getter + "\n" + lines,
                Setter: null
            );
        }

        internal string? JoinLines(params string?[] ones)
        {
            return string.Join(
                "\n",
                ones
                    .Select(it => (it ?? "")?.TrimEnd())
            );
        }

        internal SourceRef GetSourceRefFromSymbol(IToken symbol)
        {
            return new SourceRef(
                SourceName: symbol.TokenSource.SourceName,
                Line: symbol.Line,
                Column: symbol.Column
            );
        }

        internal VarRef Deref(VarRef lval, SourceRef sourceRef)
        {
            if (lval.Type.PointerDepth == 0)
            {
                throw new CompilerException(sourceRef, $"Cannot dereference non-pointer type {lval.Type.Display}");
            }

            if (lval.Type.PointerDepth == 1)
            {
                if (lval.Type.Size == 0)
                {
                    throw new CompilerException(sourceRef, $"Cannot dereference void pointer type {lval.Type.Display}");
                }

                var newType = lval.Type.BaseType ?? throw new CompilerException(sourceRef, $"Pointer type {lval.Type.Display} has no base type");
                var size = newType.Size;

                return new VarRef(
                    Type: newType,
                    Getter: (size <= 4)
                        ? JoinLines(
                            lval.Getter,
                            $"push.d.pop 0 ; deref"
                        )
                        : null,
                    Setter: null,
                    GetPointer: lval.Getter
                );
            }
            else
            {
                var newType = lval.Type with
                {
                    PointerDepth = lval.Type.PointerDepth - 1,
                };

                return new VarRef(
                    Type: newType,
                    Getter: JoinLines(
                        lval.Getter,
                        $"push.d.pop 0 ; deref"
                    ),
                    Setter: null,
                    GetPointer: lval.Getter
                );
            }
        }

        internal VarRef MakePointer(VarRef lval, SourceRef sourceRef)
        {
            var newType = lval.Type with
            {
                PointerDepth = lval.Type.PointerDepth + 1,
            };

            return new VarRef(
                Type: newType,
                Getter: lval.GetPointer ?? throw new CompilerException(sourceRef, $"Cannot get pointer of type {lval.Type.Display}"),
                Setter: null,
                GetPointer: null
            );
        }
    }
}
