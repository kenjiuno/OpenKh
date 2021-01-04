using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;
using YamlDotNet.Serialization;

namespace OpenKh.Tools.FunnyMapper.Models
{
    public class TBg
    {
        [XmlAttribute]
        public string Name;

        [XmlAttribute]
        public float Height;

        public bool IsVisible => !Name.StartsWith(".");

        [XmlElement]
        public TSurface Floor;

        [XmlElement]
        public TSurface[] Wall;
    }
}
