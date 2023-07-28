using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Xe.BinaryMapper;

namespace OpenKh.Command.AnbMaker.Models.XmlOps
{
    public class RootPositionElement
    {
        [XmlAttribute] public float ScaleX { get; set; }
        [XmlAttribute] public float ScaleY { get; set; }
        [XmlAttribute] public float ScaleZ { get; set; }
        [XmlAttribute] public int NotUnit { get; set; }
        [XmlAttribute] public float RotateX { get; set; }
        [XmlAttribute] public float RotateY { get; set; }
        [XmlAttribute] public float RotateZ { get; set; }
        [XmlAttribute] public float RotateW { get; set; }
        [XmlAttribute] public float TranslateX { get; set; }
        [XmlAttribute] public float TranslateY { get; set; }
        [XmlAttribute] public float TranslateZ { get; set; }
        [XmlAttribute] public float TranslateW { get; set; }
        [XmlAttribute] public string? FCurveId { get; set; }
    }
}
