using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;

namespace Cetera
{
    class RgEtc1
    {
        [DllImport("rg_etc1.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int pack_etc1_block(byte[] pETC1_block, int[] pSrc_pixels_rgba, int[] pack_params);

        [DllImport("rg_etc1.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern bool unpack_etc1_block(byte[] pETC1_block, int[] pDst_pixels_rgba, bool preserve_alpha);

        [DllImport("rg_etc1.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern bool pack_etc1_block_init();

        static int Transpose(int n) => (n % 4) * 4 + n / 4;
        static readonly int[] etc1_order = { 0, 4, 1, 5, 8, 12, 9, 13, 2, 6, 3, 7, 10, 14, 11, 15 };

        static RgEtc1()
        {
            pack_etc1_block_init();
        }

        // Quality setting = the higher the quality, the slower.
        // To pack large textures, it is highly recommended to call Pack() in parallel, on different blocks, from multiple threads (particularly when using HighQuality).
        public enum Quality
        {
            LowQuality,
            MediumQuality,
            HighQuality
        }

        // Packs a 4x4 block of 32bpp RGBA pixels to an 8-byte ETC1 block.
        // 32-bit RGBA pixels must always be arranged as (R,G,B,A) (R first, A last) in memory, independent of platform endianness.
        // This function is thread safe, and does not dynamically allocate any memory.
        // Pack() does not currently support "perceptual" colorspace metrics - it primarily optimizes for RGB RMSE.
        public static byte[] Pack(List<Color> src, out ulong alpha, Quality quality = Quality.HighQuality)
        {
            var colors = Enumerable.Range(0, 16).Select(j => src[etc1_order[etc1_order[etc1_order[j]]]]).ToList();

            alpha = 0;
            foreach (var d in colors.Reverse<Color>())
            {
                alpha <<= 4;
                alpha |= (byte)(d.A / 16); // the two nibbles might be reversed? double check!
            };

            var colorArray = new int[16];
            for (int i = 0; i < 16; i++)
            {
                var color = colors[i];
                colorArray[Transpose(i)] = BitConverter.ToInt32(new byte[] { color.R, color.G, color.B, 255 }, 0);
            }

            var packed = new byte[8];
            pack_etc1_block(packed, colorArray, new[] { (int)quality, 0 });
            return packed.Reverse().ToArray();
        }

        // Unpacks an 8-byte ETC1 compressed block to a block of 4x4 32bpp RGBA pixels.
        // This function is thread safe, and does not dynamically allocate any memory.
        public static IEnumerable<Color> Unpack(byte[] pETC1_block, ulong alpha = ulong.MaxValue)
        {
            var unpacked = new int[16];
            unpack_etc1_block(pETC1_block.Reverse().ToArray(), unpacked, false);
            foreach (int i in etc1_order)
            {
                var rgba = BitConverter.GetBytes(unpacked[Transpose(i)]);
                yield return Color.FromArgb((byte)(alpha >> (4 * i)) % 16 * 17, rgba[0], rgba[1], rgba[2]);
            }
        }
    }
}
