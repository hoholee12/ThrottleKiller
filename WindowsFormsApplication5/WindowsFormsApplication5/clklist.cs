using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.IO;

namespace WindowsFormsApplication5
{
    public partial class clklist : Form
    {

        string path = null;
        string clkpath = null;
        string xtupath = null;


        public clklist(string path, int topspeed)
        {
            this.path = path;
            this.clkpath = path + @"\generatedCLK.txt";
            this.xtupath = path + @"\generatedXTU.txt";

            InitializeComponent();
            this.Text = "CLK/XTU list";

            List<int> clklist = new List<int>();
            List<float> xtulist = new List<float>();

            using (var sr = File.OpenText(clkpath))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (!line.Contains("=")) continue;
                    if ((line.IndexOf("=") > line.IndexOf("#")) && line.Contains("=") && line.Contains("#")) continue; //skip empty line

                    string[] items = line.Split('=');

                    string a = items[0].Trim();
                    string b = items[1].Split('#')[0].Trim();

                    if (clklist.Count() == 0) clklist.Add(topspeed);
                    else clklist.Add(int.Parse(b));
                }
                
            }
            using (var sr2 = File.OpenText(xtupath))
            {
                string line2;
                while ((line2 = sr2.ReadLine()) != null)
                {
                    if (!line2.Contains("=")) continue;
                    if ((line2.IndexOf("=") > line2.IndexOf("#")) && line2.Contains("=") && line2.Contains("#")) continue; //skip empty line

                    string[] items = line2.Split('=');

                    string a = items[0].Trim();
                    string b = items[1].Split('#')[0].Trim();

                    xtulist.Add(float.Parse(b));
                }
                xtulist.Sort();
            }


            textBox1.AppendText("index\tCLK\tXTU" + Environment.NewLine);
            textBox1.AppendText("=============================" + Environment.NewLine);
            int size = clklist.Count();
            while (clklist.Count() != 0) {
                textBox1.AppendText((size - clklist.Count()).ToString() + ":\t" + clklist[0] + "MHz\t=" + xtulist[0] * 100 + "MHz" + Environment.NewLine);
                clklist.RemoveAt(0);
                xtulist.RemoveAt(0);
            }

            textBox1.AppendText(Environment.NewLine + "use this reference to tweak the 'profile' tab. first field is the process name, second field is the profile 'index'");

        }

    }
}
