using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace OpenKh.Command.AnbMaker.Models.XmlOps
{
    [XmlRoot(Namespace = Common.Namespace)]
    public class MsetElement
    {
        [XmlElement(Namespace = Common.Namespace)] public InterpolatedAnbElement[]? InterpolatedAnb { get; set; }
    }
}
