using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenKh.Kh2Bdx.Utils.CStuff
{
    internal record TypeDesc(
        string BaseName,
        int Size,
        LLComparators? Comparators = null,
        LLMath? Math = null,
        int PointerDepth = 0,
        TypeDesc? BaseType = null,
        bool IsStruct = false
        )
    {
        public string Display => $"{BaseName}{new string('*', PointerDepth)}";

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
                Math = LLMath.PointerVer,
                PointerDepth = PointerDepth + 1,
                BaseType = (PointerDepth == 0) 
                    ? this 
                    : BaseType ?? throw new Exception($"BaseType must exists of {Display}"),
            };
        }
    }
}
