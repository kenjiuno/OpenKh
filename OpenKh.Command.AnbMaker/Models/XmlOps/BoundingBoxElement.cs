using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Xe.BinaryMapper;

namespace OpenKh.Command.AnbMaker.Models.XmlOps
{
    public class BoundingBoxElement
    {
        [XmlAttribute] public float MinX { get; set; }
        [XmlAttribute] public float MinY { get; set; }
        [XmlAttribute] public float MinZ { get; set; }
        [XmlAttribute] public float MinW { get; set; }
        [XmlAttribute] public float MaxX { get; set; }
        [XmlAttribute] public float MaxY { get; set; }
        [XmlAttribute] public float MaxZ { get; set; }
        [XmlAttribute] public float MaxW { get; set; }
    }
}
