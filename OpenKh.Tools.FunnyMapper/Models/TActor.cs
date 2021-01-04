using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace OpenKh.Tools.FunnyMapper.Models
{
    public class TActor
    {
        [XmlAttribute]
        public string Name;

        [XmlAttribute]
        public string Preview;

        [XmlElement]
        public string SpawnPoint;
    }
}
