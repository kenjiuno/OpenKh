using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Xe.BinaryMapper;

namespace OpenKh.Command.AnbMaker.Models.XmlOps
{
    public class JointElement
    {
        [XmlAttribute] public int I { get; set; }

        [XmlAttribute] public short JointId { get; set; }

        [XmlAttribute] public byte IK { get; set; }
        [XmlAttribute] public bool ExtEffector { get; set; }
        [XmlAttribute] public bool CalcMatrix2Rot { get; set; }
        [XmlAttribute] public bool Calculated { get; set; }
        [XmlAttribute] public bool Fixed { get; set; }
        [XmlAttribute] public bool Rotation { get; set; }
        [XmlAttribute] public bool Trans { get; set; }

        [XmlAttribute] public byte Reserved { get; set; }

    }
}
