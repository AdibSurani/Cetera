using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Cetera.IO;

namespace Cetera.Compression
{
    public class LZ11
    {
        public static byte[] Decompress(Stream stream)
        {
            using (BinaryReaderX br = new BinaryReaderX(stream))
            {
                uint length = br.ReadUInt32() >> 8;
                byte[] result = new byte[length];
                int dstoffset = 0;

                while (true)
                {
                    byte header = br.ReadByte();
                    for (int i = 0; i < 8; i++)
                    {
                        if ((header & 0x80) == 0) result[dstoffset++] = br.ReadByte();
                        else
                        {
                            byte a = br.ReadByte();
                            int offset;
                            int length2;
                            if ((a >> 4) == 0)
                            {
                                byte b = br.ReadByte();
                                byte c = br.ReadByte();
                                length2 = (((a & 0xF) << 4) | (b >> 4)) + 0x11;
                                offset = (((b & 0xF) << 8) | c) + 1;
                            }
                            else if ((a >> 4) == 1)
                            {
                                byte b = br.ReadByte();
                                byte c = br.ReadByte();
                                byte d = br.ReadByte();
                                length2 = (((a & 0xF) << 12) | (b << 4) | (c >> 4)) + 0x111;
                                offset = (((c & 0xF) << 8) | d) + 1;
                            }
                            else
                            {
                                byte b = br.ReadByte();
                                length2 = (a >> 4) + 1;
                                offset = (((a & 0xF) << 8) | b) + 1;
                            }

                            for (int j = 0; j < length2; j++)
                            {
                                result[dstoffset] = result[dstoffset - offset];
                                dstoffset++;
                            }
                        }

                        if (dstoffset >= length) return result;
                        header <<= 1;
                    }
                }
            }
        }
    }
}
