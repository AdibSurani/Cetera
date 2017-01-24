using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Cetera
{
    class BCLIM
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct Header
        {
            public Magic magic; // CLIM
            public ByteOrder byteOrder;
            public int size;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            private byte[] to_be_finished;
            public Magic magic2;
            public int stuff;
            public short width;
            public short height;
            public ImageCommon.Format format;
            public ImageCommon.Swizzle swizzle;
            byte b1;
            byte b2;
            int imgSize;
        }

        public static Bitmap Load(Stream input)
        {
            using (var br = new BinaryReader(input))
            {
                var tex = br.ReadBytes((int)br.BaseStream.Length - 40);
                var header = br.ReadStruct<Header>();
                return ImageCommon.FromTexture(tex, header.width, header.height, header.format, header.swizzle);
            }
        }
    }
}
