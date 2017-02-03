using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;

namespace Cetera.Image
{
    class Etc1
    {
        [DllImport("rg_etc1.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int pack_etc1_block(byte[] pETC1_block, int[] pSrc_pixels_rgba, int[] pack_params);

        [DllImport("rg_etc1.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern bool unpack_etc1_block(byte[] pETC1_block, int[] pDst_pixels_rgba, bool preserve_alpha);

        [DllImport("rg_etc1.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern bool pack_etc1_block_init();

        static int Transpose(int n) => (n % 4) * 4 + n / 4;
        static readonly int[] order3ds = { 0, 4, 1, 5, 8, 12, 9, 13, 2, 6, 3, 7, 10, 14, 11, 15 };
        static int[] DefaultPackParams = new[] { 2, 0 }; // high quality with no dithering

        static int[][] modifiers =
        {
            new[] { 2, 8, -2, -8 },
            new[] { 5, 17, -5, -17 },
            new[] { 9, 29, -9, -29 },
            new[] { 13, 42, -13, -42 },
            new[] { 18, 60, -18, -60 },
            new[] { 24, 80, -24, -80 },
            new[] { 33, 106, -33, -106 },
            new[] { 47, 183, -47, -183 }
        };

        static Etc1()
        {
            pack_etc1_block_init();
        }

        public struct PixelData
        {
            public ulong Alpha { get; set; }
            public ulong Color { get; set; }
        }

        static int Clamp(int n) => Math.Max(0, Math.Min(n, 255));
        static int Sign3(int n) => ((n + 4) % 8) - 4;
        static int Extend5to8(int n) => (n << 3) | (n >> 2);
        static int Square(int n) => n * n;

        static Color AddColor(Color basec, int diff, int alpha = 255)
        {
            return Color.FromArgb(alpha, Clamp(basec.R + diff), Clamp(basec.G + diff), Clamp(basec.B + diff));
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
                    unpack_etc1_block(BitConverter.GetBytes(data.Color).Reverse().ToArray(), unpacked, false);
                    foreach (int i in order3ds)
                    {
                        var rgba = BitConverter.GetBytes(unpacked[Transpose(i)]);
                        queue.Enqueue(Color.FromArgb((byte)(data.Alpha >> (4 * i)) % 16 * 17, rgba[0], rgba[1], rgba[2]));
                    }
                }
                return queue.Dequeue();
            }

            public Color Get2(Func<PixelData> func)
            {
                if (!queue.Any())
                {
                    var data = func();
                    Func<int, int, int> GetBits = (offset, count) => ((int)(data.Color >> offset) & ((1 << count) - 1));

                    Color basec0, basec1;
                    int diffbit = GetBits(33, 1);
                    int flipbit = GetBits(32, 1);
                    int flipbitmask = flipbit == 0 ? 8 : 2;
                    var mod0 = modifiers[GetBits(37, 3)];
                    var mod1 = modifiers[GetBits(34, 3)];

                    if (diffbit == 0)
                    {
                        basec0 = Color.FromArgb(GetBits(60, 4) * 16, GetBits(52, 4) * 16, GetBits(44, 4) * 16);
                        basec1 = Color.FromArgb(GetBits(56, 4) * 16, GetBits(48, 4) * 16, GetBits(40, 4) * 16);
                    }
                    else
                    {
                        int r1 = GetBits(59, 5), g1 = GetBits(51, 5), b1 = GetBits(43, 5);
                        int r2 = r1 + Sign3(GetBits(56, 3)), g2 = g1 + Sign3(GetBits(48, 3)), b2 = b1 + Sign3(GetBits(40, 3));
                        basec0 = Color.FromArgb(Extend5to8(r1), Extend5to8(g1), Extend5to8(b1));
                        basec1 = Color.FromArgb(Extend5to8(r2), Extend5to8(g2), Extend5to8(b2));
                    }

                    foreach (int i in order3ds)
                    {
                        var basec = (i & flipbitmask) == 0 ? basec0 : basec1;
                        var mod = (i & flipbitmask) == 0 ? mod0 : mod1;
                        int sgn = GetBits(i + 16, 1) == 1 ? -1 : 1;
                        int diff = mod[GetBits(i, 1)];
                        queue.Enqueue(AddColor(basec, sgn * diff, (byte)(data.Alpha >> (4 * i)) % 16 * 17));
                    }
                }
                return queue.Dequeue();
            }
        }

        class BareBones
        {
            static int Diff(Color c1, Color c2)
            {
                return 2 * Square(c1.R - c2.R) + 4 * Square(c1.G - c2.G) + 3 * Square(c1.B - c2.B);
            }

            // listColors of size 8
            static int BestDiff(Color basec, List<Color> listColors)
            {
                return modifiers.Min(mod => listColors.Sum(desired => mod.Min(diff => Diff(desired, AddColor(basec, diff)))));
            }

            public static ulong Encode(List<Color> colors, out ulong alpha)
            {
                colors = Enumerable.Range(0, 16).Select(j => colors[order3ds[order3ds[order3ds[j]]]]).ToList(); // back to a sensible order
                alpha = colors.Reverse<Color>().Aggregate(0ul, (a, b) => (a * 16) | (byte)(b.A / 16));

                var topColors = colors.Where((c, i) => (i & 2) == 0).ToList();
                var bottomColors = colors.Where((c, i) => (i & 2) != 0).ToList();
                var leftColors = colors.Where((c, i) => (i & 8) == 0).ToList();
                var rightColors = colors.Where((c, i) => (i & 8) != 0).ToList();

                var leftAvg = Color.FromArgb((int)leftColors.Average(c => c.R), (int)leftColors.Average(c => c.G), (int)leftColors.Average(c => c.B));
                var rightAvg = Color.FromArgb((int)rightColors.Average(c => c.R), (int)rightColors.Average(c => c.G), (int)rightColors.Average(c => c.B));
                //var stp = System.Diagnostics.Stopwatch.StartNew();
                //var ans = All4BitColors.Min(c => BestDiff(c, topColors));
                //stp.Stop();
                //System.Windows.Forms.MessageBox.Show(stp.Elapsed.ToString());
                int k = 1;

                // pick diffbit depending on whether the average differs by much

                // foreach flipbit
                //    foreach diffbit
                ulong data = 0;
                Action<int, int, int> SetBits = (offset, count, value) => data = (data & ~(((1ul << count) - 1) << offset)) | ((ulong)value << offset);
                SetBits(60, 4, leftAvg.R / 16);
                SetBits(56, 4, rightAvg.R / 16);
                SetBits(52, 4, leftAvg.G / 16);
                SetBits(48, 4, rightAvg.G / 16);
                SetBits(44, 4, leftAvg.B / 16);
                SetBits(40, 4, rightAvg.B / 16);

                return data;
            }
        }

        public class Encoder
        {
            Queue<Color> queue = new Queue<Color>();

            static ulong Encode(List<Color> colors, out ulong alpha)
            {
                colors = Enumerable.Range(0, 16).Select(j => colors[order3ds[order3ds[order3ds[j]]]]).ToList();
                alpha = colors.Reverse<Color>().Aggregate(0ul, (a, b) => (a * 16) | (byte)(b.A / 16));

                var colorArray = new int[16];
                for (int i = 0; i < 16; i++)
                {
                    var color = colors[Transpose(i)];
                    colorArray[i] = BitConverter.ToInt32(new byte[] { color.R, color.G, color.B, 255 }, 0);
                }

                var packed = new byte[8];
                pack_etc1_block(packed, colorArray, DefaultPackParams);

                return BitConverter.ToUInt64(packed.Reverse().ToArray(), 0);
            }

            public void Set(Color c, Action<PixelData> func)
            {
                queue.Enqueue(c);
                if (queue.Count == 16)
                {
                    ulong alpha;
                    //var data = BareBones.Encode(queue.ToList(), out alpha);
                    var data = Encode(queue.ToList(), out alpha);

                    func(new PixelData { Alpha = alpha, Color = data });
                    queue.Clear();
                }
            }
        }
    }
}
