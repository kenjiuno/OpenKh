using System;
using System.Collections.Generic;
using System.Text;

namespace OpenKh.Tools.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class PoweredByAttribute : Attribute
    {
        public PoweredByAttribute(string value)
        {
            this.PoweredBy = value;
        }

        public string PoweredBy { get; set; }
    }
}
