using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;

namespace ThrottleSchedulerService
{
    class essentials
    {
        public void WriteLog(string msg) {
            //Console.WriteLine(msg);

            string folderpath = AppDomain.CurrentDomain.BaseDirectory + @"\logs";
            if (!Directory.Exists(folderpath)) Directory.CreateDirectory(folderpath);

            string filepath = AppDomain.CurrentDomain.BaseDirectory + @"\logs\" +
                DateTime.Now.Date.ToShortDateString().Replace('/', '_') + ".txt";
            if (!File.Exists(filepath)) 
                using (StreamWriter sw = File.CreateText(filepath)) sw.WriteLine(DateTime.Now + ": " + msg);
            else
                using (StreamWriter sw = File.AppendText(filepath)) sw.WriteLine(DateTime.Now + ": " + msg);
        }
    }
}
