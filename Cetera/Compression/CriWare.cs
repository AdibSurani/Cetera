using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Cetera.Compression
{
    public class CriWare
    {
        enum Method
        {
            NoCompression,
            LZSS,
            Huffman4Bit,
            Huffman8Bit,
            RLE
        }

        public static byte[] GetDecompressedBytes(Stream stream)
        {
            using (var br = new BinaryReader(stream, Encoding.Default, true))
            {
                int sizeAndMethod = br.ReadInt32();
                int size = sizeAndMethod / 8;
                var method = (Method)(sizeAndMethod % 8);
                var result = new List<byte>();
                switch (method)
                {
                    case Method.NoCompression:
                        return br.ReadBytes(size);
                    case Method.LZSS:
                        return LZSS.Decompress(new MemoryStream(br.ReadBytes((int)br.BaseStream.Length - 4)), size);
                    case Method.Huffman4Bit:
                    case Method.Huffman8Bit:
                        int num_bits = method == Method.Huffman4Bit ? 4 : 8;
                        return Huffman.Decompress(new MemoryStream(br.ReadBytes((int)br.BaseStream.Length - 4)), num_bits, size);
                    case Method.RLE:
                        return RLE.Decompress(new MemoryStream(br.ReadBytes((int)br.BaseStream.Length - 4)), size);
                    default:
                        throw new NotSupportedException($"Unknown compression method {method}");
                }
            }
        }
    }
}
