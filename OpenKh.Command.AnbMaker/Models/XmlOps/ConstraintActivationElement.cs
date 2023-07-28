using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace OpenKh.Command.AnbMaker.Models.XmlOps
{
    public class ConstraintActivationElement
    {
        [XmlAttribute] public int I { get; set; }
        [XmlAttribute] public float Time { get; set; }
        [XmlAttribute] public int Active { get; set; }
    }
}
