using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Cetera
{
    public class MSBT : List<MSBT.Item>
    {
        [DebuggerDisplay("{Label,nq}: {Text,nq}")]
        public class Item
        {
            public string Label { get; set; }
            public string Text { get; set; }
            public uint Hash => Label.Aggregate(0u, (n, c) => 1170 * n + c) % 101;
        }

        enum MsbtEncoding : byte { UTF8, Unicode }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct Header
        {
            public String8 magic;
            public ByteOrder byteOrder;
            private short zeroes1;
            private MsbtEncoding encoding;
            private byte alwaysEqualTo3;
            public short sectionCount;
            private short zeroes2;
            public int fileSize;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            private byte[] padding;

            public Encoding Encoding => encoding == MsbtEncoding.UTF8 ? Encoding.UTF8 : Encoding.Unicode;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct SectionHeader
        {
            public String4 magic;
            public int size;
            private long padding;
        }

        Header header;
        List<string> sections = new List<string>();
        List<int> tsy1 = null;
        byte[] atr1 = null;

        public MSBT(Stream input)
        {
            using (var br = new BinaryReader(input))
            {
                header = br.ReadStruct<Header>();
                if (header.magic != "MsgStdBn") throw new Exception("Not MSBT");

                List<Tuple<string, int>> lbl1 = null;
                List<string> txt2 = null;

                for (int i = 0; i < header.sectionCount; i++)
                {
                    var section = br.ReadStruct<SectionHeader>();
                    sections.Add(section.magic);

                    switch (section.magic)
                    {
                        case "LBL1":
                            if (br.ReadInt32() != 101) throw new InvalidDataException("Expecting hastable size of 101");
                            var labelCount = br.ReadMultiple(101, _ => (int)br.ReadInt64()).Sum();
                            lbl1 = br.ReadMultiple(labelCount, _ => Tuple.Create(br.ReadString(), br.ReadInt32()));
                            break;
                        case "ATR1":
                            atr1 = br.ReadBytes(section.size);
                            break;
                        case "TSY1":
                            tsy1 = br.ReadMultiple(section.size / 4, _ => br.ReadInt32());
                            break;
                        case "TXT2":
                            var textCount = br.ReadInt32();
                            var offsets = Enumerable.Range(0, textCount).Select(_ => br.ReadInt32()).Concat(new[] { section.size }).ToList();
                            txt2 = offsets.Skip(1).Zip(offsets, (o1, o2) => br.ReadString(header.Encoding, o1 - o2)).ToList();
                            break;
                        default:
                            throw new Exception("Unknown section");
                    }

                    while (br.BaseStream.Position % 16 != 0 && br.BaseStream.Position != br.BaseStream.Length)
                    {
                        br.ReadByte();
                    }
                }

                var labels = lbl1 == null
                           ? Enumerable.Range(0, txt2.Count).Select(i => $"Label_{i}")
                           : from z in lbl1 orderby z.Item2 select z.Item1;
                AddRange(labels.Zip(txt2, (lbl, txt) => new Item { Label = lbl, Text = txt }));
            }
        }

        // A quick test to check that the hashes are returned in the same order as that originally stored in the MSBT file
        public IEnumerable<Item> HashTest => this.OrderBy(item => item.Hash);
    }
}
