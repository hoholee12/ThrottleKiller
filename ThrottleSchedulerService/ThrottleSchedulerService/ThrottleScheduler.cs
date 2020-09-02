﻿using System;
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
    class ThrottleScheduler
    {
        /*TODO: port everything from xtu_scheduler.ps1
         auto_powermanager core
             * 1. get list from db - DONE
             * 2. check hardware - DONE
         * 3. check list and apply accordingly
             * 4. check if list changed(reload if changed) - DONE
         * 5. check throttle
         * 6. check list and apply accordingly
         * 7. change setting if still throttles
             * 8. sleep - DONE
             * 9. loop to 2. - DONE
         * 
         */


        //needed for opm
        public class UpdateVisitor : IVisitor
        {
            public void VisitComputer(IComputer computer)
            {
                computer.Traverse(this);
            }
            public void VisitHardware(IHardware hardware)
            {
                hardware.Update();
                foreach (IHardware subHardware in hardware.SubHardware) subHardware.Accept(this);
            }
            public void VisitSensor(ISensor sensor) { }
            public void VisitParameter(IParameter parameter) { }
        }

        //monitor init
        Computer computer = new Computer();
        UpdateVisitor updateVisitor = new UpdateVisitor();
        ManagementObject obj = new ManagementObject("Win32_Processor.DeviceID='CPU0'");


        int getLoad()
        {
            obj.Get();
            return int.Parse(obj["LoadPercentage"].ToString());
        }
        int getCLK()
        {
            obj.Get();
            return int.Parse(obj["CurrentClockSpeed"].ToString());
        }
        int getTemp()
        {
            computer.Open();
            computer.CPUEnabled = true;
            computer.Accept(updateVisitor);
            for (int i = 0; i < computer.Hardware.Length; i++)
            {
                if (computer.Hardware[i].HardwareType == HardwareType.CPU)
                {
                    for (int j = 0; j < computer.Hardware[i].Sensors.Length; j++)
                    {
                        if (computer.Hardware[i].Sensors[j].SensorType == SensorType.Temperature)
                        {
                            int hello = int.Parse(computer.Hardware[i].Sensors[j].Value.ToString());
                            computer.Close();
                            return hello;
                        }
                    }
                }
            }
            computer.Close();
            return -1;
        }
        //for management wmi


        //config files
        public SettingsToken special_programs;
        public SettingsToken programs_running_cfg_cpu;
        public SettingsToken programs_running_cfg_xtu;
        public SettingsToken programs_running_cfg_nice;
        public SettingsToken loop_delay;
        public SettingsToken boost_cycle_delay;
        public SettingsToken ac_offset;
        public SettingsToken processor_guid_tweak;


        const string cfgname = @"xtu_scheduler_config";
        string path = AppDomain.CurrentDomain.BaseDirectory + cfgname;   //verbatim string literal @: for directory string

        //GUIDs
        public string guid0 = @"381b4222-f694-41f0-9685-ff5bb260df2e";		// you can change to any powerplan you want as default!
        public string guid1 = @"54533251-82be-4824-96c1-47b60b740d00";		// processor power management
        public string guid2 = @"bc5038f7-23e0-4960-96da-33abaf5935ec";		// processor high clockspeed limit
        public string guid3 = @"893dee8e-2bef-41e0-89c6-b55d0929964c";		// processor low clockspeed limit

        public string guid4 = @"44f3beca-a7c0-460e-9df2-bb8b99e0cba6";		// intel graphics power management
        public string guid5 = @"3619c3f2-afb2-4afc-b0e9-e7fef372de36";		// submenu of intel graphics power management

        //intel graphics settings
        //1 = Balanced(Maximum Battery Life is useless)
        //2 = Maximum Performance(seems to remove long term throttling...)

        public void checkMaxSpeed() { }
        public void cpuproc(string arg0, string arg1) { }
        public void xtuproc(string arg0) { }

        public void checkSettings()
        {
            checkFiles_myfiles();
        }



        //create config files if nonexistant
        public void checkFiles(SettingsToken st)
        {

            if (!Directory.Exists(st.getPath())) { Directory.CreateDirectory(st.getPath()); WriteLog("create folder: " + st.getPath()); }
            else if (!File.Exists(st.getFullName())) { File.WriteAllText(st.getFullName(), st.getContent()); WriteLog("create file: " + st.getFullName()); }
            else if (st.getLastModifiedTime() != File.GetLastWriteTime(st.getFullName()).Ticks)
            {
                //update
                st.setLastModifiedTime(File.GetLastWriteTime(st.getFullName()).Ticks);
                if (File.GetLastWriteTime(st.getFullName()) != File.GetCreationTime(st.getFullName()))
                {
                    WriteLog("settings changed for: " + st.getName() + "!, reimporting...");
                }
                else
                {
                    WriteLog("importing settings for: " + st.getName() + "...");
                }
                //reset dictionary
                st.configList.Clear();
                //reread again
                using (StreamReader sr = File.OpenText(st.getFullName()))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        string[] items = line.Split('=');
                        string a = items[0].Trim();
                        string b = items[1].Trim();

                        if ((st.Tkey == typeof(string)) && (st.Tval == typeof(int)))
                        {
                            st.configList.Add(a, int.Parse(b));
                        }
                        else if ((st.Tkey == typeof(int)) && (st.Tval == typeof(int)))
                        {
                            st.configList.Add(int.Parse(a), int.Parse(b));
                        }
                        else if ((st.Tkey == typeof(int)) && (st.Tval == typeof(float)))
                        {
                            st.configList.Add(int.Parse(a), float.Parse(b));
                        }
                        else if ((st.Tkey == typeof(int)) && (st.Tval == typeof(string)))
                        {
                            st.configList.Add(int.Parse(a), b);
                        }

                    }

                }

            }

        }

        //batch checkfiles
        public void checkFiles_myfiles()
        {
            checkFiles(special_programs);
            checkFiles(programs_running_cfg_cpu);
            checkFiles(programs_running_cfg_xtu);
            checkFiles(programs_running_cfg_nice);
            checkFiles(loop_delay);
            checkFiles(boost_cycle_delay);
            checkFiles(ac_offset);
        }

        //logging
        public void WriteLog(string msg)
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
        }

        public void initConfig()
        {

            //settings
            special_programs = new SettingsToken();
            programs_running_cfg_cpu = new SettingsToken();
            programs_running_cfg_xtu = new SettingsToken();
            programs_running_cfg_nice = new SettingsToken();
            loop_delay = new SettingsToken();
            boost_cycle_delay = new SettingsToken();
            ac_offset = new SettingsToken();
            processor_guid_tweak = new SettingsToken();


            //initialize paths
            special_programs.setPath(path);
            programs_running_cfg_cpu.setPath(path);
            programs_running_cfg_xtu.setPath(path);
            programs_running_cfg_nice.setPath(path);
            loop_delay.setPath(path);
            boost_cycle_delay.setPath(path);
            ac_offset.setPath(path);
            processor_guid_tweak.setPath(path);

            special_programs.setName("special_programs");
            programs_running_cfg_cpu.setName("programs_running_cfg_cpu");
            programs_running_cfg_xtu.setName("programs_running_cfg_xtu");
            programs_running_cfg_nice.setName("programs_running_cfg_nice");
            loop_delay.setName("loop_delay");
            boost_cycle_delay.setName("boost_cycle_delay");
            ac_offset.setName("ac_offset");
            processor_guid_tweak.setName("processor_guid_tweak");

            //initialize contents
            special_programs.setContent(
@"'jdownloader2' = 0
'github' = 0
'steam' = 0
'origin' = 0
'mbam' = 0
'shellexperiencehost' = 0
'svchost' = 0
'subprocess' = 0
'gtavlauncher' = 0
'acad' = 1
'launcher' = 7
'tesv' = 1
'fsx' = 1
'Journey' = 1
'nullDC' = 1
'pcsxr' = 1
'ppsspp' = 1
'Project64' = 1
'ace7game' = 1
'pcars' = 1
'doom' = 1
'gtaiv' = 4
'nfs' = 1
'dirt' = 1
'grid' = 1
'studio64' = 2
'arcade64' = 2
'djmax' = 2
'streaming_client' = 2
'moonlight' = 2
'pcsx2' = 2
'dolphin' = 2
'vmware-vmx' = 2
'virtualbox' = 2
'dosbox' = 2
'cemu' = 2
'citra' = 2
'rpcs3' = 7
'drt' = 3
'dirtrally2' = 3
'tombraider' = 3
'rottr' = 3
'bf' = 4
'gta5' = 4
'borderlands2' = 4
'katamari' = 4
'bold' = 4
'setup' = 5
'minecraft' = 5
'cl' = 5
'link' = 5
'ffmpeg' = 5
'7z' = 5
'vegas' = 5
'bandizip' = 5
'handbrake' = 5
'mpc-hc' = 5
'consoleapplication' = 5
'macsfancontrol' = 6
'lubbosfancontrol' = 6
'bootcamp' = 6
'obs' = 6
'remoteplay' = 6
'discord' = 6");
            programs_running_cfg_cpu.setContent(
@"0 = 65
1 = 98
2 = 100
3 = 65
4 = 95
5 = 100
6 = 65
7 = 100");
            programs_running_cfg_xtu.setContent(
@"0 = 7.5
1 = 5.5
2 = 4.5
3 = 7.5
4 = 6.5
5 = 4.5
6 = 7.5
7 = 7.5");
            programs_running_cfg_nice.setContent(
@"0 = idle
1 = high
2 = high
3 = high
4 = high
5 = idle
6 = realtime
7 = high");
            loop_delay.setContent(@"loop_delay = 5");
            boost_cycle_delay.setContent(@"boost_cycle_delay = 6");
            ac_offset.setContent(@"ac_offset = 1");
            processor_guid_tweak.setContent(@"
06cadf0e-64ed-448a-8927-ce7bf90eb35d = 30			# processor high threshold; lower this for performance
0cc5b647-c1df-4637-891a-dec35c318583 = 100
12a0ab44-fe28-4fa9-b3bd-4b64f44960a6 = 15			# processor low threshold; upper this for batterylife
40fbefc7-2e9d-4d25-a185-0cfd8574bac6 = 1
45bcc044-d885-43e2-8605-ee0ec6e96b59 = 100
465e1f50-b610-473a-ab58-00d1077dc418 = 2
4d2b0152-7d5c-498b-88e2-34345392a2c5 = 15
893dee8e-2bef-41e0-89c6-b55d0929964c = 5			# processor low clockspeed limit
94d3a615-a899-4ac5-ae2b-e4d8f634367f = 1
bc5038f7-23e0-4960-96da-33abaf5935ec = 100          # processor high clockspeed limit
ea062031-0e34-4ff1-9b6d-eb1059334028 = 100");

            //set key value pair type
            special_programs.Tkey = typeof(string);
            special_programs.Tval = typeof(int);
            programs_running_cfg_cpu.Tkey = typeof(int);
            programs_running_cfg_cpu.Tval = typeof(int);
            programs_running_cfg_xtu.Tkey = typeof(int);
            programs_running_cfg_xtu.Tval = typeof(float);
            programs_running_cfg_nice.Tkey = typeof(int);
            programs_running_cfg_nice.Tval = typeof(string);
            loop_delay.Tkey = typeof(string);
            loop_delay.Tval = typeof(int);
            boost_cycle_delay.Tkey = typeof(string);
            boost_cycle_delay.Tval = typeof(int);
            ac_offset.Tkey = typeof(string);
            ac_offset.Tval = typeof(int);
            processor_guid_tweak.Tkey = typeof(string);
            processor_guid_tweak.Tval = typeof(int);


            //batch create first/read settings
            checkFiles_myfiles();


            //and then get last modified date
            special_programs.setLastModifiedTime(File.GetLastWriteTime(special_programs.getFullName()).Ticks);
            programs_running_cfg_cpu.setLastModifiedTime(File.GetLastWriteTime(programs_running_cfg_cpu.getFullName()).Ticks);
            programs_running_cfg_xtu.setLastModifiedTime(File.GetLastWriteTime(programs_running_cfg_xtu.getFullName()).Ticks);
            programs_running_cfg_nice.setLastModifiedTime(File.GetLastWriteTime(programs_running_cfg_nice.getFullName()).Ticks);
            loop_delay.setLastModifiedTime(File.GetLastWriteTime(loop_delay.getFullName()).Ticks);
            boost_cycle_delay.setLastModifiedTime(File.GetLastWriteTime(boost_cycle_delay.getFullName()).Ticks);
            ac_offset.setLastModifiedTime(File.GetLastWriteTime(ac_offset.getFullName()).Ticks);
            processor_guid_tweak.setLastModifiedTime(File.GetLastWriteTime(processor_guid_tweak.getFullName()).Ticks);

        }

        public ThrottleScheduler()
        {
            initConfig();
        }


        //start main loop
        public void mainflow()
        {
            checkSettings();
            WriteLog("clk:" + getCLK() + ", load:" + getLoad() + ", temp:" + getTemp());

        }

        
    }

}
