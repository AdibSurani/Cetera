using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cetera
{
    class GZip
    {
        public static byte[] Compress(byte[] bytes)
        {
            var ms = new MemoryStream();
            using (var gz = new GZipStream(ms, CompressionLevel.Optimal))
            {
                new MemoryStream(bytes).CopyTo(gz);
            }
            return ms.ToArray();
        }

        public static byte[] Decompress(byte[] bytes)
        {
            var ms = new MemoryStream();
            using (var gz = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress))
            {
                gz.CopyTo(ms);
            }
            return ms.ToArray();
        }
    }
}
