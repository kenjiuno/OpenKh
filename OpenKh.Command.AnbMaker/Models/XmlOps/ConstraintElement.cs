using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Xe.BinaryMapper;

namespace OpenKh.Command.AnbMaker.Models.XmlOps
{
    public class ConstraintElement
    {
        [XmlAttribute] public int I { get; set; }

        [XmlAttribute] public byte Type { get; set; }
        [XmlAttribute] public byte TemporaryActiveFlag { get; set; }
        [XmlAttribute] public short ConstrainedJointId { get; set; }
        [XmlAttribute] public short SourceJointId { get; set; }
        [XmlAttribute] public short LimiterId { get; set; }
        [XmlAttribute] public short ActivationCount { get; set; }
        [XmlAttribute] public short ActivationStartId { get; set; }
    }
}
