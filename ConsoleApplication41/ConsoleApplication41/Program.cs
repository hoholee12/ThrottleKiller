﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Diagnostics;

using System.IO;
using System.Management;
using OpenHardwareMonitor.Hardware;

using System.Runtime.InteropServices;

using System.Threading;

using System.Net;
using System.Net.Sockets;

namespace ThrottleSchedulerService
{
    class Program
    {
       
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        const int SW_HIDE = 0;
        const int SW_SHOW = 5;


        System.Timers.Timer timer = new System.Timers.Timer();
        essentials ess = new essentials();
        int msec = 1000;
        ThrottleScheduler ts;
        Stopwatch stopwatch;

        private void OnTimerCount(Object src, ElapsedEventArgs args)
        {
            //actual sync
            stopwatch.Restart();
            timer.Enabled = false;

            ts.mainflow();

            //actual sync
            stopwatch.Stop();
            double interval = msec - stopwatch.ElapsedMilliseconds;
            if (interval < 0.1) interval = 0.1;
            timer.Interval = interval;
            timer.Enabled = true;

            ess.WriteLog("system elapsed time = " + stopwatch.ElapsedMilliseconds + ", service timer interval = " + timer.Interval);
        }

        public void inputserver() {
            //IPC by TCP connection

            try
            {
                var listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 1999);
                listener.Start();

                var bytes = new Byte[256];
                string data = null;

                //always listen
                while (true)
                {
                    ess.WriteLog("waiting for input...");
                    var client = listener.AcceptTcpClient();
                    NetworkStream stream = client.GetStream();

                    int i;
                    while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                    {
                        data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                        ess.WriteLog("UI App requests: " + data);
                        byte[] output = null;
                        string temp = null;
                        switch (data)
                        {
                            case "sysinfo":
                                temp = ts.getSysInfo();
                                break;
                            case "reset":
                                temp = ts.reset();
                                break;
                            case "shutdown":
                                temp = ts.shutdown();
                                break;
                        }
                        output = System.Text.Encoding.ASCII.GetBytes(temp);
                        ess.WriteLog("sending output: " + temp);
                        stream.Write(output, 0, output.Length);


                    }
                    client.Close();
                }
            }
            catch (Exception) { }

        }

        public void service() {
            ts = new ThrottleScheduler(msec);
            stopwatch = Stopwatch.StartNew();

            timer.Elapsed += new ElapsedEventHandler(OnTimerCount);
            timer.Interval = msec;
            timer.Enabled = true;

            timer.Start();

            while (true) {
                inputserver();
            }


        }

        public static void Main() {
            
            var handle = GetConsoleWindow();

            //ShowWindow(handle, SW_HIDE); // To hide

            var hello = new Program();
            hello.service();
           
            //keep main thread running
            while (true) { Thread.Sleep(int.MaxValue); }
        
        }
    }

}