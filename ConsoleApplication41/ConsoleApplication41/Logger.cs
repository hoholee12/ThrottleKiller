using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

using System.IO;
using System.Management;
using OpenHardwareMonitor.Hardware;

namespace ThrottleSchedulerService
{
    class Logger
    {
        string path;
        string cfgname;

        public Logger(string path, string cfgname) {
            this.path = path;
            this.cfgname = cfgname;
        }

        //logging
        public string WriteLog(string msg)
        {
            Console.WriteLine(msg);


            string folderpath = path + @"\logs";
            if (!Directory.Exists(folderpath)) Directory.CreateDirectory(folderpath);

            string filepath = path + @"\logs\" + cfgname +
                DateTime.Now.Date.ToShortDateString().Replace('/', '_') + ".txt";
            if (!File.Exists(filepath))
                using (StreamWriter sw = File.CreateText(filepath)) sw.WriteLine(DateTime.Now + ": " + msg);
            else
                using (StreamWriter sw = File.AppendText(filepath)) sw.WriteLine(DateTime.Now + ": " + msg);

            return msg;
        }

        public void WriteErr(string msg) {
            WriteLog("Error!: " + msg + "; system exited.");
            Environment.Exit(1);
        }
    }
}
