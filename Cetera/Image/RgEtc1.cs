using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;

namespace Cetera.Image
{
    // Fast, high quality ETC1 block packer/unpacker - Rich Geldreich <richgel99@gmail.com>
    // rg_etc1 is written in C; this RgEtc1 class is a C# wrapper to use it with 3DS games
    class Etc1
    {
        [DllImport("rg_etc1.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int pack_etc1_block(byte[] pETC1_block, int[] pSrc_pixels_rgba, int[] pack_params);

        [DllImport("rg_etc1.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern bool unpack_etc1_block(byte[] pETC1_block, int[] pDst_pixels_rgba, bool preserve_alpha);

        [DllImport("rg_etc1.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern bool pack_etc1_block_init();

        static int Transpose(int n) => (n % 4) * 4 + n / 4;
        static readonly int[] etc1_order = { 0, 4, 1, 5, 8, 12, 9, 13, 2, 6, 3, 7, 10, 14, 11, 15 };
        static int[] DefaultPackParams = new[] { 2, 0 }; // high quality with no dithering

        static Etc1()
        {
            pack_etc1_block_init();
        }

        public struct PixelData
        {
            public ulong Alpha { get; set; }
            public byte[] Color { get; set; }
        }

        public class Decoder
        {
            Queue<Color> queue = new Queue<Color>();

            public Color Get(Func<PixelData> func)
            {
                if (!queue.Any())
                {
                    var data = func();
                    var unpacked = new int[16];
                    unpack_etc1_block(data.Color.Reverse().ToArray(), unpacked, false);
                    foreach (int i in etc1_order)
                    {
                        var rgba = BitConverter.GetBytes(unpacked[Transpose(i)]);
                        queue.Enqueue(Color.FromArgb((byte)(data.Alpha >> (4 * i)) % 16 * 17, rgba[0], rgba[1], rgba[2]));
                    }
                }
                return queue.Dequeue();
            }
        }

        public class Encoder
        {
            Queue<Color> queue = new Queue<Color>();

            public void Set(Color c, Action<PixelData> func)
            {
                queue.Enqueue(c);
                if (queue.Count == 16)
                {
                    var src = queue.ToList();
                    var colors = Enumerable.Range(0, 16).Select(j => src[etc1_order[etc1_order[etc1_order[j]]]]).ToList();

                    var alpha = colors.Reverse<Color>().Aggregate(0ul, (a, b) => (a * 16) | (byte)(b.A / 16));

                    var colorArray = new int[16];
                    for (int i = 0; i < 16; i++)
                    {
                        var color = colors[Transpose(i)];
                        colorArray[i] = BitConverter.ToInt32(new byte[] { color.R, color.G, color.B, 255 }, 0);
                    }

                    var packed = new byte[8];
                    pack_etc1_block(packed, colorArray, DefaultPackParams);
                    queue.Clear();

                    func(new PixelData { Alpha = alpha, Color = packed.Reverse().ToArray() });
                }
            }
        }
    }
}
