using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace OpenKh.Command.AnbMaker.Models.XmlOps
{
    public class ExternalEffectorElement
    {
        [XmlAttribute] public int I { get; set; }
        [XmlAttribute] public short JointId { get; set; }
    }
}
