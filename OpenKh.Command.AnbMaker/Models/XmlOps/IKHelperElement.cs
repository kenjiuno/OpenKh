using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Xe.BinaryMapper;

namespace OpenKh.Command.AnbMaker.Models.XmlOps
{
    public class IKHelperElement
    {
        [XmlAttribute] public int I { get; set; }

        [XmlAttribute] public short Index { get; set; }
        [XmlAttribute] public short SiblingId { get; set; }
        [XmlAttribute] public short ParentId { get; set; }
        [XmlAttribute] public short ChildId { get; set; }
        [XmlAttribute] public int Reserved { get; set; }

        [XmlAttribute] public int Unknown { get; set; }
        [XmlAttribute] public bool Terminate { get; set; }
        [XmlAttribute] public bool Below { get; set; }
        [XmlAttribute] public bool EnableBias { get; set; }

        [XmlAttribute] public float ScaleX { get; set; }
        [XmlAttribute] public float ScaleY { get; set; }
        [XmlAttribute] public float ScaleZ { get; set; }
        [XmlAttribute] public float ScaleW { get; set; }
        [XmlAttribute] public float RotateX { get; set; }
        [XmlAttribute] public float RotateY { get; set; }
        [XmlAttribute] public float RotateZ { get; set; }
        [XmlAttribute] public float RotateW { get; set; }
        [XmlAttribute] public float TranslateX { get; set; }
        [XmlAttribute] public float TranslateY { get; set; }
        [XmlAttribute] public float TranslateZ { get; set; }
        [XmlAttribute] public float TranslateW { get; set; }

    }
}
