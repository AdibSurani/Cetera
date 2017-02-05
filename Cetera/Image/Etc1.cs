using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;

namespace Cetera.Image
{
    static class Etc1Extensions
    {
        public static T MinBy<T>(this IEnumerable<T> src, Func<T, int> func)
        {
            var minArg = default(T);
            var minValue = int.MaxValue;

            foreach (var item in src)
            {
                var value = func(item);
                if (value == 0) return item;
                if (value.CompareTo(minValue) < 0)
                {
                    minArg = item;
                    minValue = value;
                }
            }

            return minArg;
        }
    }

    class Etc1
    {
        static readonly int[] order3ds = { 0, 4, 1, 5, 8, 12, 9, 13, 2, 6, 3, 7, 10, 14, 11, 15 };

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

        public struct RGB
        {
            public byte R, G, B, padding; // padding for speed reasons

            public RGB(int r, int g, int b)
            {
                R = (byte)r;
                G = (byte)g;
                B = (byte)b;
                padding = 0;
            }

            public static RGB operator +(RGB c, int mod) => new RGB(Clamp(c.R + mod), Clamp(c.G + mod), Clamp(c.B + mod));
            public static int operator -(RGB c1, RGB c2) => ErrorRGB(c1.R - c2.R, c1.G - c2.G, c1.B - c2.B);
            public static RGB Average(IEnumerable<RGB> src) => new RGB((int)src.Average(c => c.R), (int)src.Average(c => c.G), (int)src.Average(c => c.B));
            public RGB Scale(int limit) => limit == 16 ? new RGB(R * 17, G * 17, B * 17) : new RGB((R << 3) | (R >> 2), (G << 3) | (G >> 2), (B << 3) | (B >> 2));
            public RGB Unscale(int limit) => new RGB(R * limit / 256, G * limit / 256, B * limit / 256);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Block
        {
            public ushort LSB;
            public ushort MSB;
            byte flags;
            public byte B;
            public byte G;
            public byte R;

            public bool FlipBit
            {
                get { return (flags & 1) == 1; }
                set { flags = (byte)((flags & ~1) | (value ? 1 : 0)); }
            }
            public bool DiffBit
            {
                get { return (flags & 2) == 2; }
                set { flags = (byte)((flags & ~2) | (value ? 2 : 0)); }
            }
            public int ColorDepth => DiffBit ? 32 : 16;
            public int Table0
            {
                get { return (flags >> 5) & 7; }
                set { flags = (byte)((flags & ~(7 << 5)) | (value << 5)); }
            }
            public int Table1
            {
                get { return (flags >> 2) & 7; }
                set { flags = (byte)((flags & ~(7 << 2)) | (value << 2)); }
            }
            public int this[int i] => (MSB >> i) % 2 * 2 + (LSB >> i) % 2;

            public RGB Color0 => new RGB(R * ColorDepth / 256, G * ColorDepth / 256, B * ColorDepth / 256);

            public RGB Color1
            {
                get
                {
                    if (!DiffBit) return new RGB(R % 16, G % 16, B % 16);
                    var c0 = Color0;
                    int rd = Sign3(R % 8), gd = Sign3(G % 8), bd = Sign3(B % 8);
                    return new RGB(c0.R + rd, c0.G + gd, c0.B + bd);
                }
            }
        }

        public struct PixelData
        {
            public ulong Alpha { get; set; }
            public Block Block { get; set; }
        }

        static int Clamp(int n) => Math.Max(0, Math.Min(n, 255));
        static int Sign3(int n) => ((n + 4) % 8) - 4;
        static int Extend5to8(int n) => (n << 3) | (n >> 2);
        static int Square(int n) => n * n;
        //static int ErrorRGB(int r, int g, int b) => Square(r) + Square(g) + Square(b); // standard version used in rg_etc1
        static int ErrorRGB(int r, int g, int b) => 2 * Square(r) + 4 * Square(g) + 3 * Square(b); // human perception

        public class Decoder
        {
            Queue<Color> queue = new Queue<Color>();

            public Color Get(Func<PixelData> func)
            {
                if (!queue.Any())
                {
                    var data = func();
                    var basec0 = data.Block.Color0.Scale(data.Block.ColorDepth);
                    var basec1 = data.Block.Color1.Scale(data.Block.ColorDepth);

                    int flipbitmask = data.Block.FlipBit ? 2 : 8;
                    foreach (int i in order3ds)
                    {
                        var basec = (i & flipbitmask) == 0 ? basec0 : basec1;
                        var mod = modifiers[(i & flipbitmask) == 0 ? data.Block.Table0 : data.Block.Table1];
                        var c = basec + mod[data.Block[i]];
                        queue.Enqueue(Color.FromArgb((int)((data.Alpha >> (4 * i)) % 16 * 17), c.R, c.G, c.B));
                    }
                }
                return queue.Dequeue();
            }
        }

