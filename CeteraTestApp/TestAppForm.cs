using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Cetera;
using Cetera.Archive;
using Cetera.Compression;
using Cetera.Font;
using Cetera.Hardware;
using Cetera.Image;
using Cetera.IO;
using Cetera.Layout;
using Cetera.Text;

namespace CeteraTestApp
{
    public partial class TestAppForm : Form
    {
        void TestFile(string path)
        {
            path = path.ToLower();

            if (Path.GetFileName(path) == "code.bin")
            {
                label1.Text = OnionFS.DoStuff(File.ReadAllBytes(path));
            }

            switch (Path.GetExtension(path))
            {
                case ".bcfnt":
                    //BackgroundImage = new BCFNT(File.OpenRead(path)).bmp;
                    var fntz = new BCFNT(File.OpenRead(path));
                    BackgroundImage = fntz.bmp;
                    break;
                case ".xi":
                    BackgroundImage = new XI(File.OpenRead(path)).Image;
                    break;
                case ".bclim":
                case ".bflim":
                    BackgroundImage = new BXLIM(File.OpenRead(path)).Image;
                    break;
                case ".jtex":
                    BackgroundImage = new JTEX(File.OpenRead(path)).Image;
                    break;
                case ".msbt":
                    var msbt = new MSBT(File.OpenRead(path));
                    label1.Text = string.Join("\r\n", msbt.Select(i => $"{i.Label}: {i.Text.Replace("\0", "\\0").Replace("\n", "\\n")}"));
                    break;
                case ".arc":
                    var arc = new DARC(File.OpenRead(path));
                    label1.Text = string.Join("\r\n", arc.Select(i => $"{i.Path}: {i.Data.Length} bytes"));
                    var ent = arc.FirstOrDefault(i => i.Path.EndsWith("lim"));
                    if (ent != null) BackgroundImage = new BXLIM(new MemoryStream(ent.Data)).Image;
                    break;
            }
        }

        void TestXF(string fontpath, string str)
        {
            var xf = new XF(File.OpenRead(fontpath));
            var test = new Bitmap(2000, 200);
            using (var g = Graphics.FromImage(test))
            {
                float x = 5;
                foreach (var c in str)
                {
                    x = xf.Draw(c, Color.Black, g, x, 5);
                }
            }
            BackgroundImage = test;
        }

        void TestDaigasso()
        {
            var fnt = new BCFNT(GZip.OpenRead(@"C:\dbbp\unver\patch\font\Basic.bcfnt.gz"));
            var fntSym = new BCFNT(GZip.OpenRead(@"C:\dbbp\unver\patch\font\SisterSymbol.bcfnt.gz"));
            var fntRim = new BCFNT(GZip.OpenRead(@"C:\dbbp\unver\patch\font\BasicRim.bcfnt.gz"));
            var bmp = (Bitmap)Image.FromFile(@"C:\Users\Adib\Desktop\daigasso.png");
            fnt.SetColor(Color.Black);
            fntSym.SetColor(Color.Black);            
            using (var g = Graphics.FromImage(bmp))
            {
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bicubic;
                var s = "Please select the part to edit or create.\n\n　\uE10D　Lyric-related settings\n　\uE117　Save the song score\n　\uE100　Copy / delete / swap parts\n　\uE101　Song-related settings";
                s = "Slide a note from the note palette and enter it on the staff, slide the area where there is no note, you can select the range.\n　\uE10D　Lyric-related settings\n　\uE117　Save the song score\n　\uE100　Copy / delete / swap parts";
                s = "コードパレットからオリジナルコードを\nスライドして、楽譜上に入力してください。\n　[1]～[4]　　　[オ1]～[オ64]の表示を切り替え\n　[コード設定]　オリジナルコードの設定\n　[基本]　　　　基本コードに切り替え";
                s = "Please choose a chord from the\npalette and enter it into the score.\n　[1] - [4] Toggle display between [1]-[64]\n　[Chord setting] Original chord setting\n　[Basic] Switch to basic chord";
                float txtOffsetX = 32, txtOffsetY = 12;
                float x = 0, y = 0;
                foreach (var c in s)
                {
                    var fntToUse = fnt;
                    if (c >> 12 == 0xE)
                        fntToUse = fntSym;

                    var char_width = fntToUse.GetWidthInfo(c).char_width * 0.6f;
                    if (c == '\n' || x + char_width >= 336)
                    {
                        x = 0;
                        y += fnt.LineFeed * 0.6f;
                        if (c == '\n') continue;
                    }
                    fntToUse.Draw(c, g, x + txtOffsetX, y + txtOffsetY, 0.6f, 0.6f);
                    x += char_width;
                }

                txtOffsetX = 0;
                x = 0;
                y = 133;
                foreach (var c in s)
                {
                    var fntToUse = fntRim;

                    var char_width = fntToUse.GetWidthInfo(c).char_width * 0.87f;
                    if (c == '\n' || x + char_width >= 400)
                    {
                        x = 0;
                        y += fntToUse.LineFeed;
                        if (c == '\n') continue;
                    }
                    fntToUse.Draw(c, g, x + txtOffsetX, y + txtOffsetY, 0.87f, 0.87f);
                    x += char_width;
                }
            }
            BackgroundImage = bmp;
            BackgroundImage = fntSym.bmp;
        }

