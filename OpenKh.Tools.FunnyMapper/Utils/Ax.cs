using OpenKh.Tools.FunnyMapper.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenKh.Tools.FunnyMapper.Utils
{
    public class Ax
    {
        public int dx;
        public int dy;
        public SurfaceSide side;

        public static readonly Ax[] axis = new Ax[] {
            new Ax { dx =  0, dy = -1, side = SurfaceSide.N },
            new Ax { dx =  1, dy =  0, side = SurfaceSide.E },
            new Ax { dx = -1, dy =  0, side = SurfaceSide.W },
            new Ax { dx =  0, dy =  1, side = SurfaceSide.S },
        };
    }
}
