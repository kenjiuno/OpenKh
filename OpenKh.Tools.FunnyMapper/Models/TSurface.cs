using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace OpenKh.Tools.FunnyMapper.Models
{
    public class TSurface
    {
        [XmlAttribute]
        public string Texture;

        [XmlAttribute]
        public string Side;
    }
}
