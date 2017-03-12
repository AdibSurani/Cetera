using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cetera.IO;
using System.IO;

namespace Cetera.Compression
{
    class RLE
    {
        public static byte[] Decompress(Stream instream, long decompressedLength)
        {
            using (BinaryReaderX br = new BinaryReaderX(instream, true))
            {
                List<byte> result = new List<byte>();

                while (true)
                {
                    byte flag = br.ReadByte();
                    if (flag >= 128)
                        result.AddRange(Enumerable.Repeat(br.ReadByte(), flag - 128 + 3));
                    else
                        result.AddRange(br.ReadBytes(flag + 1));

                    if (result.Count == decompressedLength)
                    {
                        return result.ToArray();
                    }
                    else if (result.Count > decompressedLength)
                    {
                        throw new InvalidDataException("Went past the end of the stream");
                    }
                }
            }
        }
    }
}
