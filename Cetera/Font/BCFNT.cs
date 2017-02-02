﻿using Cetera.IO;
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

namespace Cetera.Font
{
    public class BCFNT
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        [DebuggerDisplay("[{left}, {glyph_width}, {char_width}]")]
        public struct CharWidthInfo
        {
            public sbyte left;
            public byte glyph_width;
            public byte char_width;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct CFNT
        {
            public uint magic;
            public ushort endianness;
            public short header_size;
            public int version;
            public int file_size;
            public int num_blocks;
        };

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct FINF
        {
            public uint magic;
            public int section_size;
            public byte font_type;
            public byte line_feed;
            public short alter_char_index;
            public CharWidthInfo default_width;
            public byte encoding;
            public int tglp_offset;
            public int cwdh_offset;
            public int cmap_offset;
            public byte height;
            public byte width;
            public byte ascent;
            public byte reserved;
        };

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct TGLP
        {
            public uint magic;
            public int section_size;
            public byte cell_width;
            public byte cell_height;
            public byte baseline_position;
            public byte max_character_width;
            public int sheet_size;
            public short num_sheets;
            public short sheet_image_format;
            public short num_columns;
            public short num_rows;
            public short sheet_width;
            public short sheet_height;
            public int sheet_data_offset;
        };

        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
        public struct CMAP
        {
            public uint magic;
            public int section_size;
            public char code_begin;
            public char code_end;
            public short mapping_method;
            public short reserved;
            public int next_offset;
        };

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct CWDH
        {
            public uint magic;
            public int section_size;
            public short start_index;
            public short end_index;
            public int next_offset;
        };

        FINF finf;
        TGLP tglp;
        public Bitmap bmp;
        ImageAttributes attr = new ImageAttributes();
        List<CharWidthInfo> lstCWDH = new List<CharWidthInfo>();
        Dictionary<char, int> dicCMAP = new Dictionary<char, int>();

        public CharWidthInfo GetWidthInfo(char c) => lstCWDH[GetIndex(c)];
        public int LineFeed => finf.line_feed;

        int GetIndex(char c)
        {
            int result;
            var success = dicCMAP.TryGetValue(c, out result) || dicCMAP.TryGetValue('?', out result);
            return result;
        }

        public void SetColor(Color color)
        {
            attr.SetColorMatrix(new ColorMatrix(new[]
            {
                new[] { color.R / 255f, 0, 0, 0, 0 },
                new[] { 0, color.G / 255f, 0, 0, 0 },
                new[] { 0, 0, color.B / 255f, 0, 0 },
                new[] { 0, 0, 0, 1f, 0 },
                new[] { 0, 0, 0, 0, 1f }
            }));
        }

        public void Draw(char c, Graphics g, float x, float y, float scaleX, float scaleY)
        {
            var index = GetIndex(c);
            var widthInfo = lstCWDH[index];

            int cellsPerSheet = tglp.num_rows * tglp.num_columns;
            int sheetNum = index / cellsPerSheet;
            int cellRow = (index % cellsPerSheet) / tglp.num_columns;
            int cellCol = index % tglp.num_columns;
            int xOffset = cellCol * (tglp.cell_width + 1);
            int yOffset = sheetNum * tglp.sheet_height + cellRow * (tglp.cell_height + 1);

            g.DrawImage(bmp,
                new[] { new PointF(x + widthInfo.left * scaleX, y),
                    new PointF(x + (widthInfo.left + widthInfo.glyph_width) * scaleX, y),
                    new PointF(x + widthInfo.left * scaleX, y + tglp.cell_height * scaleY) },
                new RectangleF(xOffset + 1, yOffset + 1, widthInfo.glyph_width, tglp.cell_height),
                GraphicsUnit.Pixel,
                attr);
        }

        public BCFNT(Stream input)
        {
            using (var br = new BinaryReaderX(input))
            {
                var cfnt = br.ReadStruct<CFNT>();
                finf = br.ReadStruct<FINF>();

                // read TGLP
                br.BaseStream.Position = finf.tglp_offset - 8;
                tglp = br.ReadStruct<TGLP>();

                // read image data
                br.BaseStream.Position = tglp.sheet_data_offset;
                int width = tglp.sheet_width;
                int height = tglp.sheet_height * tglp.num_sheets;
                var bytes = br.ReadBytes(tglp.sheet_size * tglp.num_sheets);
                var colors = Image.Common.GetColorsFromTexture(bytes, tglp.sheet_image_format);
                bmp = Image.Common.Load(colors, width, height, Image.Common.Swizzle.Default, true);

                // read CWDH
                for (int offset = finf.cwdh_offset; offset != 0; )
                {
                    br.BaseStream.Position = offset - 8;
                    var cwdh = br.ReadStruct<CWDH>();
                    for (int i = cwdh.start_index; i <= cwdh.end_index; i++)
                        lstCWDH.Add(br.ReadStruct<CharWidthInfo>());
                    offset = cwdh.next_offset;
                }

                // read CMAP
                for (int offset = finf.cmap_offset; offset != 0;)
                {
                    br.BaseStream.Position = offset - 8;
                    var cmap = br.ReadStruct<CMAP>();
                    switch (cmap.mapping_method)
                    {
                        case 0:
                            var charOffset = br.ReadUInt16();
                            for (char i = cmap.code_begin; i <= cmap.code_end; i++)
                                dicCMAP[i] = i - cmap.code_begin + charOffset;
                            break;
                        case 1:
                            for (char i = cmap.code_begin; i <= cmap.code_end; i++)
                                dicCMAP[i] = br.ReadUInt16();
                            break;
                        case 2:
                            var n = br.ReadUInt16();
                            for (int i = 0; i < n; i++)
                                dicCMAP[br.ReadChar()] = br.ReadUInt16();
                            break;
                        default:
                            throw new Exception("Unsupported mapping method");
                    }
                    offset = cmap.next_offset;
                }
            }
        }
    }
}