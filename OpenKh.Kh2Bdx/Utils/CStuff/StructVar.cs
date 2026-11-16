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
        internal static int SlotsOf(TypeDesc type) => (type.Size == 0) ? 1 : (type.Size + 3) / 4;

        /// <summary>
        /// Create VarRef of the variable. Struct variable is expanded to its members recursively.
        /// </summary>
        /// <param name="off">byte offset from the beginning of the variable</param>
        /// <param name="path">name path such as "s.inner.a"</param>
        internal static VarRef Build(
            TypeDesc type,
            int off,
            string path,
            Func<int, string, string> getter,
            Func<int, string, string>? setter,
            Func<int, string, string> pointer)
        {
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
