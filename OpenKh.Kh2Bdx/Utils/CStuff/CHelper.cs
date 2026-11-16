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
        Func<int, int> AllocateStackItems,
        Func<string, FuncSig?>? GetFunc = null,
        Func<byte[], string>? AddStringLiteral = null
        )
    {
        /// <summary>
        /// Placeholder replaced by the frame size (DWORDs, including 2 system DWORDs) of the calling function
        /// once the function body has been fully compiled.
        /// </summary>
        public const string FrameSizePlaceholder = "@@FRAMESIZE@@";

        /// <summary>
        /// The frame size (DWORDs) passed to jal: locals + 2 system DWORDs (count, ret-ip), rounded up to a
        /// multiple of 4 DWORDs (16 bytes). Every jal in real data has such a count (4 to 76), as sp is kept 16 bytes aligned.
        /// </summary>
        public static int FrameSizeOf(int numLocalDwords) => (numLocalDwords + 2 + 3) & ~3;

        /// <summary>
        /// Placeholder replaced by the byte offset of a hidden per-function sp slot, which keeps
        /// the original sp[0] while a pointer store borrows it. Allocated only if used.
        /// </summary>
        public const string PointerSaveSlotPlaceholder = "@@PTRSAVE@@";

        /// <summary>
        /// Store the value on top of tp to *(pointer + offset).
        ///
        /// The only store-through-pointer instruction is `pop.sp.d`, which takes the pointer from sp[0]
        /// (*(*sp + imm16)). sp[0] normally holds a user variable, so it is saved and restored around the store.
        /// The pointer expression is evaluated before sp[0] is overwritten.
        /// </summary>
        internal string PointerStore(string? pointerGetter, int offset)
        {
            return JoinLines(
                "push.d.sp 0 ; save sp[0]",
                $"pop.sp {PointerSaveSlotPlaceholder}",
                pointerGetter,
                "pop.sp 0 ; pointer register",
                $"pop.sp.d {offset}",
                $"push.d.sp {PointerSaveSlotPlaceholder}",
                "pop.sp 0 ; restore sp[0]"
            )!;
        }

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

            // Pointers are plain 4-byte values: allow `p = 0`, `int x = (int)p` and void* <-> T*.
            static bool IsIntLike(TypeDesc t) => t.PointerDepth == 0 && !t.IsStruct && t.Math == LLMath.IntVer;
            static bool IsVoidPtr(TypeDesc t) => t.PointerDepth == 1 && t.BaseType == TypeDesc.Void;
            if ((ToType.PointerDepth > 0 && IsIntLike(FromType))
                || (FromType.PointerDepth > 0 && IsIntLike(ToType))
                || (FromType.PointerDepth > 0 && ToType.PointerDepth > 0 && (IsVoidPtr(FromType) || IsVoidPtr(ToType))))
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

                if (newType.IsStruct)
                {
                    // *p of struct pointer: every member occupies a 4-byte slot (see StructVar),
                    // read with push.d.pop and written with pop.sp.d.
                    return StructVar.Build(
                        newType,
                        0,
                        "",
                        getter: (off, _) => JoinLines(lval.Getter, $"push.d.pop {off}"),
                        setter: (off, _) => PointerStore(lval.Getter, off),
                        pointer: (off, _) => (off == 0)
                            ? lval.Getter!
                            : JoinLines(lval.Getter, $"push {off}", "add")
                    );
                }

                return new VarRef(
                    Type: newType,
                    Getter: (size <= 4)
                        ? JoinLines(
                            lval.Getter,
                            $"push.d.pop 0 ; deref"
                        )
                        : null,
                    // pop.sp.d stores a whole word, so only 4-byte pointees are writable
                    // (a store to char/short would clobber the neighbouring bytes).
                    Setter: (size == 4)
                        ? PointerStore(lval.Getter, 0)
                        : null,
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
                    Setter: PointerStore(lval.Getter, 0), // pointee is a pointer: 4 bytes
                    GetPointer: lval.Getter
                );
            }
        }

        /// <summary>
        /// Code to copy the struct value `src` to `dst`, member by member (every leaf member is one 4-byte slot).
        /// Leaf conversions are not needed because both sides must have the same struct type.
        /// </summary>
        internal string CopyStruct(VarRef dst, VarRef src, SourceRef sourceRef)
        {
            static bool IsStructValue(TypeDesc t) => t.IsStruct && t.PointerDepth == 0 && t.Members != null;

            if (!IsStructValue(dst.Type) || !IsStructValue(src.Type) || dst.Type.BaseName != src.Type.BaseName)
            {
                throw new CompilerException(sourceRef, $"Cannot assign {src.Type.Display} to {dst.Type.Display}");
            }

            var lines = new List<string?>();

            if (src.DiscardCode != null
                && dst.GetPointer is { } dstPointer
                && System.Text.RegularExpressions.Regex.Match(dstPointer, @"^push\.sp (\d+)( ;.*)?$") is { Success: true } local)
            {
                // call returning a struct, stored straight into a local: jal, then memcpy.sp from the returned pointer
                return JoinLines(src.DiscardCode, $"memcpy.sp {dst.Type.Size}, {local.Groups[1].Value} ; {dst.Type.Display}")!;
            }

            lines.Add(src.Prelude);

            void Copy(VarRef to, VarRef from)
            {
                if (IsStructValue(to.Type))
                {
                    foreach (var member in to.Type.Members!)
                    {
                        Copy(to.Members![member.Name], from.Members![member.Name]);
                    }
                    return;
                }

                lines.Add(from.Getter ?? throw new CompilerException(sourceRef, "Source member has no getter"));
                lines.Add(to.Setter ?? throw new CompilerException(sourceRef, $"Destination member of {dst.Type.Display} cannot be written"));
            }

            Copy(dst, src);

            return JoinLines(lines.ToArray())!;
        }

        /// <summary>The scalar members of a struct value in slot order (nested structs are flattened).</summary>
        internal IEnumerable<VarRef> StructLeaves(VarRef value)
        {
            if (value.Type.IsStruct && value.Type.PointerDepth == 0 && value.Type.Members != null)
            {
                foreach (var member in value.Type.Members)
                {
                    foreach (var leaf in StructLeaves(value.Members![member.Name]))
                    {
                        yield return leaf;
                    }
                }
            }
            else
            {
                yield return value;
            }
        }

        internal VarRef MakePointer(VarRef lval, SourceRef sourceRef)
        {
            var newType = lval.Type.MakePointerType();

            return new VarRef(
                Type: newType,
                Getter: lval.GetPointer ?? throw new CompilerException(sourceRef, $"Cannot get pointer of type {lval.Type.Display}"),
                Setter: null,
                GetPointer: null
            );
        }
    }
}
