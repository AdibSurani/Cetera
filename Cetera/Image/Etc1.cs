using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    public class Etc1
    {
        public static int WorstErrorEver = 0;
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

        [DebuggerDisplay("{R},{G},{B}")]
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

            class SolutionSet
            {
                public SolutionSet()
                {
                    total_error = MAX_ERROR;
                }

                public SolutionSet(bool flip, bool diff, Solution soln0, Solution soln1)
                {
                    this.flip = flip;
                    this.diff = diff;
                    this.soln0 = soln0;
                    this.soln1 = soln1;
                    total_error = soln0.error + soln1.error;
                }

                public readonly bool flip;
                public readonly bool diff;
                public readonly int total_error;
                public readonly Solution soln0;
                public readonly Solution soln1;
                public Solution this[int i] => i == 0 ? soln0 : soln1;
            }

            class Solution
            {
                public int error;
                public RGB blockColour;
                public int[] intenTable;
                public int selectorMSB;
                public int selectorLSB;
            }

            List<RGB> pixels;
            public RGB baseColor;
            int limit;
            Solution best_soln;

            Optimizer(IEnumerable<RGB> pixels, int limit, int error)
            {
                this.pixels = pixels.ToList();
                this.limit = limit;
                baseColor = RGB.Average(pixels).Unscale(limit);
                best_soln = new Solution { error = error };
            }

            bool ComputeDeltas(params int[] deltas)
            {
                return TestUnscaledColors(from zd in deltas
                                          let z = zd + baseColor.B
                                          where z >= 0 && z < limit
                                          from yd in deltas
                                          let y = yd + baseColor.G
                                          where y >= 0 && y < limit
                                          from xd in deltas
                                          let x = xd + baseColor.R
                                          where x >= 0 && x < limit
                                          select new RGB(x, y, z));
            }

            IEnumerable<Solution> FindExactMatches(IEnumerable<RGB> colors, int[] intenTable)
            {
                foreach (var c in colors)
                {
                    best_soln = new Solution { error = 1 };
                    if (EvaluateSolution(c, intenTable))
                        yield return best_soln;
                }
            }

            bool TestUnscaledColors(IEnumerable<RGB> colors)
            {
                bool success = false;
                foreach (var c in colors)
                {
                    foreach (var t in modifiers)
                    {
                        if (EvaluateSolution(c, t))
                        {
                            success = true;
                            if (best_soln.error == 0) return true;
                        }
                    }
                }
                return success;
            }

            bool EvaluateSolution(RGB c, int[] intenTable)
            {
                var soln = new Solution { blockColour = c, intenTable = intenTable };
                var newTable = new RGB[4];
                var scaledColor = c.Scale(limit);
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
                    if (soln.error >= best_soln.error) return false;
                    soln.selectorMSB |= (byte)(best_j / 2 << i);
                    soln.selectorLSB |= (byte)(best_j % 2 << i);
                }
                best_soln = soln;
                return true;
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

            //static byte[][] lookup16 = modifiers.Select(t => (from a in Enumerable.Range(0, 16) from m in t select (byte)Clamp(17 * a + m)).OrderBy(x => x).Distinct().ToArray()).ToArray();
            //static byte[][] lookup32 = modifiers.Select(t => (from a in Enumerable.Range(0, 32) from m in t select (byte)Clamp(a * 8 + a / 4 + m)).OrderBy(x => x).Distinct().ToArray()).ToArray();
            static bool[][] lookup16 = new bool[8][];
            static bool[][] lookup32 = new bool[8][];
            static byte[][][] lookup16big = new byte[8][][];
            static byte[][][] lookup32big = new byte[8][][];
            static Optimizer()
            {
                for (int i = 0; i < 8; i++)
                {
                    lookup16[i] = new bool[256];
                    lookup32[i] = new bool[256];
                    lookup16big[i] = new byte[16][];
                    lookup32big[i] = new byte[32][];
                    for (int j = 0; j < 16; j++)
                    {
                        lookup16big[i][j] = modifiers[i].Select(mod => (byte)Clamp(j * 17 + mod)).Distinct().ToArray();
                        foreach (var k in lookup16big[i][j]) lookup16[i][k] = true;
                    }
                    for (int j = 0; j < 32; j++)
                    {
                        lookup32big[i][j] = modifiers[i].Select(mod => (byte)Clamp(j * 8 + j / 4 + mod)).Distinct().ToArray();
                        foreach (var k in lookup32big[i][j]) lookup32[i][k] = true;
                    }
                }
            }

            static Block? Check(List<RGB> colors)
            {
                foreach (var flip in new[] { false, true })
                {
                    var allpixels0 = colors.Where((c, j) => (j / (flip ? 2 : 8)) % 2 == 0);
                    var pixels0 = allpixels0.Distinct().ToArray();
                    if (pixels0.Count() > 4) continue;

                    var allpixels1 = colors.Where((c, j) => (j / (flip ? 2 : 8)) % 2 == 1);
                    var pixels1 = allpixels1.Distinct().ToArray();
                    if (pixels1.Count() > 4) continue;

                    foreach (var diff in new[] { false, true })
                    {
                        if (!diff)
                        {
                            var tables0 = Enumerable.Range(0, 8).Where(i => pixels0.All(c => lookup16[i][c.R] && lookup16[i][c.G] && lookup16[i][c.B])).ToList();
                            if (!tables0.Any()) continue;
                            var tables1 = Enumerable.Range(0, 8).Where(i => pixels1.All(c => lookup16[i][c.R] && lookup16[i][c.G] && lookup16[i][c.B])).ToList();
                            if (!tables1.Any()) continue;

                            var opt0 = new Optimizer(allpixels0, 16, 1);
                            Solution soln0 = null;
                            foreach (var ti in tables0)
                            {
                                var rs = Enumerable.Range(0, 16).Where(a => pixels0.All(c => lookup16big[ti][a].Contains(c.R))).ToArray();
                                var gs = Enumerable.Range(0, 16).Where(a => pixels0.All(c => lookup16big[ti][a].Contains(c.G))).ToArray();
                                var bs = Enumerable.Range(0, 16).Where(a => pixels0.All(c => lookup16big[ti][a].Contains(c.B))).ToArray();
                                soln0 = opt0.FindExactMatches(from r in rs from g in gs from b in bs select new RGB(r, g, b), modifiers[ti]).FirstOrDefault();
                                if (soln0 != null) break;
                            }
                            if (soln0 == null) continue;

                            var opt1 = new Optimizer(allpixels1, 16, 1);
                            Solution soln1 = null;
                            foreach (var ti in tables1)
                            {
                                var rs = Enumerable.Range(0, 16).Where(a => pixels1.All(c => lookup16big[ti][a].Contains(c.R))).ToArray();
                                var gs = Enumerable.Range(0, 16).Where(a => pixels1.All(c => lookup16big[ti][a].Contains(c.G))).ToArray();
                                var bs = Enumerable.Range(0, 16).Where(a => pixels1.All(c => lookup16big[ti][a].Contains(c.B))).ToArray();
                                soln1 = opt1.FindExactMatches(from r in rs from g in gs from b in bs select new RGB(r, g, b), modifiers[ti]).FirstOrDefault();
                                if (soln1 != null) break;
                            }
                            if (soln1 == null) continue;
                            return FromSet(new SolutionSet(flip, diff, soln0, soln1));
                        }
                        else
                        {
                            var tables0 = Enumerable.Range(0, 8).Where(i => pixels0.All(c => lookup32[i][c.R] && lookup32[i][c.G] && lookup32[i][c.B])).ToList();
                            if (!tables0.Any()) continue;
                            var tables1 = Enumerable.Range(0, 8).Where(i => pixels1.All(c => lookup32[i][c.R] && lookup32[i][c.G] && lookup32[i][c.B])).ToList();
                            if (!tables1.Any()) continue;

                            var opt0 = new Optimizer(allpixels0, 32, 1);
                            var solns0 = new List<Solution>();
                            foreach (var ti in tables0)
                            {
                                var rs = Enumerable.Range(0, 32).Where(a => pixels0.All(c => lookup32big[ti][a].Contains(c.R))).ToArray();
                                var gs = Enumerable.Range(0, 32).Where(a => pixels0.All(c => lookup32big[ti][a].Contains(c.G))).ToArray();
                                var bs = Enumerable.Range(0, 32).Where(a => pixels0.All(c => lookup32big[ti][a].Contains(c.B))).ToArray();
                                solns0.AddRange(opt0.FindExactMatches(from r in rs from g in gs from b in bs select new RGB(r, g, b), modifiers[ti]));
                            }
                            if (!solns0.Any()) continue;

                            var opt1 = new Optimizer(allpixels1, 32, 1);
                            foreach (var ti in tables1)
                            {
                                var rs = Enumerable.Range(0, 32).Where(a => pixels1.All(c => lookup32big[ti][a].Contains(c.R))).ToArray();
                                var gs = Enumerable.Range(0, 32).Where(a => pixels1.All(c => lookup32big[ti][a].Contains(c.G))).ToArray();
                                var bs = Enumerable.Range(0, 32).Where(a => pixels1.All(c => lookup32big[ti][a].Contains(c.B))).ToArray();
                                foreach (var s0 in solns0)
                                {
                                    var q = (from r in rs
                                             let dr = r - s0.blockColour.R
                                             where dr >= -4 && dr < 4
                                             from g in gs
                                             let dg = g - s0.blockColour.G
                                             where dg >= -4 && dg < 4
                                             from b in bs
                                             let db = b - s0.blockColour.B
                                             where db >= -4 && db < 4
                                             select new RGB(r, g, b));
                                    var soln1 = opt1.FindExactMatches(q, modifiers[ti]).FirstOrDefault();
                                    if (soln1 != null)
                                    {
                                        return FromSet(new SolutionSet(flip, diff, s0, soln1));
                                    }
                                }
                            }
                        }
                    }

                }
                //return (pixels0.Distinct().Count() <= 4 && pixels0.Distinct().Count() <= 4);
                return null;
            }

            static Block FromSet(SolutionSet bestsolns)
            {
                var blk = new Block
                {
                    DiffBit = bestsolns.diff,
                    FlipBit = bestsolns.flip,
                    Table0 = Array.IndexOf(modifiers, bestsolns[0].intenTable),
                    Table1 = Array.IndexOf(modifiers, bestsolns[1].intenTable)
                };

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

                var c0 = bestsolns[0].blockColour;
                var c1 = bestsolns[1].blockColour;
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

                return blk;
            }

            public static Block Encode(List<RGB> colors)
            {
                var chk = Check(colors);
                if (chk != null) return chk.Value;
                return PackSolidColor(new RGB(255, 255, 255));

                if (colors.Distinct().Count() == 1)
                {
                    return PackSolidColor(colors[0]);
                }

                var bestsolns = new SolutionSet();
                foreach (var flip in new[] { false, true })
                {
                    var pixels0 = colors.Where((c, j) => (j / (flip ? 2 : 8)) % 2 == 0);
                    var pixels1 = colors.Where((c, j) => (j / (flip ? 2 : 8)) % 2 == 1);
                    foreach (var diff in new[] { false, true }) // let's again just assume no diff
                    {
                        int limit = diff ? 32 : 16;
                        Func<IEnumerable<byte>, List<int>> GetColors = src =>
                            (from n in Enumerable.Range(0, limit)
                             let n2 = diff ? (n << 3) | (n >> 2) : n * 17
                             from t in modifiers
                             orderby src.Sum(ch => t.Select(mod => Clamp(n2 + mod)).Min(m => Square(m - ch)))
                             select n)
                             .Distinct().Take(8).ToList();
                        var rs = GetColors(pixels0.Select(c => c.R));
                        var gs = GetColors(pixels0.Select(c => c.G));
                        var bs = GetColors(pixels0.Select(c => c.B));

                        var opt0 = new Optimizer(pixels0, limit, bestsolns.total_error);
                        if (!opt0.TestUnscaledColors(from r in rs from b in bs from g in gs select new RGB(r, g, b)))
                            continue;
                        if (opt0.best_soln.error >= bestsolns.total_error)
                            continue;

                        var opt1 = new Optimizer(pixels1, limit, bestsolns.total_error - opt0.best_soln.error);
                        if (diff)
                        {
                            opt1.baseColor = opt0.best_soln.blockColour;
                            if (!opt1.ComputeDeltas(-4, -3, -2, -1, 0, 1, 2, 3))
                                continue;
                        }
                        else
                        {
                            rs = GetColors(pixels1.Select(c => c.R));
                            gs = GetColors(pixels1.Select(c => c.G));
                            bs = GetColors(pixels1.Select(c => c.B));
                            if (!opt1.TestUnscaledColors(from r in rs from b in bs from g in gs select new RGB(r, g, b)))
                                continue;
                        }

                        var solnset = new SolutionSet(flip, diff, opt0.best_soln, opt1.best_soln);
                        if (solnset.total_error < bestsolns.total_error)
                            bestsolns = solnset;

                    }
                }
                // 37914
                if (bestsolns.total_error == 29366)
                {
                    var s = string.Join(";", colors.Select(x => $"{x.R},{x.G},{x.B}"));
                    System.Windows.Forms.MessageBox.Show(s);
                }
                WorstErrorEver = Math.Max(WorstErrorEver, bestsolns.total_error);

                return FromSet(bestsolns);
            }
        }
    }
}
