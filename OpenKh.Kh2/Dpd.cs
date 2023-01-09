using OpenKh.Common;
using OpenKh.Kh2.Utils;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Xe.BinaryMapper;
using Xe.IO;

namespace OpenKh.Kh2
{
    public partial class Dpd
    {
        private const uint Version = 0x00000096U;

        public List<Texture> Textures { get; set; }
        public List<EffectsGroup> EffectsGroups { get; set; }

        public override string ToString() => $"{EffectsGroups.Count} EffectsGroups, {Textures.Count} Textures";

        public class EffectsGroupHeader
        {
            [Data(Count = 8)] public Vector4[] Coords { get; set; }
            [Data] public Vector4 Position { get; set; }
            [Data] public Vector4 Rotation { get; set; }
            [Data] public Vector4 Scaling { get; set; }
            [Data(Count = 96)] public byte[] Unk { get; set; }
        }

        public class EffectsGroupTop
        {
            [Data] public int Unk00 { get; set; }
            [Data] public int Unk04 { get; set; }
            [Data] public int UnkOff8 { get; set; }
            [Data] public int UnkOffC { get; set; }
        }

        public class EffectsGroup
        {
            public EffectsGroupHeader Header { get; set; }
            public EffectsGroupTop Top { get; set; }

            public List<DpdEffect> Effects { get; set; }
        }

        public class DpdEffectHeader
        {
            [Data] public uint OffsetNext { get; set; }
            [Data] public uint Unk04 { get; set; }
            [Data] public uint Unk08 { get; set; }
            [Data] public uint Unk0C { get; set; }
            [Data] public uint Unk10 { get; set; }
            [Data] public uint Unk14 { get; set; }
            [Data] public uint Unk18 { get; set; }
            [Data] public uint Unk1C { get; set; }
            [Data] public uint Unk20 { get; set; }
            [Data] public ushort Unk24 { get; set; }
            [Data] public ushort CommandsCount { get; set; }
        }

        public class DpdEffect
        {
            public DpdEffectHeader Header { get; set; }

            public List<DpdEffectCommand> Commands { get; set; }
        }

        public class DpdEffectCommand
        {
            [Data] public EffectCommand Command { get; set; }
            [Data] public ushort ParamLength { get; set; }
            [Data] public ushort ParamCount { get; set; }
            [Data] public uint OffsetPrimary { get; set; }
            [Data] public uint OffsetSecondary { get; set; }

            public byte[] Primary { get; set; }
            public byte[] Secondary { get; set; }

            public override string ToString() => $"{Command} ({ParamLength} x {ParamCount}) Primary:{Primary?.Length ?? -1} Secondary:{Secondary?.Length ?? -1}";
        }

        private static readonly IBinaryMapping _mapping =
           MappingConfiguration.DefaultConfiguration()
               .ForTypeVector4()
               .Build();

        public static Dpd Read(Stream stream)
        {
            if (!stream.CanRead || !stream.CanSeek)
            {
                throw new InvalidDataException($"Read or seek must be supported.");
            }

            var offsetBase = stream.Position;

            var reader = new BinaryReader(stream);
            if (stream.Length < 16L || reader.ReadUInt32() != Version)
            {
                throw new InvalidDataException("Invalid header");
            }

            var offsetEffectsGroupList = ReadOffsetsList(reader);
            var offsetTextures = ReadOffsetsList(reader);
            var offsetTable3 = ReadOffsetsList(reader);
            var offsetTable4 = ReadOffsetsList(reader);
            var offsetTable5 = ReadOffsetsList(reader);

            var effectsGroups = new List<EffectsGroup>();

            var deferredReads = new List<(DpdEffectCommand, long)>();
            var pointBoundary = new PointBoundary();

            foreach (var offsetEffectsGroup in offsetEffectsGroupList)
            {
                stream.Position = offsetBase + offsetEffectsGroup;
                var effectsGroupHeader = _mapping.ReadObject<EffectsGroupHeader>(stream);

                var offsetDpdEffectBase = stream.Position;

                var top = BinaryMapping.ReadObject<EffectsGroupTop>(stream);

                pointBoundary.AddPoints(
                    (int)(offsetDpdEffectBase + top.UnkOff8), 
                    (int)(offsetDpdEffectBase + top.UnkOffC)
                );

                var dpdEffects = new List<DpdEffect>();

                var nextOffset = 16U;
                while (nextOffset != 0)
                {
                    stream.Position = offsetDpdEffectBase + nextOffset;
                    var dpdEffectHeader = BinaryMapping.ReadObject<DpdEffectHeader>(stream);

                    var dpdEffectCommands = Enumerable.Range(0, dpdEffectHeader.CommandsCount)
                        .Select(_ => BinaryMapping.ReadObject<DpdEffectCommand>(stream))
                        .ToList();

                    pointBoundary.AddPoints((int)(offsetDpdEffectBase + nextOffset));

                    dpdEffectCommands.ForEach(
                        it => pointBoundary.AddPoints(
                            (int)(offsetDpdEffectBase + it.OffsetPrimary),
                            (int)(offsetDpdEffectBase + it.OffsetSecondary)
                        )
                    );

                    foreach (var command in dpdEffectCommands)
                    {
                        deferredReads.Add((command, offsetDpdEffectBase + command.OffsetPrimary));
                    }

                    dpdEffects.Add(
                        new DpdEffect
                        {
                            Header = dpdEffectHeader,
                            Commands = dpdEffectCommands,
                        }
                    );

                    nextOffset = dpdEffectHeader.OffsetNext;
                }

                effectsGroups.Add(
                    new EffectsGroup
                    {
                        Header = effectsGroupHeader,
                        Top = top,
                        Effects = dpdEffects,
                    }
                );
            }

            foreach (var defer in deferredReads)
            {
                stream.Position = defer.Item2;

                var command = defer.Item1;

                command.Primary = stream.ReadBytes(
                    pointBoundary.GetLengthFromPoint((int)command.OffsetPrimary)
                );

                command.Secondary = stream.ReadBytes(
                    pointBoundary.GetLengthFromPoint((int)command.OffsetSecondary)
                );
            }

            var textures = offsetTextures
                .Select(offset => Texture.Read(stream.SetPosition(offsetBase + offset)))
                .ToList();

            return new Dpd
            {
                EffectsGroups = effectsGroups,
                Textures = textures,
            };
        }

        private static List<int> ReadOffsetsList(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            var list = new List<int>(count);

            for (int i = 0; i < count; i++)
            {
                list.Add(reader.ReadInt32());
            }

            return list;
        }

        private void WriteOffsetsList(BinaryWriter writer, List<int> list)
        {
            writer.Write(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                writer.Write(list[i]);
            }
        }

    }
}
