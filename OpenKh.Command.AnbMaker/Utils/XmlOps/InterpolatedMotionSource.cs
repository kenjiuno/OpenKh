using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OpenKh.Kh2.Motion;

namespace OpenKh.Command.AnbMaker.Utils.XmlOps
{
    public record InterpolatedMotionSource(InterpolatedMotion Interpolated, string Name)
    {
    }
}
