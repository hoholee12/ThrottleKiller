using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.Windows.Forms.DataVisualization.Charting;
using System.IO;

namespace WindowsFormsApplication5
{
    public partial class ThrottleKillerGUI : Form
    {
        public ThrottleKillerGUI()
        {
            InitializeComponent();

            this.Text = "ThrottleKillerGUI";
            tabControl1.TabPages[0].Text = "monitor";
            tabControl1.TabPages[1].Text = "profile";
            tabControl1.TabPages[2].Text = "settings";
            tabControl1.TabPages[3].Text = "extra";
            tabControl1.TabPages[4].Text = "info";

            label6.Text = "clean up auto generated lines and compact";
            label7.Text = "core shutdown";
            label8.Text = "delete CLK/XTU list and regenerate(must wait several minutes)";
            label20.Text = "pause core";
            button1.Text = "cleanup";
            button2.Text = "shutdown";
            button3.Text = "reset";
            button8.Text = "pause";

            label9.Text = "not Online.";

            label10.Text = "GUI program written by J.H.Lee";
            label11.Text = "Core program written by J.H.Lee";
            label12.Text = "as part of graduation project in 2020";

        }

        Series cpuclk;
        Series cpuload;
        Series cputemp;
        System.Timers.Timer timer;

        RequestMaker rm = new RequestMaker();

        const int size = 25;

        int[] clkarray = new int[size];
        int[] loadarray = new int[size];
        int[] temparray = new int[size];

        string location = null;

        int topspeed = 0;

        int upcount = 0;

        bool msgflag = false;

        bool paused = false;

        logs logWindow = null;
        clklist clklistWindow = null;


        ToolTip tt = new ToolTip();

        private void OnTimerCount(Object src, System.Timers.ElapsedEventArgs args) {

            if (upcount == 0) location = rm.query(4);
            upcount++;

            

            //move array
            for (int i = 1; i < size; i++)
            {
                clkarray[i - 1] = clkarray[i];
                loadarray[i - 1] = loadarray[i];
                temparray[i - 1] = temparray[i];
            }

            //fetch info
            string[] info = rm.query(0).Split();
            clkarray[size - 1] = int.Parse(info[0]);
            loadarray[size - 1] = int.Parse(info[1]);
            temparray[size - 1] = int.Parse(info[2]);

            topspeed = int.Parse(rm.query(5));
            
            //this is to modify gui thread from other thread(timer)
            chart1.Invoke((MethodInvoker)delegate {

                chart1.ChartAreas[0].AxisY.Maximum = topspeed;

                cpuclk.Points.Clear();
                cpuload.Points.Clear();
                cputemp.Points.Clear();
                
                label1.Text = info[3];
                if (int.Parse(info[4]) != 0)
                {
                    label2.ForeColor = Color.Red;
                }
                else 
                {
                    label2.ForeColor = Color.Gray;
                }
                if (int.Parse(info[5]) != 0)
                {
                    label3.ForeColor = Color.Red;
                }
                else
                {
                    label3.ForeColor = Color.Gray;
                }
                if (int.Parse(info[6]) != 0)
                {
                    label4.ForeColor = Color.Red;
                }
                else
                {
                    label4.ForeColor = Color.Gray;
                }
                label2.Text = "newlist " + int.Parse(info[4]);
                label3.Text = "throttle " + int.Parse(info[5]);
                label4.Text = "distribute " + int.Parse(info[6]);
                if (info[7] == "True")
                {
                    if (!msgflag)
                        MessageBox.Show("xtucli missing or configured incorrectly.\nCore program will not operate");
                    msgflag = true;
                }
                else {
                    msgflag = false;
                }

                //display
                for (int i = 0; i < size; i++)
                {
                    cpuclk.Points.AddXY(i, clkarray[i]);
                    cpuload.Points.AddXY(i, loadarray[i] * topspeed / 100);
                    cputemp.Points.AddXY(i, temparray[i] * topspeed / 100);
                }

                cpuclk.LegendText = "CPU speed: " + clkarray[size - 1] + "MHz";
                cpuload.LegendText = "CPU load: " + loadarray[size - 1] + "%";
                cputemp.LegendText = "CPU temp: " + temparray[size - 1] + "°C";

                if (msgflag)
                    label9.Text = "error!";
                else
                {
                    if (paused)
                        label9.Text = "paused";
                    else
                        label9.Text = "running";
                }
            });
            
        
        }

