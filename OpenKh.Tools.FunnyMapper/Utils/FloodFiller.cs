using System;
using System.Collections.Generic;
using System.Text;

namespace OpenKh.Tools.FunnyMapper.Utils
{
    class FloodFiller
    {
        public static void Run(
            int cx, int cy, int startX, int startY, Func<int, int, string> describeCell, Action<int, int> setter
        )
        {
            var target = describeCell(startX, startY);
            var footPrint = new HashSet<string>();
            var queue = new Queue<NextPos>();
            queue.Enqueue(new NextPos { x = startX, y = startY });
            while (queue.Count != 0)
            {
                var next = queue.Dequeue();
                var y = next.y;
                if ((uint)y >= (uint)cy)
                {
                    continue;
                }
                if (target != describeCell(next.x, y))
                {
                    continue;
                }
                foreach (var toLeft in new bool[] { true, false })
                {
                    var x = next.x + (toLeft ? -1 : 0);
                    for (; (uint)x < (uint)cx; x += toLeft ? -1 : 1)
                    {
                        var key = y + "," + x;
                        if (footPrint.Contains(key))
                        {
                            break;
                        }
                        footPrint.Add(key);

                        if (target != describeCell(x, y))
                        {
                            break;
                        }
                        setter(x, y);
                        queue.Enqueue(new NextPos { x = x, y = y - 1 });
                        queue.Enqueue(new NextPos { x = x, y = y + 1 });
                    }
                }
            }
        }

        class NextPos
        {
            internal int x;
            internal int y;
        }
    }
}
