using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Xe.BinaryMapper;

namespace OpenKh.Command.AnbMaker.Models.XmlOps
{
    public class LimiterElement
    {
        [XmlAttribute] public int I { get; set; }

        [XmlAttribute] public int Type { get; set; }
        [XmlAttribute] public bool HasXMin { get; set; }
        [XmlAttribute] public bool HasXMax { get; set; }
        [XmlAttribute] public bool HasYMin { get; set; }
        [XmlAttribute] public bool HasYMax { get; set; }
        [XmlAttribute] public bool HasZMin { get; set; }
        [XmlAttribute] public bool HasZMax { get; set; }
        [XmlAttribute] public bool Global { get; set; }

        [XmlAttribute] public float DampingWidth { get; set; }
        [XmlAttribute] public float DampingStrength { get; set; }
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
