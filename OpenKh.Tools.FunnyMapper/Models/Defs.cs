using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace OpenKh.Tools.FunnyMapper.Models
{
    [XmlRoot]
    public class Defs
    {
        [XmlElement]
        public TBg[] Bg;

        [XmlElement]
        public TActor[] Actor;
    }
}
