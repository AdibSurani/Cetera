using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Cetera
{
    class XF
    {
        public XF(Stream input)
        {
            using (var br = new BinaryReader(input))
            {
                br.ReadBytes(0x339D4); // temporary hack
                var buf1 = CriWareCompression.GetDecompressedBytes(br.BaseStream);
                var buf2 = CriWareCompression.GetDecompressedBytes(br.BaseStream);
                var buf3 = CriWareCompression.GetDecompressedBytes(br.BaseStream);
                int k = 1;
            }
        }
    }
}
