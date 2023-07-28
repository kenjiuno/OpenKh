using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Xe.BinaryMapper;

namespace OpenKh.Command.AnbMaker.Models.XmlOps
{
    public class FCurveElement
    {
        [XmlAttribute] public int I { get; set; }

        [XmlAttribute] public short JointId { get; set; }
        [XmlAttribute] public int ChannelValue { get; set; }
        [XmlAttribute] public int Pre { get; set; }
        [XmlAttribute] public int Post { get; set; }
        [XmlAttribute] public byte KeyCount { get; set; }
        [XmlAttribute] public short KeyStartId { get; set; }
    }
}
