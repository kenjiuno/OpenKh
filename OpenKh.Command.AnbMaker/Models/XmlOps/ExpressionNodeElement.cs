using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace OpenKh.Command.AnbMaker.Models.XmlOps
{
    public class ExpressionNodeElement
    {
        [XmlAttribute] public int I { get; set; }

        [XmlAttribute] public float Value { get; set; }
        [XmlAttribute] public short CAR { get; set; }
        [XmlAttribute] public short CDR { get; set; }

        [XmlAttribute] public int Element { get; set; }
        [XmlAttribute] public byte Type { get; set; }
        [XmlAttribute] public bool IsGlobal { get; set; }
    }
}
