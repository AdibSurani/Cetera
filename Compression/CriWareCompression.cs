using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Cetera
{
    class CriWareCompression
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
                        for (int i = 0, flags = 0; ; i++)
                        {
                            if (i % 8 == 0) flags = br.ReadByte();
                            if ((flags & 0x80) == 0) result.Add(br.ReadByte());
                            else
                            {
                                int lengthDist = BitConverter.ToUInt16(br.ReadBytes(2).Reverse().ToArray(), 0);
                                int offs = lengthDist % 4096 + 1;
                                int length = lengthDist / 4096 + 3;
                                while (length-- > 0)
                                    result.Add(result[result.Count - offs]);
                            }
                            if (result.Count == size)
                            {
                                return result.ToArray();
                            }
                            else if (result.Count > size)
                            {
                                throw new InvalidDataException("Went past the end of the stream");
                            }
                            flags <<= 1;
                        }
                    case Method.Huffman4Bit:
                    case Method.Huffman8Bit:
                        int num_bits = method == Method.Huffman4Bit ? 4 : 8;
                        var tree_size = br.ReadByte();
                        var tree_root = br.ReadByte();
                        var tree_buffer = br.ReadBytes(tree_size * 2);

                        for (int i = 0, code = 0, next = 0, pos = tree_root; ; i++)
                        {
                            if (i % 32 == 0) code = br.ReadInt32();
                            next += (pos & 0x3F) * 2 + 2;
                            int direction = (code >> (31 - i)) % 2 == 0 ? 2 : 1;
                            var leaf = (pos >> 5 >> direction) % 2 != 0;
                            pos = tree_buffer[next - direction];
                            if (leaf)
                            {
                                result.Add((byte)pos);
                                pos = tree_root;
                                next = 0;
                            }

                            if (result.Count == size * 8 / num_bits)
                            {
                                return num_bits == 8 ? result.ToArray() :
                                    Enumerable.Range(0, size).Select(j => (byte)(result[2 * j + 1] * 16 + result[2 * j])).ToArray();
                            }
                        }
                    case Method.RLE:
                        while (true)
                        {
                            var flag = br.ReadByte();
                            if (flag >= 128)
                                result.AddRange(Enumerable.Repeat(br.ReadByte(), flag - 128 + 3));
                            else
                                result.AddRange(br.ReadBytes(flag + 1));
                            if (result.Count == size)
                            {
                                if (br.BaseStream.Position != br.BaseStream.Length)
                                    throw new Exception("Haven't consumed all data in stream");
                                return result.ToArray();
                            }
                            else if (result.Count > size)
                            {
                                throw new InvalidDataException("Went past the end of the stream");
                            }
                        }
                    default:
                        throw new NotSupportedException($"Unknown compression method {method}");
                }
            }
        }
    }
}
