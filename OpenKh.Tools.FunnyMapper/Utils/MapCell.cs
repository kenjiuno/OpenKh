using OpenKh.Tools.FunnyMapper.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;

namespace OpenKh.Tools.FunnyMapper.Utils
{
    public class MapCell
    {
        internal int x;
        internal int y;
        internal Border border;
        internal Image underlay;
        internal Image overlay;
        internal TBg bg;
        internal TActor actor;

        internal bool HasFloor => bg != null;
    }
}