        public class Encoder
        {
            List<Color> queue = new List<Color>();

            public void Set(Color c, Action<PixelData> func)
            {
                queue.Add(c);
                if (queue.Count == 16)
                {
                    var colors = Enumerable.Range(0, 16).Select(j => queue[order3ds[order3ds[order3ds[j]]]]); // invert order3ds
                    var alpha = colors.Reverse().Aggregate(0ul, (a, b) => (a * 16) | (byte)(b.A / 16));
                    var data = Optimizer.Encode(colors.Select(c2 => new RGB(c2.R, c2.G, c2.B)).ToList());

                    func(new PixelData { Alpha = alpha, Block = data });
                    queue.Clear();
                }
            }
        }

        // Loosely based on rg_etc1
        class Optimizer
        {
            static int[] inverseLookup = (from limit in new[] { 16, 32 }
                                          from inten in modifiers
                                          from selector in inten
                                          from color in Enumerable.Range(0, 256)
                                          select Enumerable.Range(0, limit).Min(packed_c =>
                                          {
                                              int c = (limit == 32) ? (packed_c << 3) | (packed_c >> 2) : packed_c * 17;
                                              return (Math.Abs(Clamp(c + selector) - color) << 8) | packed_c;
                                          })).ToArray();

            const int MAX_ERROR = 99999999;

            public class SolutionSet : List<Solution>
            {
                public SolutionSet()
                {
                    Add(new Solution { error = MAX_ERROR });
                    Add(new Solution { error = MAX_ERROR });
                }
                public bool flip;
                public bool diff;
            }

            public struct Solution
            {
                public int error;
                public RGB blockColour;
                public int[] intenTable;
                public byte selectorMSB;
                public byte selectorLSB;
            }

            List<RGB> pixels;
            public RGB baseColor;
            int limit;

            public Optimizer(IEnumerable<RGB> pixels, int limit)
            {
                this.pixels = pixels.ToList();
                this.limit = limit;
                baseColor = RGB.Average(pixels).Unscale(limit);
            }

            public Solution Compute(params int[] deltas)
            {
                return (from zd in deltas
                        let z = zd + baseColor.B
                        where z >= 0 && z < limit
                        from yd in deltas
                        let y = yd + baseColor.G
                        where y >= 0 && y < limit
                        from xd in deltas
                        let x = xd + baseColor.R
                        where x >= 0 && x < limit
                        let c = new RGB(x, y, z).Scale(limit)
                        from t in modifiers
                        select EvaluateSolution(c, t))
                        .MinBy(soln => soln.error);
            }

            public Solution EvaluateSolution(RGB scaledColor, int[] intenTable)
            {
                var soln = new Solution { blockColour = scaledColor, intenTable = intenTable };
                var newTable = new RGB[4];
                for (int i = 0; i < 4; i++)
                    newTable[i] = scaledColor + intenTable[i];

                for (int i = 0; i < 8; i++)
                {
                    int best_j = 0, best_error = MAX_ERROR;
                    for (int j = 0; j < 4; j++)
                    {
                        int error = pixels[i] - newTable[j];
                        if (error < best_error)
                        {
                            best_error = error;
                            best_j = j;
                        }
                    }
                    soln.error += best_error;
                    soln.selectorMSB |= (byte)(best_j / 2 << i);
                    soln.selectorLSB |= (byte)(best_j % 2 << i);
                }
                return soln;
            }

            public static Block PackSolidColor(RGB c)
            {
                var soln = (from i in Enumerable.Range(0, 64)
                            let r = inverseLookup[i * 256 + c.R]
                            let g = inverseLookup[i * 256 + c.G]
                            let b = inverseLookup[i * 256 + c.B]
                            let table = inverseLookup[i]
                            let error = ErrorRGB(r >> 8, g >> 8, b >> 8)
                            let blockColour = new RGB(r, g, b)
                            select new Solution { error = error, blockColour = blockColour, selectorMSB = (byte)i })
                            .MinBy(s => s.error);

                int val = soln.selectorMSB;
                int multiplier = (val & 32) == 32 ? 8 : 17;
                return new Block
                {
                    DiffBit = (val & 32) == 32,
                    Table0 = (val >> 2) & 7,
                    Table1 = (val >> 2) & 7,
                    MSB = (ushort)((val & 2) != 0 ? 0xFFFF : 0),
                    LSB = (ushort)((val & 1) != 0 ? 0xFFFF : 0),
                    R = (byte)(soln.blockColour.R * multiplier),
                    G = (byte)(soln.blockColour.G * multiplier),
                    B = (byte)(soln.blockColour.B * multiplier),
                };
            }

