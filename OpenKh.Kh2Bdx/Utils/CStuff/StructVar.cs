using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenKh.Kh2Bdx.Utils.CStuff
{
    /// <summary>
    /// A member of a struct. Every scalar occupies one 4-byte slot (VM memory is word based).
    /// </summary>
    /// <param name="Slot">position of the member in 4-byte units from the beginning of the struct</param>
    internal record StructMember(string Name, TypeDesc Type, int Slot);

    internal static class StructVar
    {
        /// <summary>Number of 4-byte slots that a variable of the type occupies.</summary>
        internal static int SlotsOf(TypeDesc type) => type.IsByteArray
            ? (Math.Max(type.ArrayLength, 0) + 3) / 4 // char a[N] packs 4 bytes in a slot
            : type.IsArray
                ? Math.Max(type.ArrayLength, 0) * SlotsOf(type.ElementType!)
                : (type.Size == 0) ? 1 : (type.Size + 3) / 4;

        /// <summary>
        /// What `p + 1` adds to a pointer to the type, and the distance between the elements of an array of it, in bytes.
        /// A char is one byte; everything else takes whole slots.
        /// </summary>
        internal static int StrideOf(TypeDesc type) => type.IsByte ? 1 : 4 * SlotsOf(type);

        /// <summary>
        /// Create VarRef of the variable. Struct variable is expanded to its members, and array variable to its elements, recursively.
        /// </summary>
        /// <param name="off">byte offset from the beginning of the variable</param>
        /// <param name="path">name path such as "s.inner.a" or "a.2"</param>
        internal static VarRef Build(
            TypeDesc type,
            int off,
            string path,
            Func<int, string, string> getter,
            Func<int, string, string>? setter,
            Func<int, string, string> pointer)
        {
            if (type.IsByteArray)
            {
                // the bytes are packed in word slots; the slots are what can be read, written and labeled
                var slots = Enumerable.Range(0, SlotsOf(type))
                    .Select(i => Build(TypeDesc.Int32, off + 4 * i, $"{path}.{i}", getter, setter, pointer))
                    .ToArray();
                return new VarRef(
                    Type: type,
                    Getter: pointer(off, path),
                    GetPointer: pointer(off, path),
                    Slots: slots
                );
            }

            if (type.IsArray)
            {
                var elementType = type.ElementType!;
                var stride = 4 * SlotsOf(elementType);
                var elements = Enumerable.Range(0, Math.Max(type.ArrayLength, 0))
                    .Select(i => Build(elementType, off + stride * i, $"{path}.{i}", getter, setter, pointer))
                    .ToArray();
                // the value of an array is the address of its first element
                return new VarRef(
                    Type: type,
                    Getter: pointer(off, path),
                    GetPointer: pointer(off, path),
                    Elements: elements
                );
            }

            if (type.IsStruct && type.PointerDepth == 0 && type.Members != null)
            {
                var members = type.Members.ToDictionary(
                    m => m.Name,
                    m => Build(m.Type, off + 4 * m.Slot, path + "." + m.Name, getter, setter, pointer)
                );
                return new VarRef(
                    Type: type,
                    GetPointer: pointer(off, path),
                    Members: members
                );
            }

            return new VarRef(
                Type: type,
                Getter: getter(off, path),
                Setter: setter?.Invoke(off, path),
                GetPointer: pointer(off, path)
            );
        }
    }
}
