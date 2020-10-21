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
            button1.Text = "cleanup";
            button2.Text = "shutdown";
            button3.Text = "reset";

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

        const int size = 30;

        int[] clkarray = new int[size];
        int[] loadarray = new int[size];
        int[] temparray = new int[size];


        private void OnTimerCount(Object src, System.Timers.ElapsedEventArgs args) {
            
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
            
            //this is to modify gui thread from other thread(timer)
            chart1.Invoke((MethodInvoker)delegate {

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


                //display
                for (int i = 0; i < size; i++)
                {
                    cpuclk.Points.AddXY(i, clkarray[i]);
                    cpuload.Points.AddXY(i, loadarray[i] * 30);
                    cputemp.Points.AddXY(i, temparray[i] * 30);
                }

                cpuclk.LegendText = "CPU speed: " + clkarray[size - 1] + "MHz";
                cpuload.LegendText = "CPU load: " + loadarray[size - 1] + "%";
                cputemp.LegendText = "CPU temp: " + temparray[size - 1] + "°C";


                label9.Text = "running";
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

            label5.Text = "Core Optimization Values:";
            label5.ForeColor = Color.Yellow;
            label5.BackColor = Color.Black;
            label1.Text = "";
            label2.Text = "newlist";
            label3.Text = "throttle";
            label4.Text = "distribute";
            label2.ForeColor = Color.Gray;
            label3.ForeColor = Color.Gray;
            label4.ForeColor = Color.Gray;

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
    }
}
