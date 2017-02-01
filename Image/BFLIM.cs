//using System;
//using System.Collections.Generic;
//using System.Drawing;
//using System.IO;
//using System.Linq;
//using System.Runtime.InteropServices;
//using System.Text;
//using System.Threading.Tasks;

//namespace Cetera
//{
//    class BFLIM
//    {
//        [StructLayout(LayoutKind.Sequential, Pack = 1)]
//        struct BFLIMHeader
//        {
//            public String4 magic; // FLIM
//            public ByteOrder byteOrder;
//            public int size;
//            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
//            private byte[] to_be_finished;
//            public String4 magic2;
//            public int stuff;
//            public short width;
//            public short height;
//            byte b1;
//            byte b2;
//            public ImageCommon.Format format;
//            public ImageCommon.Swizzle swizzle;
//            int imgSize;
//        }

//        public static Bitmap Load(Stream input)
//        {
//            using (var br = new BinaryReader(input))
//            {
//                var tex = br.ReadBytes((int)br.BaseStream.Length - 40);
//                var header = br.ReadStruct<BFLIMHeader>();
//                var colors = ImageCommon.GetColorsFromTexture(tex, header.format);
//                return ImageCommon.Load(colors, header.width, header.height, header.swizzle, true);
//            }
//        }
//    }
//}
