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
        public struct Header
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

        public Header JTEXHeader { get; private set; }
        public Bitmap Image { get; set; }
        public Settings Settings { get; set; }

        public JTEX(Stream input)
        {
            using (var br = new BinaryReaderX(input))
            {
                JTEXHeader = br.ReadStruct<Header>();
                Settings = new Settings { Width = JTEXHeader.width, Height = JTEXHeader.height };
                Settings.SetFormat(JTEXHeader.format);
                var texture = br.ReadBytes(JTEXHeader.unk3[0]); // bytes to read?
                Image = Common.Load(texture, Settings);
            }
        }

        public void Save(Stream output)
        {
            using (var bw = new BinaryWriterX(output))
            {
                var modifiedJTEXHeader = JTEXHeader;
                modifiedJTEXHeader.width = (short)Image.Width;
                modifiedJTEXHeader.height = (short)Image.Height;

                var settings = new Settings();
                settings.Width = modifiedJTEXHeader.width;
                settings.Height = modifiedJTEXHeader.height;
                settings.Format = Settings.ConvertFormat(modifiedJTEXHeader.format);

                byte[] texture = Common.Save(Image, settings);
                modifiedJTEXHeader.unk3[0] = texture.Length;
                JTEXHeader = modifiedJTEXHeader;

                bw.WriteStruct(JTEXHeader);
                bw.Write(texture);
            }
        }
    }
}
