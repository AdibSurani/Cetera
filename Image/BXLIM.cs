using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Cetera
{
    sealed class BXLIM
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

        public Bitmap Image { get; set; }
        public Format ImageFormat { get; set; }
        public ImageCommon.Swizzle Swizzle { get; set; }
        public short UnknownShort { get; set; }

        public BXLIM(Stream input)
        {
            using (var br = new BinaryReader(input))
            {
                var tex = br.ReadBytes((int)br.BaseStream.Length - 40);
                int width, height;
                if (br.PeekChar() == 'C')
                {
                    var header = br.ReadSections().Single().Data.ToStruct<BCLIMImageHeader>();
                    width = header.width;
                    height = header.height;
                    ImageFormat = header.format;
                    Swizzle = header.swizzle;
                    UnknownShort = header.unknown;
                }
                else
                {
                    var header = br.ReadSections().Single().Data.ToStruct<BFLIMImageHeader>();
                    width = header.width;
                    height = header.height;
                    ImageFormat = header.format;
                    Swizzle = header.swizzle;
                    UnknownShort = header.unknown;
                }
                var colors = ImageCommon.GetColorsFromTexture(tex, ImageFormat);
                Image = ImageCommon.Load(colors, width, height, Swizzle, true);
            }
        }
    }
}
