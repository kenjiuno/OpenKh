using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenKh.Kh2Bdx.Utils.CStuff
{
    /// <param name="ArrayLength">
    /// Length of an array type. -1 for `int a[]`, whose length comes from the initializer.
    /// An array type is a pointer type (to <paramref name="ElementType"/>) whose value is the address of its first element,
    /// so it decays to the pointer for free. Its storage is <see cref="StructVar.SlotsOf"/> slots.
    /// </param>
    /// <param name="ElementType">the type of one element, or null if this is not an array type</param>
    internal record TypeDesc(
        string BaseName,
        int Size,
        LLComparators? Comparators = null,
        LLMath? Math = null,
        int PointerDepth = 0,
        TypeDesc? BaseType = null,
        bool IsStruct = false,
        IReadOnlyList<StructMember>? Members = null,
        int ArrayLength = 0,
        TypeDesc? ElementType = null
        )
    {
        public bool IsArray => ElementType != null;

        /// <summary>char / unsigned char as a value (not a pointer, not an array)</summary>
        public bool IsByte => PointerDepth == 0 && !IsStruct && ElementType == null && Size == 1;

        /// <summary>An array of char: its bytes are packed, 4 in a slot.</summary>
        public bool IsByteArray => ElementType is { IsByte: true };

        /// <summary>short / unsigned short as a value</summary>
        public bool IsShort => PointerDepth == 0 && !IsStruct && ElementType == null && Size == 2;

        /// <summary>char or short: smaller than a slot, so it is read with a mask and written with a 1 or 2 byte memcpy</summary>
        public bool IsPacked => IsByte || IsShort;

        /// <summary>An array of char or short: its elements are packed in the slots.</summary>
        public bool IsPackedArray => ElementType is { IsPacked: true };

        public string Display => IsArray
            ? $"{ElementType!.Display}[{(0 <= ArrayLength ? ArrayLength.ToString() : "")}]"
            : $"{BaseName}{new string('*', PointerDepth)}";

        public static TypeDesc Void = new TypeDesc(
            BaseName: "void",
            Size: 0
            );

        public static TypeDesc Int8 = new TypeDesc(
            BaseName: "signed char",
            Size: 1,
            Comparators: LLComparators.IntVer,
            Math: LLMath.IntVer
            );

        public static TypeDesc UInt8 = new TypeDesc(
            BaseName: "unsigned char",
            Size: 1,
            Comparators: LLComparators.IntVer,
            Math: LLMath.IntVer
            );

        public static TypeDesc Int16 = new TypeDesc(
            BaseName: "signed short",
            Size: 2,
            Comparators: LLComparators.IntVer,
            Math: LLMath.IntVer
            );

        public static TypeDesc UInt16 = new TypeDesc(
            BaseName: "unsigned short",
            Size: 2,
            Comparators: LLComparators.IntVer,
            Math: LLMath.IntVer
            );

        public static TypeDesc Int32 = new TypeDesc(
            BaseName: "signed int",
            Size: 4,
            Comparators: LLComparators.IntVer,
            Math: LLMath.IntVer
            );

        public static TypeDesc UInt32 = new TypeDesc(
            BaseName: "unsigned int",
            Size: 4,
            Comparators: LLComparators.IntVer,
            Math: LLMath.IntVer
            );

        public static TypeDesc Float32 = new TypeDesc(
            BaseName: "float",
            Size: 4,
            Comparators: LLComparators.FloatVer,
            Math: LLMath.FloatVer
            );

        public TypeDesc MakePointerType()
        {
            return this with
            {
                Size = 4,
                Comparators = LLComparators.IntVer,
                // `p + 1` moves by what p points to: this one element in bytes (every scalar is a 4-byte slot)
                Math = LLMath.PointerVerOf(StructVar.StrideOf(this)),
                PointerDepth = PointerDepth + 1,
                IsStruct = false,
                Members = null,
                ArrayLength = 0,
                ElementType = null,
                BaseType = (PointerDepth == 0) 
                    ? this 
                    : BaseType ?? throw new Exception($"BaseType must exists of {Display}"),
            };
        }

        /// <summary>
        /// An array of this type. The value of an array is the address of its first element, so it is also
        /// a pointer to this type, which moves by one element for `a + 1`.
        /// </summary>
        public TypeDesc MakeArrayType(int length)
        {
            return MakePointerType() with
            {
                ArrayLength = length,
                ElementType = this,
            };
        }

        /// <summary>What an array becomes when it is used as a value: a pointer to its element type.</summary>
        public TypeDesc Decay() => IsArray ? ElementType!.MakePointerType() : this;
    }
}
