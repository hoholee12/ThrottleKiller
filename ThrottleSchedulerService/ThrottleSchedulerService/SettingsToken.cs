using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;

using System.IO;
using System.Management;
using OpenHardwareMonitor.Hardware;

namespace ThrottleSchedulerService
{
    //settings paths
    class SettingsToken
    {
        public IList<object> mconfig;   //file
        public IDictionary<object, object> configList;  //loaded config
        public Type Tkey, Tval;
        public Logger log;

        public SettingsToken(Logger log)
        {
            this.log = log;
            mconfig = new List<object>();
            configList = new Dictionary<object, object>();
            for (int i = 0; i < 4; i++) mconfig.Add(new long());
        }

        public void setPath(string path) { mconfig[0] = path; }
        public void setName(string name) { mconfig[1] = name; }
        public void setContent(string content) { mconfig[2] = content; }
        public void setLastModifiedTime(long lastModifiedTime) { mconfig[3] = lastModifiedTime; }

        public string getPath() { return (string)mconfig[0]; }
        public string getName() { return (string)mconfig[1]; }
        public string getContent() { return (string)mconfig[2]; }
        public long getLastModifiedTime() { return (long)mconfig[3]; }

        public string getFullName() { return getPath() + @"\" + getName(); }

        //create config files if nonexistant
        public void checkFiles()
        {

            if (!Directory.Exists(getPath())) { Directory.CreateDirectory(getPath()); log.WriteLog("create folder: " + getPath()); }
            else if (!File.Exists(getFullName())) { File.WriteAllText(getFullName(), getContent()); log.WriteLog("create file: " + getFullName()); }
            else if (getLastModifiedTime() != File.GetLastWriteTime(getFullName()).Ticks)
            {
                //update
                setLastModifiedTime(File.GetLastWriteTime(getFullName()).Ticks);
                if (File.GetLastWriteTime(getFullName()) != File.GetCreationTime(getFullName()))
                {
                    log.WriteLog("settings changed for: " + getName() + "!, reimporting...");
                }
                else
                {
                    log.WriteLog("importing settings for: " + getName() + "...");
                }
                //reset dictionary
                configList.Clear();
                //reread again
                using (StreamReader sr = File.OpenText(getFullName()))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line == "") continue; //skip empty line
                        
                        string[] items = line.Split('=');
                        
                        string a = items[0].Trim();
                        string b = items[1].Split('#')[0].Trim();

                        if ((Tkey == typeof(string)) && (Tval == typeof(int)))
                        {
                            a = a.Replace("\'", "");
                            configList.Add(a, int.Parse(b));
                            log.WriteLog("writing: key = " + a + ", value = " + configList[a]);
                        }
                        else if ((Tkey == typeof(int)) && (Tval == typeof(int)))
                        {
                            configList.Add(int.Parse(a), int.Parse(b));
                            log.WriteLog("writing: key = " + int.Parse(a) + ", value = " + configList[int.Parse(a)]);
                        }
                        else if ((Tkey == typeof(int)) && (Tval == typeof(float)))
                        {
                            configList.Add(int.Parse(a), float.Parse(b));
                            log.WriteLog("writing: key = " + int.Parse(a) + ", value = " + configList[int.Parse(a)]);
                        }
                        else if ((Tkey == typeof(int)) && (Tval == typeof(ProcessPriorityClass)))
                        {
                            ProcessPriorityClass result = ProcessPriorityClass.Normal;
                            switch (b)
                            {
                                case "idle": result = ProcessPriorityClass.Idle; break;
                                case "high": result = ProcessPriorityClass.High; break;
                                case "realtime": result = ProcessPriorityClass.RealTime; break;
                                case "normal": result = ProcessPriorityClass.Normal; break;
                                case "belownormal": result = ProcessPriorityClass.BelowNormal; break;
                                case "abovenormal": result = ProcessPriorityClass.AboveNormal; break;
                            }
                            configList.Add(int.Parse(a), result);
                            log.WriteLog("writing: key = " + int.Parse(a) + ", value = " + configList[int.Parse(a)].ToString());
                        }

                    }

                }

            }

        }
    };
}
