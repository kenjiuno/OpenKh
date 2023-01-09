using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OpenKh.Command.MapGen.Models.MapGenConfig;
using Xunit;
using System.IO;
using OpenKh.Kh2;
using OpenKh.Common;

namespace OpenKh.Tests.kh2
{
    public class DpxTest
    {
        private const string dpxFile = "kh2/res/bliz_0.dpx";

        [Fact]
        public void Bliz_0()
        {
            var dpx = File.OpenRead(dpxFile).Using(Dpx.Read);
            Assert.Equal(7, dpx.Entries.Count);
            Assert.Equal(0, dpx.Entries[0].Index);
            Assert.Equal(0, dpx.Entries[0].Id);
            Assert.Equal(1, dpx.Entries[1].Index);
            Assert.Equal(1, dpx.Entries[1].Id);
            Assert.Equal(2, dpx.Entries[2].Index);
            Assert.Equal(2, dpx.Entries[2].Id);
            Assert.Equal(3, dpx.Entries[3].Index);
            Assert.Equal(3, dpx.Entries[3].Id);
            Assert.Equal(4, dpx.Entries[4].Index);
            Assert.Equal(4, dpx.Entries[4].Id);
            Assert.Equal(5, dpx.Entries[5].Index);
            Assert.Equal(5, dpx.Entries[5].Id);
            Assert.Equal(6, dpx.Entries[6].Index);
            Assert.Equal(6, dpx.Entries[6].Id);
            Assert.Equal(1, dpx.DpdList.Count);
            Assert.Equal(0, dpx.Unk04);
            Assert.Equal(0, dpx.Unk08);
        }

    }
}
