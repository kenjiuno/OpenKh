using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

namespace OpenKh.Tools.FunnyMapper.Extensions
{
    static class OldCollectionExtensions
    {
        public static StringCollection ToStringCollection(this IEnumerable<string> self)
        {
            var list = new StringCollection();
            list.AddRange(self.ToArray());
            return list;
        }
    }
}
