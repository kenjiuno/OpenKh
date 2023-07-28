using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Xe.BinaryMapper;

namespace OpenKh.Command.AnbMaker.Models.XmlOps
{
    public class ExpressionElement
    {
        [XmlAttribute] public int I { get; set; }

        [XmlAttribute] public short TargetId { get; set; }
        [XmlAttribute] public short TargetChannel { get; set; }
        [XmlAttribute] public short Reserved { get; set; }
        [XmlAttribute] public short NodeId { get; set; }
    }
}
