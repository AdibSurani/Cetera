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
        [DllImport("rg_etc1.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern bool pack_etc1_block_init();

        [DllImport("rg_etc1.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int pack_etc1_block(byte[] pETC1_block, int[] pSrc_pixels_rgba, int[] pack_params);

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

        public struct ColorQ
        {
            public byte A;
            public byte R;
            public byte G;
            public byte B;

            public static ColorQ FromArgb(int pa, int pr, int pg, int pb) => new ColorQ { A = (byte)pa, R = (byte)pr, G = (byte)pg, B = (byte)pb };
            public static ColorQ FromArgb(int pr, int pg, int pb) => new ColorQ { A = 255, R = (byte)pr, G = (byte)pg, B = (byte)pb };
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
        }

        static Etc1()
        {
            pack_etc1_block_init();
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
        //static int ErrorRGB(int r, int g, int b) => Square(r) + Square(g) + Square(b);
        static int ErrorRGB(int r, int g, int b) => 2 * Square(r) + 4 * Square(g) + 3 * Square(b); // human perception

        static ColorQ AddColor(ColorQ basec, int mod, int alpha = 255)
        {
            return ColorQ.FromArgb(alpha, Clamp(basec.R + mod), Clamp(basec.G + mod), Clamp(basec.B + mod));
        }

        public class Decoder
        {
            Queue<ColorQ> queue = new Queue<ColorQ>();

            public Color Get(Func<PixelData> func)
            {
                if (!queue.Any())
                {
                    var data = func();

                    ColorQ basec0, basec1;
                    if (data.Block.DiffBit)
                    {
                        int r1 = data.Block.R / 8, g1 = data.Block.G / 8, b1 = data.Block.B / 8;
                        int r2 = r1 + Sign3(data.Block.R % 8), g2 = g1 + Sign3(data.Block.G % 8), b2 = b1 + Sign3(data.Block.B % 8);
                        basec0 = ColorQ.FromArgb(Extend5to8(r1), Extend5to8(g1), Extend5to8(b1));
                        basec1 = ColorQ.FromArgb(Extend5to8(r2), Extend5to8(g2), Extend5to8(b2));
                    }
                    else
                    {
                        basec0 = ColorQ.FromArgb(data.Block.R / 16 * 17, data.Block.G / 16 * 17, data.Block.B / 16 * 17);
                        basec1 = ColorQ.FromArgb(data.Block.R % 16 * 17, data.Block.G % 16 * 17, data.Block.B % 16 * 17);
                    }

                    int flipbitmask = data.Block.FlipBit ? 2 : 8;
                    foreach (int i in order3ds)
                    {
                        var basec = (i & flipbitmask) == 0 ? basec0 : basec1;
                        var mod = modifiers[(i & flipbitmask) == 0 ? data.Block.Table0 : data.Block.Table1];
                        queue.Enqueue(AddColor(basec, mod[data.Block[i]], (byte)(data.Alpha >> (4 * i)) % 16 * 17));
                    }
                }
                var cq = queue.Dequeue();
                return Color.FromArgb(cq.A, cq.R, cq.G, cq.B);
            }
        }

        // To be slowly copied from rg_etc1
        class Etc1Optimizer
        {
            static ColorQ Unscale(ColorQ c)
            {
                if (c.A == 16) return ColorQ.FromArgb(c.R * 17, c.G * 17, c.B * 17);
                else return ColorQ.FromArgb((c.R << 3) | (c.R >> 2), (c.G << 3) | (c.G >> 2), (c.B << 3) | (c.B >> 2));
            }

            static int ColorDiff(ColorQ c1, ColorQ c2)
            {
                return ErrorRGB(c1.R - c2.R, c1.G - c2.G, c1.B - c2.B);
            }

            static int[][] g_etc1_inverse_lookup = (from limit in new[] { 16, 32 }
                                                    from inten in modifiers
                                                    from selector in inten
                                                    select (from color in Enumerable.Range(0, 256)
                                                            select Enumerable.Range(0, limit).Min(packed_c =>
                                                            {
                                                                int c = (limit == 32) ? (packed_c << 3) | (packed_c >> 2) : packed_c * 17;
                                                                int err = Math.Abs(Clamp(c + selector) - color);
                                                                return (err << 8) | packed_c;
                                                            }))
                                                            .ToArray())
                                                    .ToArray();

            const int MAX_ERROR = 99999999;

            public class SolutionSet : List<Solution>
            {
                public SolutionSet()
                {
                    Add(new Solution());
                    Add(new Solution());
                }
                public bool flip;
                public bool diff;
            }

            public struct Solution
            {
                public int error;
                public ColorQ blockColour; // check A = 16 or 32
                public int[] intenTable;
                public byte selectorMSB;
                public byte selectorLSB;
            }

            List<ColorQ> pixels;
            ColorQ avgColor;
            int limit;

            public Etc1Optimizer(IEnumerable<ColorQ> srcPixels, int srcLimit)
            {
                pixels = srcPixels.ToList();
                limit = srcLimit;
                avgColor = ColorQ.FromArgb(16, (int)pixels.Average(c => c.R) * limit / 256, (int)pixels.Average(c => c.G) * limit / 256, (int)pixels.Average(c => c.B) * limit / 256);
            }

            public Solution ComputeConstrained(ColorQ firstColor)
            {
                return Compute(firstColor, new[] { -4, -3, -2, -1, 0, 1, 2, 3 });
            }

            public Solution Compute(params int[] deltas)
            {
                return Compute(avgColor, deltas);
            }

            Solution Compute(ColorQ color, int[] deltas)
            {
                return (from zd in deltas
                        let z = zd + color.B
                        where z >= 0 && z < limit
                        from yd in deltas
                        let y = yd + color.G
                        where y >= 0 && y < limit
                        from xd in deltas
                        let x = xd + color.R
                        where x >= 0 && x < limit
                        let c = ColorQ.FromArgb(limit, x, y, z)
                        from t in modifiers
                        select EvaluateSolution(c, t))
                        .MinBy(soln => soln.error);
            }

            public Solution EvaluateSolution(ColorQ c)
            {
                return modifiers.Select(t => EvaluateSolution(c, t)).MinBy(soln => soln.error);
                // skip the refinement for now
            }

            public Solution EvaluateSolution(ColorQ c, int[] intenTable)
            {
                var soln = new Solution { blockColour = c, intenTable = intenTable };
                var unscaledColor = Unscale(c);
                var newTable = new ColorQ[4];
                for (int i = 0; i < 4; i++)
                    newTable[i] = AddColor(unscaledColor, intenTable[i]);

                for (int i = 0; i < 8; i++)
                {
                    int best_j = 0, best_error = MAX_ERROR;
                    for (int j = 0; j < 4; j++)
                    {
                        int error = ColorDiff(pixels[i], newTable[j]);
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

            public static Block PackSolidColor(ColorQ c)
            {
                var soln = (from i in Enumerable.Range(0, 64)
                            let table = g_etc1_inverse_lookup[i]
                            let error = ErrorRGB(table[c.R] >> 8, table[c.G] >> 8, table[c.B] >> 8)
                            let blockColour = ColorQ.FromArgb((byte)table[c.R], (byte)table[c.G], (byte)table[c.B])
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

            public static Block Encode(List<ColorQ> colors, out ulong alpha)
            {
                colors = Enumerable.Range(0, 16).Select(j => colors[order3ds[order3ds[order3ds[j]]]]).ToList();
                alpha = colors.Reverse<ColorQ>().Aggregate(0ul, (a, b) => (a * 16) | (byte)(b.A / 16));
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
                            var optimizer = new Etc1Optimizer(colors.Where((c, j) => (j / (flip ? 2 : 8)) % 2 == i), diff ? 32 : 16);
                            if (i == 1 && diff)
                            {
                                solns[1] = optimizer.ComputeConstrained(solns[0].blockColour);
                            }
                            else
                            {
                                solns[i] = optimizer.Compute(-4, -3, -2, -1, 0, 1, 2, 3);

                                if (solns[i].error > 9000)
                                {
                                    var refine = optimizer.Compute(-8, -7, -6, -5, 4, 5, 6, 7);

                                    if (refine.error < solns[i].error)
                                        solns[i] = refine;
                                }
                            }
                        }

                        int sum = solns[0].error + solns[1].error;
                        if (sum < best_error)
                        {
                            best_error = sum;
                            bestsolns = solns;
                        }
                    } // use_color4
                } // flip

                var blk = new Block
                {
                    DiffBit = bestsolns.diff,
                    FlipBit = bestsolns.flip,
                    Table0 = Array.IndexOf(modifiers, bestsolns[0].intenTable),
                    Table1 = Array.IndexOf(modifiers, bestsolns[1].intenTable)
                };

                if (blk.DiffBit)
                {
                    int rdiff = (bestsolns[1].blockColour.R - bestsolns[0].blockColour.R + 8) % 8;
                    int gdiff = (bestsolns[1].blockColour.G - bestsolns[0].blockColour.G + 8) % 8;
                    int bdiff = (bestsolns[1].blockColour.B - bestsolns[0].blockColour.B + 8) % 8;
                    blk.R = (byte)(bestsolns[0].blockColour.R * 8 + rdiff);
                    blk.G = (byte)(bestsolns[0].blockColour.G * 8 + gdiff);
                    blk.B = (byte)(bestsolns[0].blockColour.B * 8 + bdiff);
                }
                else
                {
                    blk.R = (byte)(bestsolns[0].blockColour.R * 16 + bestsolns[1].blockColour.R);
                    blk.G = (byte)(bestsolns[0].blockColour.G * 16 + bestsolns[1].blockColour.G);
                    blk.B = (byte)(bestsolns[0].blockColour.B * 16 + bestsolns[1].blockColour.B);
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

        public class Encoder
        {
            // This uses rg_etc1.dll as a baseline
            unsafe static Block EncodeWithCpp(List<ColorQ> colors, out ulong alpha)
            {
                colors = Enumerable.Range(0, 16).Select(j => colors[order3ds[order3ds[order3ds[j]]]]).ToList();
                alpha = colors.Reverse<ColorQ>().Aggregate(0ul, (a, b) => (a * 16) | (byte)(b.A / 16));

                var colorArray = new int[16];
                for (int i = 0; i < 16; i++)
                {
                    var color = colors[(i % 4) * 4 + i / 4];
                    colorArray[i] = BitConverter.ToInt32(new byte[] { color.R, color.G, color.B, 255 }, 0);
                }

                var packed = new byte[8];
                pack_etc1_block(packed, colorArray, new[] { 2, 0 }); // high quality with no dithering

                fixed (byte* pBuffer = packed.Reverse().ToArray())
                    return Marshal.PtrToStructure<Block>((IntPtr)pBuffer);
            }

            Queue<ColorQ> queue = new Queue<ColorQ>();

            public void Set(Color c, Action<PixelData> func)
            {
                queue.Enqueue(ColorQ.FromArgb(c.A, c.R, c.G, c.B));
                if (queue.Count == 16)
                {
                    ulong alpha;
                    var data = Etc1Optimizer.Encode(queue.ToList(), out alpha);
                    //var data = EncodeWithCpp(queue.ToList(), out alpha);

                    func(new PixelData { Alpha = alpha, Block = data });
                    queue.Clear();
                }
            }
        }
    }
}