        public void TestLayout(string path)
        {
            var lyt = new BCLYT(File.OpenRead(path));
        }

        public TestAppForm()
        {
            InitializeComponent();
            BackgroundImageLayout = ImageLayout.None;
            AllowDrop = true;
            DragEnter += (s, e) => e.Effect = DragDropEffects.Copy;
            DragDrop += (s, e) => TestFile(((string[])e.Data.GetData(DataFormats.FileDrop)).First());

            //TestFile(@"C:\pikachu\Graphics\product\menu\common.arc\timg\topmenu_talk.bflim");
            //TestFile(@"C:\dqmrs3\Images&Menus\Layout\Menu\Upper_menu.arc\extracted\timg\txt_item.bclim");
            //TestFile(@"C:\Users\Adib\Desktop\blah\criware.xi");
            //TestFile(@"C:\Users\Adib\Desktop\MAJOR 3DS CLEANUP\dumps\traveler\ExtractedRomFS\ctr\ttp\ar\ar_mikoto.xi");
            //TestFile(@"C:\Users\Adib\Downloads\zor_cmbko4.jtex");
            //TestXFFont(@"C:\Users\Adib\Downloads\nrm_main.xf", "Time Travelers （タイムトラベラーズ Taimu Toraberazu） is a video game \"without a genre\" developed by Level-5");
            //TestLayout(@"C:\Users\Adib\Desktop\ms_normal.bclyt");
            //TestDaigasso();

            //using (var br = new BinaryReaderX(File.OpenRead(@"C:\Users\Adib\Downloads\zor_cmbko4.jtex")))
            //{
            //    br.ReadBytes(128);
            //    var tex = br.ReadBytes(65536);
            //    var settings = new Settings { Width = 512, Height = 128, Format = Format.ETC1A4 };
            //    var bmp = Common.Load(tex, settings);

            //    // Recompress
            //    var etc = Common.Save(bmp, settings);
            //    bmp = Common.Load(etc, settings);

            //    BackgroundImage = bmp;
            //}

            return;

            ////var lyt = new BCLYT(File.OpenRead(@"C:\Users\Adib\Desktop\ms_normal.bclyt"));
            //var lyt = new BCLYT(File.OpenRead(@"C:\Users\Adib\Desktop\TtrlTxt_U.bclyt"));
            ////return;

            ////var bytes = File.ReadAllBytes(@"C:\Users\Adib\Desktop\Basic.bcfnt").Skip(128).ToArray();
            ////var bytes = File.ReadAllBytes(@"C:\Users\Adib\Desktop\rocket.bcfnt").Skip(128).ToArray();
            ////BackColor = Color.Red;
            ////var bmp = ImageCommon.FromTexture(bytes, 128, 128 * 4, ImageCommon.Format.L4, ImageCommon.Swizzle.RightDown);
            ////BackgroundImage = bmp;
            ////bmp.Save(@"C:\Users\Adib\Desktop\rocket.png");
            ////var fnt = new BCFNT(@"C:\Users\Adib\Desktop\MAJOR 3DS CLEANUP\Basic.bcfnt");
            ////var fnt = new BCFNT(File.OpenRead(@"C:\Users\Adib\Desktop\rocket.bcfnt.gz"));
            ////var ms = new MemoryStream();
            ////new GZipStream(File.OpenRead(@"C:\Users\Adib\Desktop\pikachu.bcfnt.gz"), CompressionMode.Decompress).CopyTo(ms);
            ////ms.Position = 0;
            ////var fnt = new BCFNT(ms);
            ////BackgroundImage = fnt.bmp;

            ////return;


            //zzz.Save(@"C:\Users\Adib\Desktop\tmpscreen.png");
        }
    }
}
