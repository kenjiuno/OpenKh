using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace OpenKh.Kh2Bdx.Utils.CStuff
{
    /// <summary>
    /// The sp frame of the function being compiled.
    ///
    /// Locals live to the end of the function. Temporaries (such as the copy of a returned struct) live only
    /// while the statement that needs them is compiled, then they are released and their slots are reused.
    ///
    /// A jal gets the size of the area that is alive at that point (see <see cref="CHelper.FrameSizeOf"/>), because the
    /// callee's frame starts right after it. So the count differs from jal to jal in a function.
    /// </summary>
    internal class FrameState
    {
        private record Entry(int Start, bool IsTemp);

        private readonly Stack<Entry> _entries = new();
        private readonly List<int> _jalTops = new();

        /// <summary>DWORDs in use (locals and live temporaries).</summary>
        public int Top { get; private set; }

        /// <summary>The largest <see cref="Top"/> reached so far.</summary>
        public int HighWater { get; private set; }

        /// <summary>Allocate `numItems` DWORDs. Returns the index of the first one.</summary>
        /// <param name="temp">a temporary that can be released by <see cref="ReleaseTemps"/></param>
        /// <param name="align16">start at a 16 bytes boundary, as real data places the copies of returned structs</param>
        public int Allocate(int numItems, bool temp = false, bool align16 = false)
        {
            var start = Top;
            if (align16)
            {
                Top = (Top + 3) & ~3;
            }
            var index = Top;
            Top += numItems;
            HighWater = Math.Max(HighWater, Top);
            _entries.Push(new Entry(start, temp));
            return index;
        }

        /// <summary>
        /// Release the temporaries allocated since `mark` (<see cref="Top"/> at that time). A temporary
        /// below a local that was declared after it cannot be released (stack discipline), it just stays.
        /// </summary>
        public void ReleaseTemps(int mark)
        {
            while (_entries.Count != 0 && _entries.Peek() is { IsTemp: true } entry && mark <= entry.Start)
            {
                Top = entry.Start;
                _entries.Pop();
            }
        }

        /// <summary>The frame size placeholder of one jal, resolved by <see cref="ResolveJalFrameSizes"/>.</summary>
        public string NextJalFrameSize()
        {
            _jalTops.Add(Top);
            return $"@@FRAMESIZE:{_jalTops.Count - 1}@@";
        }

        /// <summary>One more DWORD above everything ever used, for the sp[0] save slot of pointer stores.</summary>
        public int ReserveAboveHighWater()
        {
            return HighWater++;
        }

        /// <param name="alwaysAtLeast">every jal covers at least this many DWORDs</param>
        public string ResolveJalFrameSizes(string body, int alwaysAtLeast)
        {
            return Regex.Replace(
                body,
                @"@@FRAMESIZE:(\d+)@@",
                m => CHelper.FrameSizeOf(Math.Max(_jalTops[int.Parse(m.Groups[1].Value)], alwaysAtLeast)).ToString()
            );
        }
    }
}
