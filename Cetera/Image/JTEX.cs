using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Cetera.IO;
using Cetera.Compression;

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

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct RawHeader
        {
            public uint dataStart;
            uint formTmp;
            uint unk1;
            uint unk2;
            public int width;
            public int height;

            public Format format => (Format)(formTmp & 0xFF);
        }

        public bool lz11_compressed = false;

        public Header JTEXHeader { get; private set; }
        public RawHeader JTEXRawHeader;
        public Bitmap Image { get; set; }
        public ImageSettings Settings { get; set; }

        public JTEX(Stream input, bool raw)
        {
            if (raw)
            {
                LoadRaw(input);
            }
            else
            {
                using (var br = new BinaryReaderX(input))
                {
                    JTEXHeader = br.ReadStruct<Header>();
                    Settings = new ImageSettings { Width = JTEXHeader.width, Height = JTEXHeader.height };
                    Settings.SetFormat(JTEXHeader.format);
                    var texture2 = br.ReadBytes(JTEXHeader.unk3[0]); // bytes to read?
                    Image = Common.Load(texture2, Settings);
                }
            }
        }

        public void Save(Stream output)
        {
            using (var bw = new BinaryWriterX(output))
            {
                var modifiedJTEXHeader = JTEXHeader;
                modifiedJTEXHeader.width = (short)Image.Width;
                modifiedJTEXHeader.height = (short)Image.Height;

                var settings = new ImageSettings();
                settings.Width = modifiedJTEXHeader.width;
                settings.Height = modifiedJTEXHeader.height;
                settings.Format = ImageSettings.ConvertFormat(modifiedJTEXHeader.format);

                byte[] texture = Common.Save(Image, settings);
                modifiedJTEXHeader.unk3[0] = texture.Length;
                JTEXHeader = modifiedJTEXHeader;

                bw.WriteStruct(JTEXHeader);
                bw.Write(texture);
            }
        }

        public void LoadRaw(Stream input)
        {
            using (var br = new BinaryReaderX(input))
            {
                Stream stream;

                if (br.ReadByte() == 0x11)
                {
                    br.BaseStream.Position = 0;
                    uint size = br.ReadUInt32() >> 8;
                    br.BaseStream.Position = 0;
                    lz11_compressed = true;
                    byte[] decomp = LZ11.Decompress(br.BaseStream);
                    stream = new MemoryStream(decomp);
                }
                else
                {
                    br.BaseStream.Position = 0;
                    stream = br.BaseStream;
                }

                //File.OpenWrite("test.decomp").Write(new BinaryReaderX(stream).ReadBytes((int)stream.Length), 0, (int)stream.Length);

                using (BinaryReaderX br2 = new BinaryReaderX(stream))
                {
                    JTEXRawHeader = br2.ReadStruct<RawHeader>();
                    br2.BaseStream.Position = JTEXRawHeader.dataStart;
                    Settings = new ImageSettings { Width = JTEXRawHeader.width, Height = JTEXRawHeader.height, Format = JTEXRawHeader.format };
                    Image = Common.Load(br2.ReadBytes((int)(br2.BaseStream.Length - br2.BaseStream.Position)), Settings);
                }
            }
        }

        public void SaveRaw(Stream output)
        {
            ImageSettings modSettings = Settings;
            modSettings.Width = Image.Width;
            modSettings.Height = Image.Height;

            byte[] data = Common.Save(Image, modSettings);
            using (BinaryWriterX br = new BinaryWriterX(new MemoryStream()))
            {
                JTEXRawHeader.width = (ushort)Image.Width; JTEXRawHeader.height = (ushort)Image.Height;
                br.WriteStruct<RawHeader>(JTEXRawHeader);
                br.BaseStream.Position = JTEXRawHeader.dataStart;
                br.Write(data);
                br.BaseStream.Position = 0;

                if (lz11_compressed)
                {
                    byte[] comp = LZ11.Compress(br.BaseStream);
                    output.Write(comp, 0, comp.Length);
                }
                else
                {
                    output.Write(new BinaryReaderX(br.BaseStream).ReadBytes((int)br.BaseStream.Length), 0, (int)br.BaseStream.Length);
                }
                output.Close();
            }
        }
    }
}
