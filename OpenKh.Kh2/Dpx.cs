using OpenKh.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xe.BinaryMapper;

namespace OpenKh.Kh2
{
    public partial class Dpx
    {
        public const uint Version = 0x82U;

        public int Unk04 { get; set; }
        public int Unk08 { get; set; }
        public List<Entry> Entries { get; set; }
        public byte[] Trail { get; set; }
        public List<Dpd> DpdList { get; set; }

        public class Header
        {
            [Data] public int Version { get; set; }
            [Data] public int Unk04 { get; set; }
            [Data] public int Unk08 { get; set; }
            [Data] public int EntriesCount { get; set; }
        }

        public class Entry
        {
            [Data] public int DpdOffset { get; set; }
            [Data] public int Index { get; set; }
            [Data] public int Id { get; set; }
            [Data] public int Unk0C { get; set; }
            [Data] public int Unk10 { get; set; }
            [Data] public int Unk14 { get; set; }
            [Data] public int Unk18 { get; set; }
            [Data] public int Unk1C { get; set; }

            public int DpdIndex { get; set; }

            public static Entry Read(Stream stream)
            {
                return BinaryMapping.ReadObject<Entry>(stream);
            }

            public static void Write(Stream stream, Entry source)
            {
                BinaryMapping.WriteObject<Entry>(stream, source);
            }
        }

        public static Dpx Read(Stream stream)
        {
            if (!stream.CanRead || !stream.CanSeek)
            {
                throw new InvalidDataException($"Read and seek must be supported.");
            }

            var offsetBase = stream.Position;

            var header = BinaryMapping.ReadObject<Header>(stream);

            if (header.Version != Version)
            {
                throw new InvalidDataException("Invalid header");
            }

            var entries = Enumerable.Range(0, header.EntriesCount)
                .Select(_ => Entry.Read(stream))
                .ToList();

            var trail = stream.ReadBytes(128);

            var dpdList = new List<Dpd>();

            entries
                .GroupBy(entry => entry.DpdOffset)
                .ForEach(
                    group =>
                    {
                        stream.Position = offsetBase + group.Key;

                        var dpd = Dpd.Read(stream);
                        var entryIndex = dpdList.Count;
                        dpdList.Add(dpd);

                        group
                            .ForEach(
                                entry =>
                                {
                                    entry.DpdIndex = entryIndex;
                                }
                            );
                    }
                );

            return new Dpx
            {
                Unk04 = header.Unk04,
                Unk08 = header.Unk08,
                Entries = entries,
                Trail = trail,
                DpdList = dpdList,
            };
        }
    }
}
