using OpenKh.Kh2;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace OpenKh.Tests.kh2
{
    public class BarTests : Common
    {
        private const string FilePath =
            @"D:\Hacking\KH2\export\obj\G_OA300.mdlx";

        [Fact]
        public void IsValidTest()
        {
            using (var stream = new MemoryStream())
            {
                stream.WriteByte(0x42);
                stream.WriteByte(0x41);
                stream.WriteByte(0x52);
                stream.WriteByte(0x01);
                stream.Position = 0;
                Assert.True(Bar.IsValid(stream));
            }
        }

        [Fact]
        public void IsNotValidTest()
        {
            using (var stream = new MemoryStream())
            {
                stream.WriteByte(1);
                stream.WriteByte(2);
                stream.WriteByte(3);
                stream.WriteByte(4);
                stream.Position = 0;
                Assert.False(Bar.IsValid(stream));
            }
        }

        [Fact]
        public void IsReadingEntryCountCorrect() =>
            FileOpenRead(FilePath, stream =>
            {
                var entries = Bar.Read(stream);
                Assert.Equal(7, entries.Count());
            });

        [Fact]
        public void IsReadingEntryNamesCorrectly() =>
            FileOpenRead(FilePath, stream =>
            {
                var entries = Bar.Read(stream);
                Assert.Equal("s_da", entries[0].Name);
                Assert.Equal("m_da", entries[1].Name);
                Assert.Equal("l_da", entries[2].Name);
                Assert.Equal("thru", entries[3].Name);
                Assert.Equal("tack", entries[4].Name);
                Assert.Equal("blow", entries[5].Name);
                Assert.Equal("game", entries[6].Name);
            });

        [Fact]
        public void IsReadingEntrySizesCorrectly() =>
            FileOpenRead(FilePath, stream =>
            {
                var entries = Bar.Read(stream);
                Assert.Equal(40, entries[0].Stream.Length);
                Assert.Equal(40, entries[1].Stream.Length);
                Assert.Equal(48, entries[2].Stream.Length);
                Assert.Equal(40, entries[3].Stream.Length);
                Assert.Equal(56, entries[4].Stream.Length);
                Assert.Equal(64, entries[5].Stream.Length);
                Assert.Equal(56, entries[6].Stream.Length);
            });

        [Fact]
        public void IsReadingEntryTypesCorrectly() =>
            FileOpenRead(FilePath, stream =>
            {
                var entries = Bar.Read(stream);
                Assert.Equal(Bar.EntryType.Vibration, entries[0].Type);
                Assert.Equal(Bar.EntryType.Vibration, entries[1].Type);
                Assert.Equal(Bar.EntryType.Vibration, entries[2].Type);
                Assert.Equal(Bar.EntryType.Vibration, entries[3].Type);
                Assert.Equal(Bar.EntryType.Vibration, entries[4].Type);
                Assert.Equal(Bar.EntryType.Vibration, entries[5].Type);
                Assert.Equal(Bar.EntryType.Vibration, entries[6].Type);
            });

        [Fact]
        public void IsReadingEntryIndexesCorrectly() =>
            FileOpenRead(FilePath, stream =>
            {
                var entries = Bar.Read(stream);
                Assert.Equal(0, entries[0].Index);
                Assert.Equal(0, entries[1].Index);
                Assert.Equal(0, entries[2].Index);
                Assert.Equal(0, entries[3].Index);
                Assert.Equal(0, entries[4].Index);
                Assert.Equal(0, entries[5].Index);
                Assert.Equal(0, entries[6].Index);
            });

        [Fact]
        public void IsReadingEntryDataCorrectly() =>
            FileOpenRead(FilePath, stream =>
            {
                var entries = Bar.Read(stream);
                Assert.Equal(86, entries[0].Stream.ReadByte());
                Assert.Equal(86, entries[1].Stream.ReadByte());
                Assert.Equal(86, entries[2].Stream.ReadByte());
                Assert.Equal(86, entries[3].Stream.ReadByte());
                Assert.Equal(86, entries[4].Stream.ReadByte());
                Assert.Equal(86, entries[5].Stream.ReadByte());
                Assert.Equal(86, entries[6].Stream.ReadByte());
            });

        [Fact]
        public void IsWritingBackCorrectly() =>
            FileOpenRead(FilePath, stream =>
            {
                var expectedData = new byte[stream.Length];
                stream.Read(expectedData, 0, expectedData.Length);

                var entries = Bar.Read(new MemoryStream(expectedData));
                using (var dstStream = new MemoryStream(expectedData.Length))
                {
                    Bar.Write(dstStream, entries);
                    dstStream.Position = 0;
                    var actualData = new byte[dstStream.Length];
                    dstStream.Read(actualData, 0, actualData.Length);

                    dstStream.Dump("D:/03system_openkh.bin");

                    Assert.Equal(expectedData.Length, actualData.Length);
                    Assert.Equal(expectedData, actualData);
                }
            });

        [Fact]
        public void ShouldAlignContentOnWrite()
        {
            var dstStream = new MemoryStream();
            Bar.Write(dstStream, new Bar.Entry[]
            {
                new Bar.Entry
                {
                    Name = "Test",
                    Stream = new MemoryStream(new byte[] { 0xcc, 0xcc, 0xcc, 0xcc, 0xcc })
                },
                new Bar.Entry
                {
                    Name = "Algn",
                    Stream = new MemoryStream(new byte[] { 0xde, 0xad, 0xbe, 0xef })
                }
            });

            var content = dstStream.ToArray();
            Assert.Equal(0x30, content[0x18]);
            Assert.Equal(5, content[0x1c]);
            Assert.Equal(0x40, content[0x28]);
            Assert.Equal(4, content[0x2c]);
        }

        [Theory]
        [InlineData("TEST", "TEST")]
        [InlineData("333", "333")]
        [InlineData("22", "22")]
        [InlineData("1", "1")]
        [InlineData("666666", "6666")]
        public void AlwaysReadCorrectName(string name, string expectedName)
        {
            var entries = new List<Bar.Entry>
            {
                new Bar.Entry
                {
                    Type = Bar.EntryType.Dummy,
                    Index = 0,
                    Name = name,
                    Stream = new MemoryStream()
                }
            };

            using (var tmpStream = new MemoryStream())
            {
                Bar.Write(tmpStream, entries);
                tmpStream.Position = 0;
                entries = Bar.Read(tmpStream);
            }

            Assert.Equal(expectedName, entries[0].Name);
        }
    }
}
