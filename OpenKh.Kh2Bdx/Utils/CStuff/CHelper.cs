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
        Func<byte[], string>? AddStringLiteral = null,
        FrameState? Frame = null
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

        /// <summary>Remember the current top of the sp frame, to <see cref="ReleaseTemps"/> later.</summary>
        internal int TempMark() => Frame?.Top ?? 0;

        /// <summary>The temporaries allocated since the mark are dead (their statement or condition is done).</summary>
        internal void ReleaseTemps(int mark) => Frame?.ReleaseTemps(mark);

        /// <summary>The frame size operand of a jal: what is alive now, resolved when the function is complete.</summary>
        internal string NextJalFrameSize() => Frame?.NextJalFrameSize() ?? FrameSizePlaceholder;

        /// <summary>
        /// A temporary in the frame for the copy of a returned struct. Real data places it at a 16 bytes boundary.
        /// </summary>
        internal int AllocateTemp(int numItems) =>
            Frame?.Allocate(numItems, temp: true, align16: true) ?? AllocateStackItems(numItems);

        /// <summary>
        /// Placeholders replaced by the byte offsets of two hidden sp slots that hold the pointer and the value of a
        /// store through a pointer. They are alive for a few instructions with no jal between, so no jal covers them.
        /// Allocated only if used.
        /// </summary>
        public const string StorePointerSlotPlaceholder = "@@STOREPTR@@";
        public const string StoreValueSlotPlaceholder = "@@STOREVAL@@";

        /// <summary>
        /// Store the value on top of tp to *(pointer + offset).
        ///
        /// `memcpy 0` (op4) pops a value and then a destination pointer and stores the value as 4 bytes. The value is
        /// already on tp below the pointer expression, so the two are put in hidden slots to be pushed in the order
        /// the instruction wants.
        /// </summary>
        internal string PointerStore(string? pointerGetter, int offset)
        {
            return JoinLines(
                pointerGetter,
                $"pop.sp {StorePointerSlotPlaceholder} ; pointer",
                $"pop.sp {StoreValueSlotPlaceholder} ; value",
                $"push.d.sp {StorePointerSlotPlaceholder}",
                offset == 0 ? null : $"push {offset}\nadd",
                $"push.d.sp {StoreValueSlotPlaceholder}",
                "memcpy 0"
            )!;
        }

        /// <summary>
        /// The end of a store to a variable reached through a pointer, with `address; value` on tp: store, and leave
        /// the stored value on tp (it is the value of the assignment).
        /// A statement that does not use the value drops these extras, see <see cref="WithoutStoreResult"/>.
        /// </summary>
        internal static string StoreTail(PointerTarget target) => target.IsByte
            ? string.Join("\n", $"pop.sp {StoreValueSlotPlaceholder}", $"push.sp {StoreValueSlotPlaceholder}", "memcpy 1", $"push.d.sp {StoreValueSlotPlaceholder}")
            : string.Join("\n", "dup", $"pop.sp {StoreValueSlotPlaceholder}", "memcpy 0", $"push.d.sp {StoreValueSlotPlaceholder}");

        /// <summary>
        /// The code of an expression statement that ends with <see cref="StoreTail"/>, without the copy of the stored
        /// value (and so without the `drop` that would follow). Null if the code does not end so.
        /// </summary>
        internal static string? WithoutStoreResult(string code)
        {
            var lines = code.Split('\n').Select(it => it.TrimEnd()).ToList();
            if (lines.Count < 2
                || lines[^1] != $"push.d.sp {StoreValueSlotPlaceholder}"
                || lines[^2] is not ("memcpy 0" or "memcpy 1"))
            {
                return null;
            }

            lines.RemoveAt(lines.Count - 1);
            if (lines[^1] == "memcpy 0")
            {
                // the last `dup; pop.sp slot` is the one of this store (a nested store has its own, before it)
                for (int x = lines.Count - 2; 1 <= x; x--)
                {
                    if (lines[x] == $"pop.sp {StoreValueSlotPlaceholder}" && lines[x - 1] == "dup")
                    {
                        lines.RemoveRange(x - 1, 2);
                        break;
                    }
                }
            }
            return string.Join("\n", lines);
        }

        /// <summary>
        /// Store the low byte of the value on top of tp to *pointer.
        ///
        /// `memcpy 1` copies one byte from the source pointer it pops, so the value goes to a hidden slot first
        /// (the first byte of a little endian word is its low byte).
        /// </summary>
        internal string ByteStore(string? pointerGetter)
        {
            return JoinLines(
                pointerGetter,
                $"pop.sp {StorePointerSlotPlaceholder} ; pointer",
                $"pop.sp {StoreValueSlotPlaceholder} ; byte to store",
                $"push.d.sp {StorePointerSlotPlaceholder}",
                $"push.sp {StoreValueSlotPlaceholder}",
                "memcpy 1"
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
            if (FromType.IsArray)
            {
                // an array is used as the pointer to its first element
                FromType = FromType.Decay();
            }

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

        /// <summary>
        /// `array[index]` and `pointer[index]`, that is `*(array + index)`.
        /// </summary>
        internal VarRef Index(VarRef array, VarRef index, SourceRef sourceRef)
        {
            var type = array.Type;
            if (!type.IsArray && type.PointerDepth == 0)
            {
                throw new CompilerException(sourceRef, $"Subscripted value is neither an array nor a pointer: {type.Display}");
            }

            if (!type.IsArray && type.PointerDepth == 1 && type.BaseType is { IsStruct: false, Size: 2 })
            {
                throw new CompilerException(sourceRef, $"Indexing {type.Display} is not supported: memory is word based");
            }

            if (index.Getter == null || index.Type.PointerDepth != 0 || index.Type.IsStruct || index.Type.Math != LLMath.IntVer)
            {
                throw new CompilerException(sourceRef, "Array index must be an integer");
            }

            if (type.IsArray && 0 <= type.ArrayLength && ConstEval.TryEvaluateCode(index.Getter, out var checkedIndex)
                && (checkedIndex < 0 || type.ArrayLength <= checkedIndex))
            {
                throw new CompilerException(sourceRef, $"Index {checkedIndex} is out of the bounds of {type.Display}");
            }

            // a variable at a known place with a constant index is just that element
            // (the elements of a char array are bytes in a word: they are reached through their address)
            if (array.Elements != null && ConstEval.TryEvaluateCode(index.Getter, out var constantIndex))
            {
                return array.Elements[constantIndex];
            }

            var indexConverter = GetConverter(index.Type, TypeDesc.Int32)
                ?? throw new CompilerException(sourceRef, $"Cannot use {index.Type.Display} as an array index");

            // the size of one element in bytes
            var stride = type.IsArray
                ? StructVar.StrideOf(type.ElementType!)
                : (type.PointerDepth == 1 ? StructVar.StrideOf(type.BaseType!) : 4);

            var address = JoinLines(
                array.Getter ?? throw new CompilerException(sourceRef, "Array has no address"),
                index.Getter,
                indexConverter,
                $"push {stride}",
                "mul",
                "add"
            );

            return Deref(new VarRef(Type: type, Getter: address, GetPointer: address), sourceRef);
        }

        internal VarRef Deref(VarRef lval, SourceRef sourceRef)
        {
            if (lval.Type.IsArray)
            {
                // *array is the first element (or the first sub-array, whose value is just its address)
                var element = lval.Type.ElementType!;
                return element.IsArray
                    ? new VarRef(Type: element, Getter: lval.Getter, GetPointer: lval.Getter)
                    : Deref(new VarRef(Type: element.MakePointerType(), Getter: lval.Getter, GetPointer: lval.GetPointer), sourceRef);
            }

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
                            : JoinLines(lval.Getter, $"push {off}", "add"),
                        target: (off, _) => new PointerTarget(
                            (off == 0) ? lval.Getter! : JoinLines(lval.Getter, $"push {off}", "add"),
                            "push.d.pop 0"
                        )
                    );
                }

                if (newType.IsByte)
                {
                    // there is no byte load: read the word at the address and keep its lowest byte
                    var load = JoinLines(
                        "push.d.pop 0 ; deref char",
                        "push 255",
                        "and",
                        newType == TypeDesc.Int8 ? "push 128\nxor\npush 128\nsub" : null
                    );
                    return new VarRef(
                        Type: newType,
                        Getter: JoinLines(lval.Getter, load),
                        Setter: ByteStore(lval.Getter),
                        GetPointer: lval.Getter,
                        Target: new PointerTarget(lval.Getter!, load, IsByte: true)
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
                    // (a store to short would clobber the neighbouring bytes).
                    Setter: (size == 4)
                        ? PointerStore(lval.Getter, 0)
                        : null,
                    GetPointer: lval.Getter,
                    Target: (size == 4)
                        ? new PointerTarget(lval.Getter!, "push.d.pop 0 ; deref")
                        : null
                );
            }
            else
            {
                var newType = lval.Type with
                {
                    PointerDepth = lval.Type.PointerDepth - 1,
                    // `p + 1` moves by what the new type points to
                    Math = LLMath.PointerVerOf(
                        (lval.Type.PointerDepth - 1 == 1) ? StructVar.StrideOf(lval.Type.BaseType!) : 4
                    ),
                };

                return new VarRef(
                    Type: newType,
                    Getter: JoinLines(
                        lval.Getter,
                        $"push.d.pop 0 ; deref"
                    ),
                    Setter: PointerStore(lval.Getter, 0), // pointee is a pointer: 4 bytes
                    GetPointer: lval.Getter,
                    Target: new PointerTarget(lval.Getter!, "push.d.pop 0 ; deref")
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

            // The members are in consecutive slots, so the whole struct is one `memcpy`, as in real data
            // (the size is the ssub of op4, which has 10 bits).
            var size = dst.Type.Size;
            var srcPointer = src.DiscardCode ?? src.GetPointer;
            if (dst.GetPointer is { } dstPointer && srcPointer != null && 0 < size && size <= 1023)
            {
                if (System.Text.RegularExpressions.Regex.Match(dstPointer, @"^push\.sp (\d+)( ;.*)?$") is { Success: true } local)
                {
                    // into a local: the source pointer (for a call, the pointer it returns), then memcpy.sp
                    // from the returned pointer
                    return JoinLines(src.Prelude, srcPointer, $"memcpy.sp {size}, {local.Groups[1].Value} ; {dst.Type.Display}")!;
                }

                // anywhere else: destination pointer, then source pointer, then op4
                return JoinLines(src.Prelude, dstPointer, srcPointer, $"memcpy {size} ; {dst.Type.Display}")!;
            }

            lines.Add(src.Prelude);

            void Copy(VarRef to, VarRef from)
            {
                if (to.Type.IsByteArray)
                {
                    // the packed bytes are copied as whole slots
                    for (int x = 0; x < to.Slots!.Count; x++)
                    {
                        Copy(to.Slots[x], from.Slots![x]);
                    }
                    return;
                }

                if (to.Type.IsArray)
                {
                    // an array member of the struct is copied element by element
                    for (int x = 0; x < to.Elements!.Count; x++)
                    {
                        Copy(to.Elements[x], from.Elements![x]);
                    }
                    return;
                }

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
            if (value.Type.IsByteArray)
            {
                foreach (var slot in value.Slots!)
                {
                    yield return slot;
                }
            }
            else if (value.Type.IsArray)
            {
                foreach (var element in value.Elements!)
                {
                    foreach (var leaf in StructLeaves(element))
                    {
                        yield return leaf;
                    }
                }
            }
            else if (value.Type.IsStruct && value.Type.PointerDepth == 0 && value.Type.Members != null)
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
            if (lval.Type.IsArray)
            {
                throw new CompilerException(sourceRef, $"Address of an array is not supported: use the array itself or &a[0]");
            }

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
