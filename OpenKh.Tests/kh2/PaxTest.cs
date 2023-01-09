using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OpenKh.Command.MapGen.Models.MapGenConfig;
using Xunit;
using OpenKh.Kh2;
using System.IO;
using OpenKh.Common;

namespace OpenKh.Tests.kh2
{
    public class PaxTest
    {
        private const string paxFile = "kh2/res/bliz_0.pax";

        [Fact]
        public void Bliz_0()
        {
            var pax = File.OpenRead(paxFile).Using(Pax.Read);
            Assert.Equal(8, pax.Entries.Count);
            Assert.Equal("blizzard1", pax.Name);
            Assert.NotNull(pax.Dpx);
        }
    }
}
