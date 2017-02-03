using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Cetera.Compression;
using Cetera.IO;

namespace Cetera.Image
{
    public sealed class XI
    {
        public enum Format : byte
        {
            RGBA8888, RGBA4444,
            RGBA5551, RGB888, RGB565,
            LA88 = 11, LA44, L8, HL88, A8,
            L4 = 26, A4, ETC1, ETC1A4
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Header
        {
            public int magic; // IMGC
            public int const1; // 30 30 00 00
            public short const2; // 30 00
            public Format imageFormat;
            public Swizzle swizzle; // always 01?
            public byte combineFormat;
            public byte bitDepth;
            public short bytesPerTile;
            public short width;
            public short height;
            public int const3; // 30 00 00 00
            public int const4; // 30 00 01 00
            public int const5; // 48 00 00 00 = tableDataOffset
            public int const6; // 03 00 00 00
            public int const7; // 00 00 00 00
            public int const8; // 00 00 00 00
            public int const9; // 00 00 00 00
            public int const10; // 00 00 00 00
            public int tableSize1;
            public int tableSize2;
            public int imgDataSize;
            public int const11; // 00 00 00 00
            public int const12; // 00 00 00 00
        }

        public Bitmap Image { get; set; }
        public Settings Settings { get; set; }
        public int CombineFormat { get; set; }

        public XI(Stream input)
        {
            using (var br = new BinaryReaderX(input))
            {
                var header = br.ReadStruct<Header>();
                Settings = new Settings { Width = header.width, Height = header.height, Swizzle = header.swizzle, PadToPowerOf2 = false };
                Settings.SetFormat(header.imageFormat);
                CombineFormat = header.combineFormat;

                if (CombineFormat != 1)
                    throw new Exception($"Unknown combine format {header.combineFormat}");

                var buf1 = CriWare.GetDecompressedBytes(input);
                while (input.Position % 4 != 0) input.ReadByte();
                var buf2 = CriWare.GetDecompressedBytes(input);

                var ms = new MemoryStream();
                using (var bw = new BinaryWriter(ms))
                {
                    for (int i = 0; i < buf1.Length / 2; i++)
                    {
                        int index = BitConverter.ToInt16(buf1, 2 * i);
                        ms.Write(buf2, index * header.bytesPerTile, header.bytesPerTile);
                    }
                }
                var tmp = ms.ToArray();
                Image = Common.Load(ms.ToArray(), Settings);
            }
        }
    }
}
