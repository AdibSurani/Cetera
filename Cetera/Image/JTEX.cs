using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Cetera.IO;

namespace Cetera.Image
{
    public class JTEX
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct Header
        {
            public String4 magic;
            public int unk1;
            public short width;
            public short height;
            public BXLIM.Format format;
            public Orientation orientation;
            public short unk2;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 28)]
            public int[] unk3;
        }

        public Bitmap Image { get; set; }
        public Settings Settings { get; set; }

        public JTEX(Stream input)
        {
            using (var br = new BinaryReaderX(input))
            {
                var header = br.ReadStruct<Header>();
                Settings = new Settings { Width = header.width, Height = header.height };
                Settings.SetFormat(header.format);
                var tex = br.ReadBytes(header.unk3[0]); // bytes to read?
                Image = Common.Load(tex, Settings);
            }
        }
    }
}
