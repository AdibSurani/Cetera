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
                    case ".bflim":
                        BackgroundImage = BFLIM.Load(File.OpenRead(path));
                        break;
                    case ".bclim":
                        BackgroundImage = BCLIM.Load(File.OpenRead(path));
                        break;
                    case ".msbt":
                        var msbt = new MSBT(File.OpenRead(path));
                        label1.Text = string.Join("\r\n", msbt.Select(i => $"{i.Label}: {i.Text.Replace("\0", "\\0").Replace("\n", "\\n")}"));
                        break;
                    case ".arc":
                        var arc = new DARC(File.OpenRead(path));
                        label1.Text = string.Join("\r\n", arc.Select(i => $"{i.Path}: {i.Data.Length} bytes"));
                        var ent = arc.FirstOrDefault(i => Path.GetExtension(i.Path) == ".bclim");
                        if (ent != null) BackgroundImage = BCLIM.Load(new MemoryStream(ent.Data));
                        else
                        {
                            ent = arc.FirstOrDefault(i => Path.GetExtension(i.Path) == ".bflim");
                            if (ent != null) BackgroundImage = BFLIM.Load(new MemoryStream(ent.Data));
                        }
                        break;
                }
            };
        }
    }
}
