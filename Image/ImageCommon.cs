using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Cetera
{
    class ImageCommon
    {
        static int npo2(int n) => 2 << (int)Math.Log(n - 1, 2); // next power of 2

        public enum Format : byte
        {
            L8, A8, LA44, LA88, HL88,
            RGB565, RGB888, RGBA5551,
            RGBA4444, RGBA8888,
            ETC1, ETC1_A4, L4, A4
        }

        public enum Swizzle : byte
        {
            Default, XiSwizzle = 1,
            Rotate90 = 4, Transpose = 8
        }

        public static IEnumerable<Color> GetColorsFromTexture(byte[] tex, Format format)
        {
            using (var br = new BinaryReader(new MemoryStream(tex)))
            {
                int? nibble = null;
                Func<int> ReadNibble = () =>
                {
                    int val;
                    if (nibble == null)
                    {
                        val = br.ReadByte();
                        nibble = val / 16;
                        val %= 16;
                    }
                    else
                    {
                        val = nibble.Value;
                        nibble = null;
                    }
                    return val;
                };

                var etc1colors = new Queue<Color>();

                while (br.BaseStream.Position != br.BaseStream.Length)
                {
                    int a = 255, r = 255, g = 255, b = 255;
                    switch (format)
                    {
                        case Format.L8:
                            b = g = r = br.ReadByte();
                            break;
                        case Format.A8:
                            a = br.ReadByte();
                            break;
                        case Format.LA44:
                            a = ReadNibble() * 17;
                            b = g = r = ReadNibble() * 17;
                            break;
                        case Format.LA88:
                            a = br.ReadByte();
                            b = g = r = br.ReadByte();
                            break;
                        case Format.HL88:
                            g = br.ReadByte();
                            r = br.ReadByte();
                            break;
                        case Format.RGB565:
                            var s = br.ReadUInt16();
                            b = (s % 32) * 33 / 4;
                            g = (s >> 5) % 64 * 65 / 16;
                            r = (s >> 11) * 33 / 4;
                            break;
                        case Format.RGB888:
                            b = br.ReadByte();
                            g = br.ReadByte();
                            r = br.ReadByte();
                            break;
                        case Format.RGBA5551:
                            var s2 = br.ReadUInt16();
                            a = (s2 & 1) * 255;
                            b = (s2 >> 1) % 32 * 33 / 4;
                            g = (s2 >> 6) % 32 * 33 / 4;
                            r = (s2 >> 11) % 32 * 33 / 4;
                            break;
                        case Format.RGBA4444:
                            a = ReadNibble() * 17;
                            b = ReadNibble() * 17;
                            g = ReadNibble() * 17;
                            r = ReadNibble() * 17;
                            break;
                        case Format.RGBA8888:
                            a = br.ReadByte();
                            b = br.ReadByte();
                            g = br.ReadByte();
                            r = br.ReadByte();
                            break;
                        case Format.ETC1:
                        case Format.ETC1_A4:
                            if (etc1colors.Count == 0)
                            {
                                var etc1alpha = (format == Format.ETC1_A4) ? br.ReadUInt64() : ulong.MaxValue;
                                etc1colors = new Queue<Color>(RgEtc1.Unpack(br.ReadBytes(8), etc1alpha));
                            }
                            yield return etc1colors.Dequeue();
                            continue;
                        case Format.L4:
                            b = g = r = ReadNibble() * 17;
                            break;
                        case Format.A4:
                            a = ReadNibble() * 17;
                            break;
                        default:
                            throw new NotSupportedException($"Unknown image format {format}");
                    }
                    yield return Color.FromArgb(a, r, g, b);
                }
            }
        }

        public static Bitmap LoadImage(IEnumerable<Color> colors, int width, int height, Swizzle swizzle, bool padToPowerOf2)
        {
            var bmp = new Bitmap(width, height);

            int stride = (int)swizzle < 4 ? width : height;
            if (padToPowerOf2) stride = npo2(stride);
            if (stride < 8) stride = 8;

            int i = 0;
            foreach (var color in colors)
            {
                int x, y;
                switch (swizzle)
                {
                    case Swizzle.Default:
                        x = (i / 64 % (stride / 8)) * 8 + (i / 4 & 4) | (i / 2 & 2) | (i & 1);
                        y = (i / 64 / (stride / 8)) * 8 + (i / 8 & 4) | (i / 4 & 2) | (i / 2 & 1);
                        break;
                    case Swizzle.XiSwizzle:
                        x = (i / 64 % (stride / 8)) * 8 + (i / 8 & 4) | (i / 4 & 2) | (i / 2 & 1);
                        y = (i / 64 / (stride / 8)) * 8 + (i / 4 & 4) | (i / 2 & 2) | (i & 1);
                        break;
                    case Swizzle.Rotate90:
                        x = (i / 64 / (stride / 8)) * 8 + (i / 8 & 4) | (i / 4 & 2) | (i / 2 & 1);
                        y = (i / 64 % (stride / 8)) * 8 + (i / 4 & 4) | (i / 2 & 2) | (i & 1);
                        y = stride - 1 - y;
                        break;
                    case Swizzle.Transpose:
                        x = (i / 64 / (stride / 8)) * 8 + (i / 8 & 4) | (i / 4 & 2) | (i / 2 & 1);
                        y = (i / 64 % (stride / 8)) * 8 + (i / 4 & 4) | (i / 2 & 2) | (i & 1);
                        break;
                    default:
                        throw new NotSupportedException($"Unknown swizzle format {swizzle}");
                }
                if (0 <= x && x < width && 0 <= y && y < height)
                {
                    bmp.SetPixel(x, y, color);
                }

                i++;
            }
            return bmp;
        }
        
        public static byte[] ToTexture(Bitmap bmp, Format format, Swizzle swizzle)
        {
            throw new NotSupportedException("Need to make changes to some swizzle stuff");
            var ms = new MemoryStream();
            int width = bmp.Width, height = bmp.Height;
            int stride = Math.Max(8, npo2(height));

            var etc1colors = new Queue<Color>();

            using (var bw = new BinaryWriter(ms))
            {
                int? nibble = null;
                Action<int> WriteNibble = val =>
                {
                    val &= 15;
                    if (nibble == null)
                    {
                        nibble = val;
                    }
                    else
                    {
                        bw.Write((byte)(nibble.Value + 16 * val));
                        nibble = null;
                    }
                };

                for (int i = 0; i < ((width + 7) & ~7) * stride; i++)
                {
                    int x = (i / 64 / (stride / 8)) * 8 + (i / 8 & 4) | (i / 4 & 2) | (i / 2 & 1);
                    int y = (i / 64 % (stride / 8)) * 8 + (i / 4 & 4) | (i / 2 & 2) | (i & 1);
                    if (swizzle == Swizzle.Rotate90)
                    {
                        y = stride - 1 - y;
                    }

                    x = Math.Min(x, bmp.Width - 1);
                    y = Math.Max(0, Math.Min(y, bmp.Height - 1));
                    var color = bmp.GetPixel(x, y);
                    if (color.A == 0) color = default(Color);

                    switch (format)
                    {
                        case Format.L8:
                            bw.Write((byte)((color.R + color.G + color.B) / 3));
                            break;
                        case Format.A8:
                            bw.Write(color.A);
                            break;
                        case Format.LA44:
                            WriteNibble(color.A / 16);
                            WriteNibble((color.R + color.G + color.B) / 48);
                            break;
                        case Format.LA88:
                            bw.Write(color.A);
                            bw.Write((byte)((color.R + color.G + color.B) / 3));
                            break;
                        case Format.HL88:
                            bw.Write(color.G);
                            bw.Write(color.B);
                            break;
                        case Format.RGB565:
                            bw.Write((short)((color.R / 8 << 11) | (color.G / 4 << 5) | (color.B / 8)));
                            break;
                        case Format.RGB888:
                            bw.Write(color.B);
                            bw.Write(color.G);
                            bw.Write(color.R);
                            break;
                        case Format.RGBA5551:
                            bw.Write((short)((color.R / 8 << 11) | (color.G / 8 << 6) | (color.B / 8 << 1) | color.A / 128));
                            break;
                        case Format.RGBA4444:
                            WriteNibble(color.A / 16);
                            WriteNibble(color.B / 16);
                            WriteNibble(color.G / 16);
                            WriteNibble(color.R / 16);
                            break;
                        case Format.RGBA8888:
                            bw.Write(color.A);
                            bw.Write(color.B);
                            bw.Write(color.G);
                            bw.Write(color.R);
                            break;
                        case Format.ETC1:
                        case Format.ETC1_A4:
                            etc1colors.Enqueue(color);
                            if (etc1colors.Count != 16) continue;

                            ulong alpha;
                            var packed = RgEtc1.Pack(etc1colors.ToList(), out alpha);
                            if (format == Format.ETC1_A4) bw.Write(alpha);
                            bw.Write(packed);
                            etc1colors.Clear();

                            break;
                        case Format.L4:
                            WriteNibble((color.R + color.G + color.B) / 48);
                            break;
                        case Format.A4:
                            WriteNibble(color.A / 3);
                            break;
                        default:
                            throw new NotSupportedException();
                    }
                }
            }

            return ms.ToArray();
        }
    }
}
