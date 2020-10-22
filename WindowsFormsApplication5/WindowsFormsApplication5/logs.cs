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
    public partial class logs : Form
    {
        string path = null;
        string filepath = null;
        long lastreadlength = 0;

        System.Timers.Timer timer;

        int timecount = 0;


        public logs(string path)
        {
            this.path = path;
            this.filepath = path + @"\logs\" + "xtu_scheduler_config" +
                DateTime.Now.Date.ToShortDateString().Replace('/', '_') + ".txt";
            InitializeComponent();
            this.Text = "logs";


            timer = new System.Timers.Timer();
            timer.Elapsed += new System.Timers.ElapsedEventHandler(OnTimerCount);
            timer.Interval = 100;
            timer.Enabled = true;

            timer.Start();

            textBox1.ScrollBars = ScrollBars.Vertical;
            
        }

        private void OnTimerCount(Object src, System.Timers.ElapsedEventArgs args) {
            
            textBox1.Invoke((MethodInvoker) delegate{
                textBox1.Clear();
                
                if (timecount == 0)
                {
                    timer.Interval = 5000;

                    textBox1.AppendText(File.ReadAllText(filepath));

                    lastreadlength = new FileInfo(filepath).Length;
                    return;
                }
                timecount++;

                
                var fileSize = new FileInfo(filepath).Length;
                if (fileSize > lastreadlength)
                {
                    using (var fs = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        fs.Seek(lastreadlength, SeekOrigin.Begin);
                        var buffer = new byte[1024];

                        var bytesRead = fs.Read(buffer, 0, buffer.Length);
                        lastreadlength += bytesRead;

                        textBox1.AppendText(ASCIIEncoding.UTF8.GetString(buffer, 0, bytesRead));
                    }
                }

            });
        }


        //dispose
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            textBox1.Dispose();
            Dispose();
        }


    }
}
