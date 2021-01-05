using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace OpenKh.Tools.FunnyMapper.Utils
{
    public class CoordSpace
    {
        public float BlkSize = 128;
        public int Cx = 32;
        public int Cy = 32;

        public Vector3 GetOriginVectorOf(MapCell cell)
        {
            return new Vector3(
                BlkSize * cell.x,
                cell.bg?.Height ?? 0f,
                -BlkSize * cell.y
            );
        }
    }
}
