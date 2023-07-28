using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Xe.BinaryMapper;

namespace OpenKh.Command.AnbMaker.Models.XmlOps
{
    public class InitialPoseElement
    {
        [XmlAttribute] public int I { get; set; }

        [XmlAttribute] public short BoneId { get; set; }
        [XmlAttribute] public int ChannelValue { get; set; }
        [XmlAttribute] public float Value { get; set; }
    }
}
