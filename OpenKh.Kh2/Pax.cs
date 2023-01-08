using OpenKh.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xe.BinaryMapper;

namespace OpenKh.Kh2
{
    public partial class Pax
    {
        public string Name { get; set; }
        public List<Entry> Entries { get; set; }
        public Dpx Dpx { get; set; }

        public class Header
        {
            [Data] public int MagicCode { get; set; }
            [Data] public int OffsetName { get; set; }
            [Data] public int EntriesCount { get; set; }
            [Data] public int OffsetDpx { get; set; }
        }

        public class Entry
        {
            [Data] public ushort Effect { get; set; }
            [Data] public ushort Caster { get; set; }
            [Data] public ushort Unk04 { get; set; }
            [Data] public ushort Unk06 { get; set; }
            [Data] public int Unk08 { get; set; }
            [Data] public int Unk0C { get; set; }
            [Data] public int Unk10 { get; set; }
            [Data] public int Unk14 { get; set; }
            [Data] public int SoundEffect { get; set; }
            [Data] public float PosX { get; set; }
            [Data] public float PosZ { get; set; }
            [Data] public float PosY { get; set; }
            [Data] public float RotX { get; set; }
            [Data] public float RotZ { get; set; }
            [Data] public float RotY { get; set; }
            [Data] public float ScaleX { get; set; }
            [Data] public float ScaleZ { get; set; }
            [Data] public float ScaleY { get; set; }
            [Data] public int Unk40 { get; set; }
            [Data] public int Unk44 { get; set; }
            [Data] public int Unk48 { get; set; }
            [Data] public int Unk4C { get; set; }

            public static Entry Read(Stream stream)
            {
                return BinaryMapping.ReadObject<Entry>(stream);
            }

            public static void Write(Stream stream, Entry source)
            {
                BinaryMapping.WriteObject<Entry>(stream, source);
            }
        }

        public const uint MagicCode = 0x5F584150U;

        public static Pax Read(Stream stream)
        {
            if (!stream.CanRead || !stream.CanSeek)
            {
                throw new InvalidDataException($"Read and seek must be supported.");
            }

            var offsetBase = stream.Position;

            var header = BinaryMapping.ReadObject<Header>(stream);

            if (header.MagicCode != MagicCode)
            {
                throw new InvalidDataException("Invalid header");
            }

            var entries = Enumerable.Range(0, header.EntriesCount)
                .Select(_ => Entry.Read(stream))
                .ToList();

            stream.Position = offsetBase + header.OffsetName;
            var name = stream.ReadString(128, Encoding.Latin1);

            stream.Position = offsetBase + header.OffsetDpx;
            var dpx = Dpx.Read(stream);

            return new Pax
            {
                Entries = entries,
                Name = name,
            };
        }

        private void ReadDpx(BinaryReader reader)
        {
        }

        public void SaveChanges(Stream stream)
        {
            SaveChanges(new BinaryWriter(stream));
        }

        public void SaveChanges(BinaryWriter writer)
        {
            var offsetBase = writer.BaseStream.Position;

            writer.BaseStream.Position += 0x10;
            SaveEntries(writer);

            int offsetName = (int)(writer.BaseStream.Position - offsetBase);
            SaveName(writer);

            int offsetDpx = (int)(writer.BaseStream.Position - offsetBase);
            //Dpx.SaveChanges(writer);

            writer.BaseStream.Position = offsetBase;
            writer.Write(MagicCode);
            writer.Write(offsetName);
            writer.Write(Entries.Count);
            writer.Write(offsetDpx);
        }

        public void SaveEntries(BinaryWriter writer)
        {
            foreach (var entry in Entries)
            {
                entry.Save(writer);
            }
        }
    }
}
