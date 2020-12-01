using System;
using System.Collections.Generic;
using System.Text;

namespace OpenKh.Tools.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class AuthorAttribute : Attribute
    {
        public AuthorAttribute(string value)
        {
            this.Author = value;
        }

        public string Author { get; set; }
    }
}
