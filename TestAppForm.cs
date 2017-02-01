using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Cetera
{
    public partial class TestAppForm : Form
    {
        public TestAppForm()
        {
            InitializeComponent();
            BackgroundImageLayout = ImageLayout.None;
            //BackgroundImage = BFLIM.Load(@"C:\pikachu\Graphics\product\menu\common.arc\timg\topmenu_talk.bflim");
            AllowDrop = true;
            DragEnter += (s, e) => e.Effect = DragDropEffects.Copy;
            DragDrop += (s, e) =>
            {
                var path = ((string[])e.Data.GetData(DataFormats.FileDrop)).First().ToLower();
                if (Path.GetFileName(path) == "code.bin")
                {
                    label1.Text = OnionFS.DoStuff(File.ReadAllBytes(path));
                }

                switch(Path.GetExtension(path))
                {
                    case ".bcfnt":
                        //BackgroundImage = new BCFNT(File.OpenRead(path)).bmp;
                        var fntz = new BCFNT(File.OpenRead(path));
                        BackgroundImage = fntz.bmp;
                        break;
                    case ".xi":
                        BackgroundImage = XI.Load(File.OpenRead(path));
                        break;
                    case ".bclim":
                    case ".bflim":
                        BackgroundImage = BXLIM.Load(File.OpenRead(path));
                        break;
                    case ".msbt":
                        var msbt = new MSBT(File.OpenRead(path));
                        label1.Text = string.Join("\r\n", msbt.Select(i => $"{i.Label}: {i.Text.Replace("\0", "\\0").Replace("\n", "\\n")}"));
                        break;
                    case ".arc":
                        var arc = new DARC(File.OpenRead(path));
                        label1.Text = string.Join("\r\n", arc.Select(i => $"{i.Path}: {i.Data.Length} bytes"));
                        var ent = arc.FirstOrDefault(i => i.Path.EndsWith("lim"));
                        if (ent != null) BackgroundImage = BXLIM.Load(new MemoryStream(ent.Data));
                        break;
                }
                //System.Diagnostics.Debug.WriteLine(label1.Text);
            };

            new XF(File.OpenRead(@"C:\Users\Adib\Downloads\nrm_main.xf"));
            return;
            //var lyt = new BCLYT(File.OpenRead(@"C:\Users\Adib\Desktop\ms_normal.bclyt"));
            var lyt = new BCLYT(File.OpenRead(@"C:\Users\Adib\Desktop\TtrlTxt_U.bclyt"));
            //return;

            //var bytes = File.ReadAllBytes(@"C:\Users\Adib\Desktop\Basic.bcfnt").Skip(128).ToArray();
            //var bytes = File.ReadAllBytes(@"C:\Users\Adib\Desktop\rocket.bcfnt").Skip(128).ToArray();
            //BackColor = Color.Red;
            //var bmp = ImageCommon.FromTexture(bytes, 128, 128 * 4, ImageCommon.Format.L4, ImageCommon.Swizzle.RightDown);
            //BackgroundImage = bmp;
            //bmp.Save(@"C:\Users\Adib\Desktop\rocket.png");
            //var fnt = new BCFNT(@"C:\Users\Adib\Desktop\MAJOR 3DS CLEANUP\Basic.bcfnt");
            //var fnt = new BCFNT(File.OpenRead(@"C:\Users\Adib\Desktop\rocket.bcfnt.gz"));
            //var ms = new MemoryStream();
            //new GZipStream(File.OpenRead(@"C:\Users\Adib\Desktop\pikachu.bcfnt.gz"), CompressionMode.Decompress).CopyTo(ms);
            //ms.Position = 0;
            //var fnt = new BCFNT(ms);
            //BackgroundImage = fnt.bmp;

            //return;

            var fnt = new BCFNT(File.OpenRead(@"C:\Users\Adib\Desktop\dbbpfonts\Basic.bcfnt"));
            var fntSym = new BCFNT(File.OpenRead(@"C:\Users\Adib\Desktop\dbbpfonts\SisterSymbol.bcfnt"));
            var fntRim = new BCFNT(File.OpenRead(@"C:\Users\Adib\Desktop\dbbpfonts\BasicRim.bcfnt"));
            //var zzz = new Bitmap(1000, 1000);
            var zzz = (Bitmap)Image.FromFile(@"C:\Users\Adib\Desktop\daigasso.png");
            //var zzz = new Bitmap(200, 200);
            fnt.SetColor(Color.Black);
            fntSym.SetColor(Color.Black);
            using (var g = Graphics.FromImage(zzz))
            {
                //g.DrawImage(Image.FromFile(@"C:\Users\Adib\Desktop\daigasso.png"), 0, 0, 200, 50);
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
                    fntToUse.Draw(c, g, x + txtOffsetX, y + txtOffsetY, 0.6f);
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
                    fntToUse.Draw(c, g, x + txtOffsetX, y + txtOffsetY, 0.87f);
                    x += char_width;
                }
            }
            BackgroundImage = zzz;
            zzz.Save(@"C:\Users\Adib\Desktop\tmpscreen.png");
        }
    }
}
