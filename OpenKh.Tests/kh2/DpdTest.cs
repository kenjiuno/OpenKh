using OpenKh.Common;
using OpenKh.Kh2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace OpenKh.Tests.kh2
{
    public class DpdTest
    {
        private const string dpdFile = "kh2/res/bliz_0.dpd";

        [Fact]
        public void Bliz_0()
        {
            var dpd = File.OpenRead(dpdFile).Using(Dpd.Read);
            Assert.Equal(7, dpd.EffectsGroups.Count);
            Assert.Equal(1, dpd.Textures.Count);
        }
    }
}
