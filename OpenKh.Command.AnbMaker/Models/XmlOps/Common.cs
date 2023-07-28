using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenKh.Command.AnbMaker.Models.XmlOps
{
    internal class Common
    {
        /// <summary>
        /// This XML namespace indicates a kind of version.
        /// Bump this when any of structure under `OpenKh.Command.AnbMaker.Models.XmlOps` has been changed.
        /// 
        /// The intension is to reject to import a XML from different version.
        /// </summary>
        public const string Namespace = "https://openkh.dev/ns/OpenKh.Command.AnbMaker.Models.XmlOps/20230728_1804";
    }
}
