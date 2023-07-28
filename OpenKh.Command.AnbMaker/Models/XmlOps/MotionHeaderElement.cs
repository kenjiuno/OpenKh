using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Xe.BinaryMapper;

namespace OpenKh.Command.AnbMaker.Models.XmlOps
{
    public class MotionHeaderElement
    {
        [XmlAttribute] public int Type { get; set; }
        [XmlAttribute] public int SubType { get; set; }
        [XmlAttribute] public int ExtraOffset { get; set; }
        [XmlAttribute] public int ExtraSize { get; set; }
    }
}
