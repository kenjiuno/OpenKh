using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenKh.Kh2Bdx.Utils.CStuff
{
    /// <summary>
    /// The type of a function (return type and parameter types). Two function types are the same when they print the same.
    /// </summary>
    internal sealed class FuncType : IEquatable<FuncType>
    {
        public FuncType(TypeDesc ret, IReadOnlyList<TypeDesc> parameters)
        {
            Ret = ret;
            Params = parameters;
        }

        public TypeDesc Ret { get; }
        public IReadOnlyList<TypeDesc> Params { get; }

        public string Key => KeyOf(Ret, Params);

        public static string KeyOf(TypeDesc ret, IEnumerable<TypeDesc> parameters)
            => $"{ret.Display}({string.Join(",", parameters.Select(it => it.Display))})";

        public string Display => $"{Ret.Display}(*)({string.Join(",", Params.Select(it => it.Display))})";

        public bool Equals(FuncType? other) => other != null && Key == other.Key;
        public override bool Equals(object? obj) => Equals(obj as FuncType);
        public override int GetHashCode() => Key.GetHashCode();
    }
}
