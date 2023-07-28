using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace OpenKh.Command.AnbMaker.Models.XmlOps
{
    public class KeyFloatElement
    {
        [XmlAttribute] public int I { get; set; }
        [XmlText] public float Value { get; set; }
    }
}
