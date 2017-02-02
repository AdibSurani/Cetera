using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Cetera.IO;

namespace Cetera.Image
{
    public sealed class BXLIM
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct BCLIMImageHeader
        {
            public short width;
            public short height;
            public Format format;
            public Common.Swizzle swizzle;
            public short unknown;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct BFLIMImageHeader
        {
            public short width;
            public short height;
            public short unknown;
            public Format format;
            public Common.Swizzle swizzle;
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
        public Common.Swizzle Swizzle { get; set; }
        public short UnknownShort { get; set; }

        public BXLIM(Stream input)
        {
            using (var br = new BinaryReaderX(input))
            {
                var tex = br.ReadBytes((int)br.BaseStream.Length - 40);
                string magic;
                var imagData = br.ReadSections(out magic).Single().Data;
                int width, height;
                switch (magic)
                {
                    case "CLIM":
                        var bclim = imagData.ToStruct<BCLIMImageHeader>();
                        width = bclim.width;
                        height = bclim.height;
                        ImageFormat = bclim.format;
                        Swizzle = bclim.swizzle;
                        UnknownShort = bclim.unknown;
                        break;
                    case "FLIM":
                        var bflim = imagData.ToStruct<BFLIMImageHeader>();
                        width = bflim.width;
                        height = bflim.height;
                        ImageFormat = bflim.format;
                        Swizzle = bflim.swizzle;
                        UnknownShort = bflim.unknown;
                        break;
                    default:
                        throw new NotSupportedException($"Unknown image format {magic}");
                }

                var colors = Common.GetColorsFromTexture(tex, ImageFormat);
                Image = Common.Load(colors, width, height, Swizzle, true);
            }
        }
    }
}
