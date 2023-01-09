using OpenKh.Common;
using System;
using System.Drawing;
using System.IO;
using Xe.BinaryMapper;

namespace OpenKh.Kh2
{
    public partial class Dpd
    {
        public class Texture
        {
            [Data] public short Unk00 { get; set; }
            [Data] public short Unk02 { get; set; }
            [Data] public short Unk04 { get; set; }
            [Data] public short Format { get; set; }
            [Data] public short Unk08 { get; set; }
            [Data] public short Unk0a { get; set; }
            [Data] public short Width { get; set; }
            [Data] public short Height { get; set; }
            [Data] public short Unk10 { get; set; }
            [Data] public short Unk12 { get; set; }
            [Data] public short Unk14 { get; set; }
            [Data] public short Unk16 { get; set; }
            [Data] public int Unk18 { get; set; }
            [Data] public int Unk1c { get; set; }

            public override string ToString() => $"{Width} x {Height} ({(Imaging.Tm2.GsPSM)Format})";

            public Size Size => new Size(Width, Height);

            public byte[] Data { get; set; }
            public byte[] Palette { get; set; }

            public static Texture Read(Stream stream)
            {
                var entity = BinaryMapping.ReadObject<Texture>(stream);

                entity.Data = stream.ReadBytes(entity.Width * entity.Height);
                entity.Palette = stream.ReadBytes(0x100 * sizeof(int));

                return entity;
            }

            public static void Write(Stream stream, Texture entity)
            {
                BinaryMapping.WriteObject<Texture>(stream, entity);

                stream.Write(entity.Data);
                stream.Write(entity.Palette);
            }

            public byte[] GetBitmap()
            {
                var swizzled = 0;

                switch (Format)
                {
                    case 0x13:
                        return GetBitmapFrom8bpp(
                            swizzled == 7 ? Ps2.Decode8(Ps2.Encode32(Data, Size.Width / 128, Size.Height / 64), Size.Width / 128, Size.Height / 64) : Data
                            , Palette, Size.Width, Size.Height);
                    case 0x14:
                        return GetBitmapFrom4bpp(
                            swizzled == 7 ? Ps2.Decode4(Ps2.Encode32(Data, Size.Width / 128, Size.Height / 128), Size.Width / 128, Size.Height / 128) : Data
                            , Palette, Size.Width, Size.Height);
                    default:
                        throw new NotSupportedException($"The format {Format} is not supported.");
                }
            }

            private static byte[] GetBitmapFrom8bpp(byte[] src, byte[] palette, int width, int height)
            {
                var dst = new byte[width * height * 4];
                for (int i = 0; i < dst.Length; i += 4)
                {
                    var index = Ps2.Repl(src[i / 4]);
                    dst[i + 0] = (byte)Math.Max(0, palette[index * 4 + 2] * 2 - 1);
                    dst[i + 1] = (byte)Math.Max(0, palette[index * 4 + 1] * 2 - 1);
                    dst[i + 2] = (byte)Math.Max(0, palette[index * 4 + 0] * 2 - 1);
                    dst[i + 3] = (byte)Math.Max(0, palette[index * 4 + 3] * 2 - 1);
                }

                return dst;
            }

            private static byte[] GetBitmapFrom4bpp(byte[] src, byte[] palette, int width, int height)
            {
                var dst = new byte[width * height * 4];
                for (int i = 0; i < dst.Length; i += 8)
                {
                    var index = src[i / 8] & 0x0F;
                    dst[i + 0] = palette[index * 4 + 0];
                    dst[i + 1] = palette[index * 4 + 1];
                    dst[i + 2] = palette[index * 4 + 2];
                    dst[i + 3] = palette[index * 4 + 3];

                    index = src[i / 8] >> 4;
                    dst[i + 4] = palette[index * 4 + 0];
                    dst[i + 5] = palette[index * 4 + 1];
                    dst[i + 6] = palette[index * 4 + 2];
                    dst[i + 7] = palette[index * 4 + 3];
                }

                return dst;
            }
        }
    }
}