        private void Form1_Load(object sender, EventArgs e)
        {

            chart1.ChartAreas[0].AxisX.Enabled = AxisEnabled.False;
            //chart1.ChartAreas[0].AxisY.Enabled = AxisEnabled.False;
            //chart1.ChartAreas[0].AxisX.LabelStyle.Enabled = false;
            //chart1.ChartAreas[0].AxisY.LabelStyle.Enabled = false;
            chart1.ChartAreas[0].BackColor = Color.Black;
            chart1.ChartAreas[0].AxisX.MajorGrid.LineColor = Color.Green;
            chart1.ChartAreas[0].AxisY.MajorGrid.LineColor = Color.Green;
            chart1.ChartAreas[0].AxisY.MajorGrid.Enabled = false;
            
            chart1.Series.Clear();
            cpuclk = chart1.Series.Add("CPU speed");
            cpuclk.ChartType = SeriesChartType.Spline;
            cpuclk.Color = Color.SkyBlue;

            cpuload = chart1.Series.Add("CPU load");
            cpuload.ChartType = SeriesChartType.Spline;
            cpuload.Color = Color.Orange;

            cputemp = chart1.Series.Add("CPU temp");
            cputemp.ChartType = SeriesChartType.Spline;
            cputemp.Color = Color.Red;

            label5.Text = "Core Optimization Timers:";
            label5.ForeColor = Color.Yellow;
            label5.BackColor = Color.Black;
            label1.Text = "";
            label2.Text = "newlist";
            label3.Text = "throttle";
            label4.Text = "distribute";
            label2.ForeColor = Color.Gray;
            label3.ForeColor = Color.Gray;
            label4.ForeColor = Color.Gray;


            button4.Text = "Refresh";
            button5.Text = "Apply";

            textBox1.Clear();
            textBox1.AppendText("click refresh after few seconds!");



            label13.Text = "general time(sec):";
            label14.Text = "newlist time(sec):";
            label15.Text = "throttle time(sec):";
            label16.Text = "newlist median(%):";
            label17.Text = "throttle median(%):";
            label18.Text = "thermal median(°C)";

            tt.SetToolTip(label13, "refresh rate for GUI communication / general update speed\n\nGUI refreshes every 5 seconds by default.");
            tt.SetToolTip(label14, "refresh rate for adding new applications to newlist");
            tt.SetToolTip(label15, "refresh rate for changing applications profile via throttle check");
            tt.SetToolTip(label16, "median cpu percentage for low cpu clock limit");
            tt.SetToolTip(label17, "median cpu usage percentage for distinguishing between high / low cpu usage");
            tt.SetToolTip(label18, "median temperature value for throttle check");

            tt.SetToolTip(textBox1, "use CLK/XTU list reference on the menu. first field is the process name, second field is the profile 'index'");

            label19.Text = "click refresh after few seconds!";

            button6.Text = "Refresh";
            button7.Text = "Apply";

            timer = new System.Timers.Timer();
            timer.Elapsed += new System.Timers.ElapsedEventHandler(OnTimerCount);
            timer.Interval = 5000;
            timer.Enabled = true;

            timer.Start();

        }

        private void tabPage1_Click(object sender, EventArgs e)
        {

        }

        private void chart1_Click(object sender, EventArgs e)
        {

        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            rm.query(1);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            rm.query(2);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            rm.query(3);
        }

        private void logsToolStripMenuItem_Click(object sender, EventArgs e)
        {
           
        }

        private void logsToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            
        }

