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

        public string getFullName() { return getPath() + @"\" + getName() + ".txt"; }

        public int getCount() {
            string[] count = File.ReadAllLines(getFullName());
            return count.Count();
        }

        public void completeWriteBack() {
            var temp = new List<string>();
            foreach (KeyValuePair<object, object> kvp in configList) {
                if ((Tkey == typeof(string)) && (Tval == typeof(int)))
                {
                    temp.Add((string)kvp.Key + " = " + (int)kvp.Value);
                }
                else if ((Tkey == typeof(int)) && (Tval == typeof(int)))
                {
                    temp.Add((int)kvp.Key + " = " + (int)kvp.Value);
                }
                else if ((Tkey == typeof(int)) && (Tval == typeof(float)))
                {
                    temp.Add((int)kvp.Key + " = " + (float)kvp.Value);
                }
                else if ((Tkey == typeof(int)) && (Tval == typeof(ProcessPriorityClass)))
                {
                    temp.Add((int)kvp.Key + " = " + (ProcessPriorityClass)kvp.Value);
                }
            }

            File.WriteAllLines(getFullName(), temp);

            checkFiles();   //reload settings
        }

        //changes can be appended to config file
        public void appendChanges(object key, object value) {
            try {
                string[] readfile = File.ReadAllLines(getFullName());

                //find @append
                bool appendexists = false;
                foreach(string line in readfile) {
                    
                    if (line == "@append") {
                        appendexists = true;
                        break;
                    }
                }

                if (!appendexists) File.AppendAllText(getFullName(), "@append");
                
                /////////////////////////////////////////////////////////////////
                if ((Tkey == typeof(string)) && (Tval == typeof(int)))
                {
                    File.AppendAllText(getFullName(), (string)key + " = " + (int)value);
                    log.WriteLog("appending: key = " + (string)key + ", value = " + (int)value);
                }
                else if ((Tkey == typeof(int)) && (Tval == typeof(int)))
                {
                    File.AppendAllText(getFullName(), (int)key + " = " + (int)value);
                    log.WriteLog("appending: key = " + (int)key + ", value = " + (int)value);
                }
                else if ((Tkey == typeof(int)) && (Tval == typeof(float)))
                {
                    File.AppendAllText(getFullName(), (int)key + " = " + (float)value);
                    log.WriteLog("appending: key = " + (int)key + ", value = " + (float)value);
                }
                else if ((Tkey == typeof(int)) && (Tval == typeof(ProcessPriorityClass)))
                {
                    File.AppendAllText(getFullName(), (int)key + " = " + ((ProcessPriorityClass)value).ToString().ToLower());
                    log.WriteLog("appending: key = " + (int)key + ", value = " + ((ProcessPriorityClass)value).ToString().ToLower());
                }

                
                /////////////////////////////////////////////////////////////////
            }
            catch (Exception) { }   //just in case file gets deleted before appending
            checkFiles();   //reload settings
        }

        //create config files if nonexistant
        public void checkFiles()
        {
            //they need to work in tandem
            if (!Directory.Exists(getPath())) { Directory.CreateDirectory(getPath()); log.WriteLog("create folder: " + getPath()); }
            if (!File.Exists(getFullName())) { File.WriteAllText(getFullName(), getContent()); log.WriteLog("create file: " + getFullName()); }
            if (getLastModifiedTime() != File.GetLastWriteTime(getFullName()).Ticks)
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
                using (var sr = File.OpenText(getFullName()))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (!line.Contains("=")) continue;
                        if ((line.IndexOf("=") > line.IndexOf("#")) && line.Contains("=") && line.Contains("#")) continue; //skip empty line
                        
                        string[] items = line.Split('=');
                        
                        string a = items[0].Trim();
                        string b = items[1].Split('#')[0].Trim();

                        if ((Tkey == typeof(string)) && (Tval == typeof(int)))
                        {
                            a = a.Replace("\'", "");
                            configList.Add(a, int.Parse(b));
                            log.WriteLog("reading: key = " + a + ", value = " + configList[a]);
                        }
                        else if ((Tkey == typeof(int)) && (Tval == typeof(int)))
                        {
                            configList.Add(int.Parse(a), int.Parse(b));
                            log.WriteLog("reading: key = " + int.Parse(a) + ", value = " + configList[int.Parse(a)]);
                        }
                        else if ((Tkey == typeof(int)) && (Tval == typeof(float)))
                        {
                            configList.Add(int.Parse(a), float.Parse(b));
                            log.WriteLog("reading: key = " + int.Parse(a) + ", value = " + configList[int.Parse(a)]);
                        }
                        else if ((Tkey == typeof(int)) && (Tval == typeof(ProcessPriorityClass)))
                        {
                            ProcessPriorityClass result = ProcessPriorityClass.Normal;
                            switch (b.ToLower())
                            {
                                case "idle": result = ProcessPriorityClass.Idle; break;
                                case "high": result = ProcessPriorityClass.High; break;
                                case "realtime": result = ProcessPriorityClass.RealTime; break;
                                case "normal": result = ProcessPriorityClass.Normal; break;
                                case "belownormal": result = ProcessPriorityClass.BelowNormal; break;
                                case "abovenormal": result = ProcessPriorityClass.AboveNormal; break;
                            }
                            configList.Add(int.Parse(a), result);
                            log.WriteLog("reading: key = " + int.Parse(a) + ", value = " + configList[int.Parse(a)].ToString());
                        }

                    }

                }

            }

        }
    };
}