            public static Block Encode(List<RGB> colors)
            {
                if (colors.Distinct().Count() == 1)
                {
                    return PackSolidColor(colors[0]);
                }

                SolutionSet bestsolns = null;
                int best_error = MAX_ERROR;
                foreach (var flip in new[] { false, true })
                {
                    foreach (var diff in new[] { false, true })
                    {
                        var solns = new SolutionSet { diff = diff, flip = flip };
                        for (int i = 0; i < 2; i++)
                        {
                            var optimizer = new Optimizer(colors.Where((c, j) => (j / (flip ? 2 : 8)) % 2 == i), diff ? 32 : 16);
                            if (i == 1 && diff)
                            {
                                optimizer.baseColor = solns[0].blockColour.Unscale(32);
                                solns[1] = optimizer.Compute(-4, -3, -2, -1, 0, 1, 2, 3);
                            }
                            else
                            {
                                solns[i] = optimizer.Compute(-4, -3, -2, -1, 0, 1, 2, 3);

                                // this threshold was set arbitrarily
                                if (solns[i].error > 9000)
                                {
                                    var refine = optimizer.Compute(-8, -7, -6, -5, 4, 5, 6, 7);

                                    if (refine.error < solns[i].error)
                                        solns[i] = refine;
                                }
                            }

                            if (solns[i].error >= best_error) break;
                        }

                        int sum = solns[0].error + solns[1].error;
                        if (sum < best_error)
                        {
                            best_error = sum;
                            bestsolns = solns;
                        }
                    }
                }

                var blk = new Block
                {
                    DiffBit = bestsolns.diff,
                    FlipBit = bestsolns.flip,
                    Table0 = Array.IndexOf(modifiers, bestsolns[0].intenTable),
                    Table1 = Array.IndexOf(modifiers, bestsolns[1].intenTable)
                };

                var c0 = bestsolns[0].blockColour.Unscale(blk.DiffBit ? 32 : 16);
                var c1 = bestsolns[1].blockColour.Unscale(blk.DiffBit ? 32 : 16);
                if (blk.DiffBit)
                {
                    int rdiff = (c1.R - c0.R + 8) % 8;
                    int gdiff = (c1.G - c0.G + 8) % 8;
                    int bdiff = (c1.B - c0.B + 8) % 8;
                    blk.R = (byte)(c0.R * 8 + rdiff);
                    blk.G = (byte)(c0.G * 8 + gdiff);
                    blk.B = (byte)(c0.B * 8 + bdiff);
                }
                else
                {
                    blk.R = (byte)(c0.R * 16 + c1.R);
                    blk.G = (byte)(c0.G * 16 + c1.G);
                    blk.B = (byte)(c0.B * 16 + c1.B);
                }

                if (blk.FlipBit)
                {
                    int m0 = bestsolns[0].selectorMSB, m1 = bestsolns[1].selectorMSB;
                    m0 = (m0 & 0xC0) * 64 + (m0 & 0x30) * 16 + (m0 & 0xC) * 4 + (m0 & 0x3);
                    m1 = (m1 & 0xC0) * 64 + (m1 & 0x30) * 16 + (m1 & 0xC) * 4 + (m1 & 0x3);
                    blk.MSB = (ushort)(m0 + 4 * m1);
                    int l0 = bestsolns[0].selectorLSB, l1 = bestsolns[1].selectorLSB;
                    l0 = (l0 & 0xC0) * 64 + (l0 & 0x30) * 16 + (l0 & 0xC) * 4 + (l0 & 0x3);
                    l1 = (l1 & 0xC0) * 64 + (l1 & 0x30) * 16 + (l1 & 0xC) * 4 + (l1 & 0x3);
                    blk.LSB = (ushort)(l0 + 4 * l1);
                }
                else
                {
                    blk.MSB = (ushort)(bestsolns[0].selectorMSB + 256 * bestsolns[1].selectorMSB);
                    blk.LSB = (ushort)(bestsolns[0].selectorLSB + 256 * bestsolns[1].selectorLSB);
                }
                return blk;

            }
        }
    }
}
