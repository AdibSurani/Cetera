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
    class BXLIM
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct BCLIMImageHeader
        {
            public short width;
            public short height;
            public Format format;
            public ImageCommon.Swizzle swizzle;
            public short unknown;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct BFLIMImageHeader
        {
            public short width;
            public short height;
            public short unknown;
            public Format format;
            public ImageCommon.Swizzle swizzle;
        }

        public enum Format : byte
        {
            L8, A8, LA44, LA88, HL88,
            RGB565, RGB888, RGBA5551,
            RGBA4444, RGBA8888,
            ETC1, ETC1A4, L4, A4
        }

        public static Bitmap Load(Stream input)
        {
            using (var br = new BinaryReader(input))
            {
                var tex = br.ReadBytes((int)br.BaseStream.Length - 40);
                if (br.PeekChar() == 'C')
                {
                    var header = br.ReadSections().Single().Data.ToStruct<BCLIMImageHeader>();
                    var colors = ImageCommon.GetColorsFromTexture(tex, header.format);
                    return ImageCommon.Load(colors, header.width, header.height, header.swizzle, true);
                }
                else
                {
                    var header = br.ReadSections().Single().Data.ToStruct<BFLIMImageHeader>();
                    var colors = ImageCommon.GetColorsFromTexture(tex, header.format);
                    return ImageCommon.Load(colors, header.width, header.height, header.swizzle, true);
                }
            }
        }
    }
}
