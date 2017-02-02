using Cetera.IO;
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

namespace Cetera.Layout
{
    public class BCLYT
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct Layout
        {
            int origin;
            Vector2D canvas_size;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct Material
        {
            String20 name;
            int tevColor;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            int[] tevColors;
            int flags;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct Pane
        {
            public byte flags;
            public byte base_position_type;
            public byte alpha;
            public byte padding;
            public String16 name;
            public String8 userdata;
            public Vector3D translation;
            public Vector3D rotation;
            public Vector2D scale;
            public Vector2D size;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct TextBox
        {
            public short buffer_length;
            public short string_length;
            public short materialID;
            public short fontID;
            public byte position_type;
            public byte text_align;
            public short padding;
            public int text_offset;
            public int color_top;
            public int color_bottom;
            public Vector2D font_size;
            public Vector2D font_kerning;
        }

        public BCLYT(Stream input)
        {
            using (var sr = new BinaryReaderX(input))
            {
                var sections = sr.ReadSections();

                foreach (var sec in sections)
                {
                    using (var br = new BinaryReaderX(new MemoryStream(sec.Data)))
                    {
                        switch (sec.Magic)
                        {
                            case "lyt1":
                                sec.Object = br.ReadStruct<Layout>();
                                break;
                            case "txl1":
                            case "fnl1":
                                int txlCount = br.ReadInt32();
                                br.ReadMultiple(txlCount, _ => br.ReadInt32());
                                sec.Object = br.ReadMultiple(txlCount, _ => br.ReadCStringA());
                                break;
                            case "mat1":
                                // incomplete
                                break;
                            case "pan1":
                                // incomplete
                            case "pic1":
                                // incomplete
                            case "wnd1":
                                 // incomplete
                            case "bnd1":
                                // incomplete
                                sec.Object = br.ReadStruct<Pane>();
                                break;
                            case "txt1":
                                var txtPane = br.ReadStruct<Pane>();
                                var txtBox = br.ReadStruct<TextBox>();
                                var str = "";
                                if (txtBox.string_length != 0) str = br.ReadCStringW();
                                sec.Object = new { txtPane, txtBox, str };
                                break;
                            case "pts1":
                            case "pas1":
                            case "pae1":
                            case "grs1":
                            case "gre1":
                                break;
                            case "grp1":
                                // incomplete
                                break;
                            case "usd1":
                                // incomplete
                                break;
                            default:
                                throw new NotSupportedException($"Unknown magic {sec.Magic}");
                        }
                    }
                }

                var txts = sections.Where(z => z.Magic == "txt1").Select(z => z.Object).ToList();
            }
        }
    }
}
