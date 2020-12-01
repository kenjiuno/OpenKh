using System;
using System.Collections.Generic;
using System.Text;

namespace OpenKh.Tools.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class BuildVersionAttribute : Attribute
    {
        public BuildVersionAttribute(string value)
        {
            this.BuildVersion = value;
        }

        public string BuildVersion { get; set; }
    }
}