        private void showLogToolStripMenuItem_Click(object sender, EventArgs e)
        {

            try
            {
                if (location == null) throw new Exception();
                logWindow = new logs(location);
                logWindow.Show();
            }
            catch {
                MessageBox.Show("not ready yet... try refreshing again!");
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void button4_Click(object sender, EventArgs e)
        {
            textBox1.Clear();
            try
            {
                textBox1.AppendText(File.ReadAllText(location + @"\special_programs.txt"));
            }
            catch {
                textBox1.AppendText("not ready yet... try refreshing again!");
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            while (true)
            {
                try
                {
                    File.WriteAllText(location + @"\special_programs.txt", textBox1.Text);
                    break;
                }
                catch { }
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            try {

                label13.Text = "general time(sec):";
                label14.Text = "newlist time(sec):";
                label15.Text = "throttle time(sec):";
                label16.Text = "newlist median(%):";
                label17.Text = "throttle median(%):";
                label18.Text = "thermal median(°C)";

                textBox2.Text = File.ReadAllText(location + @"\loop_delay.txt").Split('=')[1].Trim().Split()[0];
                textBox3.Text = File.ReadAllText(location + @"\newlist_cycle_delay.txt").Split('=')[1].Trim().Split()[0];
                textBox4.Text = File.ReadAllText(location + @"\boost_cycle_delay.txt").Split('=')[1].Trim().Split()[0];
                textBox5.Text = File.ReadAllText(location + @"\newlist_median.txt").Split('=')[1].Trim().Split()[0];
                textBox6.Text = File.ReadAllText(location + @"\throttle_median.txt").Split('=')[1].Trim().Split()[0];
                textBox7.Text = File.ReadAllText(location + @"\thermal_median.txt").Split('=')[1].Trim().Split()[0];


                label19.Text = "hover over the labels to see tooltips!";
            }
            catch(Exception){
                label19.Text = "not ready yet... try refreshing again!";
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(textBox2.Text) ||
                string.IsNullOrEmpty(textBox3.Text) ||
                string.IsNullOrEmpty(textBox4.Text) ||
                string.IsNullOrEmpty(textBox5.Text) ||
                string.IsNullOrEmpty(textBox6.Text) ||
                string.IsNullOrEmpty(textBox7.Text)) {
                    MessageBox.Show("Some boxes are empty!");
                    return;
            }
            //check for bad chars
            if (!textBox2.Text.All(char.IsDigit) ||
                !textBox3.Text.All(char.IsDigit) ||
                !textBox4.Text.All(char.IsDigit) ||
                !textBox5.Text.All(char.IsDigit) ||
                !textBox6.Text.All(char.IsDigit) ||
                !textBox7.Text.All(char.IsDigit))
            {
                MessageBox.Show("All values must be Integer!");
                return;
            }



            while (true) {
                try {
                    File.WriteAllText(location + @"\loop_delay.txt", "loop_delay = " + textBox2.Text);
                    File.WriteAllText(location + @"\newlist_cycle_delay.txt", "newlist_cycle_delay = " + textBox3.Text);
                    File.WriteAllText(location + @"\boost_cycle_delay.txt", "boost_cycle_delay = " + textBox4.Text);
                    File.WriteAllText(location + @"\newlist_median.txt", "newlist_median = " + textBox5.Text);
                    File.WriteAllText(location + @"\throttle_median.txt", "throttle_median = " + textBox6.Text);
                    File.WriteAllText(location + @"\thermal_median.txt", "thermal_median = " + textBox7.Text);
                    break;
                }
                catch { }
            }
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void label20_Click(object sender, EventArgs e)
        {

        }

        private void button8_Click(object sender, EventArgs e)
        {
            if (paused)
            {
                paused = false;
                label20.Text = "pause core";
                button8.Text = "pause";
                rm.query(7);
            }
            else{
                paused = true;
                label20.Text = "resume core";
                button8.Text = "resume";
                rm.query(6);
            }
        }

        private void showCLKXTUListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (location == null) throw new Exception();
                clklistWindow = new clklist(location, topspeed);
                clklistWindow.Show();
            }
            catch
            {
                MessageBox.Show("not ready yet... try refreshing again!");
            }
        }
    }
}
