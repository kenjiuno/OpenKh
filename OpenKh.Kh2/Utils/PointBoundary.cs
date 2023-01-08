using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenKh.Kh2.Utils
{
    internal class PointBoundary
    {
        private readonly SortedSet<int> _points = new SortedSet<int>();

        public void AddPoints(params int[] points)
        {
            foreach (var point in points)
            {
                _points.Add(point);
            }
        }

        public int GetLengthFromPoint(int point)
        {
            var next = _points.Where(it => point < it).Take(1).ToArray();
            if (next.Any())
            {
                return next.First() - point;
            }
            else
            {
                throw new Exception($"Next point since {point} is not registered");
            }
        }
    }
}
