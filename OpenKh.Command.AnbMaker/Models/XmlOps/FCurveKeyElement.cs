using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Xe.BinaryMapper;

namespace OpenKh.Command.AnbMaker.Models.XmlOps
{
    public class FCurveKeyElement
    {
        [XmlAttribute] public int I { get; set; }

        [XmlAttribute] public int Type { get; set; }
        [XmlAttribute] public short Time { get; set; }
        [XmlAttribute] public short ValueId { get; set; }
        [XmlAttribute] public short LeftTangentId { get; set; }
        [XmlAttribute] public short RightTangentId { get; set; }
    }
}
