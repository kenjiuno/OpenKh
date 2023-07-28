using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Xe.BinaryMapper;

namespace OpenKh.Command.AnbMaker.Models.XmlOps
{
    public class FrameDataElement
    {
        [XmlAttribute] public float FrameStart { get; set; }
        [XmlAttribute] public float FrameEnd { get; set; }
        [XmlAttribute] public float FramesPerSecond { get; set; }
        [XmlAttribute] public float FrameReturn { get; set; }
    }
}
