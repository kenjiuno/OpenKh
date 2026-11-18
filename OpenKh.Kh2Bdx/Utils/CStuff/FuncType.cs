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

    /// <summary>
    /// Function pointers. bdscript cannot jump to an address in a variable (jal takes a relative offset), so the value of a
    /// function pointer is the number of the function (1, 2, ..., 0 is null), and a call through it is a
    /// `jal` to a stub that compares the number with each function of the same type and calls that one.
    /// Only functions whose address is taken are numbered, and one stub is made for each function type that is called.
    /// </summary>
    internal class FunctionPointers
    {
        private readonly Dictionary<string, int> _index = new();
        private readonly Dictionary<string, string> _keyOf = new();
        private readonly Dictionary<string, string> _stubs = new();

        /// <summary>The number of the function (taking its address numbers it).</summary>
        public int IndexOf(string function, string key)
        {
            if (!_index.TryGetValue(function, out var index))
            {
                index = _index.Count + 1;
                _index[function] = index;
                _keyOf[function] = key;
            }
            return index;
        }

        /// <summary>The label of the stub that calls through a function pointer of the type.</summary>
        public string StubFor(string key)
        {
            if (!_stubs.TryGetValue(key, out var label))
            {
                label = $"__icall{_stubs.Count + 1}";
                _stubs[key] = label;
            }
            return label;
        }

        /// <summary>
        /// The stubs. The number is on top of tp (pushed last) and the arguments are below it, so the stub pops the
        /// number and the callee then takes the arguments from tp as for a direct call.
        /// </summary>
        public void WriteStubs(TextWriter writer)
        {
            foreach (var (key, label) in _stubs)
            {
                writer.WriteLine($"{label}: ; call through a function pointer {key}");
                writer.WriteLine("pop.sp 0 ; function number");
                var next = 0;
                foreach (var function in _index.Where(it => _keyOf[it.Key] == key).OrderBy(it => it.Value))
                {
                    next += 1;
                    writer.WriteLine("push.d.sp 0");
                    writer.WriteLine($"push {function.Value}");
                    writer.WriteLine("sub");
                    writer.WriteLine($"bnez {label}_{next}");
                    writer.WriteLine($"jal {CHelper.FrameSizeOf(1)}, {function.Key}");
                    writer.WriteLine("ret");
                    writer.WriteLine($"{label}_{next}:");
                }
                // a null or unknown function pointer
                writer.WriteLine("halt");
            }
        }
    }
}
