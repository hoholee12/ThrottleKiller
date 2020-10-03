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
    class SettingsManager
    {

        public long perfx = 0;   //slowdown io usage

        //config files
        public SettingsToken special_programs;
        public SettingsToken programs_running_cfg_cpu;
        public SettingsToken programs_running_cfg_xtu;
        public SettingsToken programs_running_cfg_nice;
        public SettingsToken loop_delay;
        public SettingsToken boost_cycle_delay;
        public SettingsToken ac_offset;
        public SettingsToken processor_guid_tweak;
        public SettingsToken generatedCLK;
        public SettingsToken throttle_median;

        //BASE DIRECTORY
        public string cfgname = @"xtu_scheduler_config";
        public string path; //later

        public Logger log;  //init from other side

        //=======================================================================
        //why on settingsmanager? -  its the most frequently passed around object
        public int base_msec = 0;
        
        public int accumulated_msec = 0;
        public int target_msec = 0;

        public int bc_acc_msec = 0;
        public int bc_target_msec = 0;

        public bool timeSync { get; set; }   //run myself on true
        public bool throttleSync { get; set; }   //throttle delay sync


        //=======================================================================



        public void initTimeSync(int msec) {
            try
            {
                base_msec = msec;
                target_msec = int.Parse(loop_delay.configList["loop_delay"].ToString()) * 1000;
                bc_target_msec = int.Parse(boost_cycle_delay.configList["boost_cycle_delay"].ToString()) * 1000;
            }
            catch (Exception) {
                log.WriteErr("config file bug");
            }
        }

        public void startThrottleSync() {
            //state1 to state2
            if (!throttleSync && bc_acc_msec == 0)
            {
                throttleSync = true;
            }
        }
        public bool checkThrottleSync() {
            //state4 to state1
            if (throttleSync && bc_acc_msec != 0)
            {
                bc_acc_msec = 0;
                throttleSync = false;
                return true;
            }
            else
            {
                return false;
            }
        }

        public void updateTimeSync() {
            try
            {
                //general timer
                if (accumulated_msec % target_msec == 0)
                {
                    timeSync = true;
                    target_msec = int.Parse(loop_delay.configList["loop_delay"].ToString()) * 1000;
                }
                else {
                    timeSync = false;
                }
                accumulated_msec += base_msec;
                

                //throttle timer
                /* states:
                 * 1. throttleSync = false, bc_acc_msec = 0: (launch)
                 * 2. throttleSync = true, bc_acc_msec = 0:
                 *      start timer:
                 *          accumulate bc_acc_msec
                 * 3. throttleSync = false, bc_acc_msec != 0:
                 *      check sync:
                 *          set throttleSync = true
                 *          set bc_acc_msec = 0
                 *      else:
                 *          acumulate bc_acc_msec
                 * 4. throttleSync = true, bc_acc_msec != 0: (exit)
                 *      to: state 1
                 */

                //state 2
                if (throttleSync && bc_acc_msec == 0) {
                    log.WriteLog("start of throttle timer");
                    throttleSync = false;
                    bc_acc_msec += base_msec;
                }
                //state 3
                else if(!throttleSync && bc_acc_msec != 0){
                    if (bc_acc_msec % bc_target_msec == 0)
                    {
                        throttleSync = true;
                        bc_target_msec = int.Parse(boost_cycle_delay.configList["boost_cycle_delay"].ToString()) * 1000;
                    }
                    else
                    {
                        bc_acc_msec += base_msec;
                    }
                }

            }
            catch (Exception) {
                log.WriteErr("config file bug");
            }
        }

        public void checkMaxSpeed() { }
        public void cpuproc(string arg0, string arg1) { }
        public void xtuproc(string arg0) { }


        public bool checkPowerCFGFlag { get; set; }

        /*
         * sm.throttleMode:
         * 0 -> nein
         * 1 -> cpu(cpu usage under 80)
         * 2 -> gpu(cpu usage over 80)
         * cpu is more important than gpu
         */
        public int throttleMode { get; set; }


        //batch checkfiles
        public void batchCheckFiles()
        {
            checkPowerCFGFlag = false;
            special_programs.checkFiles();
            programs_running_cfg_cpu.checkFiles();
            programs_running_cfg_xtu.checkFiles();
            programs_running_cfg_nice.checkFiles();
            loop_delay.checkFiles();
            boost_cycle_delay.checkFiles();
            ac_offset.checkFiles();
            checkPowerCFGFlag = processor_guid_tweak.checkFiles();
            generatedCLK.checkFiles();
            throttle_median.checkFiles();
        }

        public void initConfig(Logger log) {     
            //get log object
            this.log = log;


            //settings
            special_programs = new SettingsToken(log);
            programs_running_cfg_cpu = new SettingsToken(log);
            programs_running_cfg_xtu = new SettingsToken(log);
            programs_running_cfg_nice = new SettingsToken(log);
            loop_delay = new SettingsToken(log);
            boost_cycle_delay = new SettingsToken(log);
            ac_offset = new SettingsToken(log);
            processor_guid_tweak = new SettingsToken(log);
            generatedCLK = new SettingsToken(log);
            throttle_median = new SettingsToken(log);
            
            
            
            //initialize paths
            special_programs.setPath(path);
            programs_running_cfg_cpu.setPath(path);
            programs_running_cfg_xtu.setPath(path);
            programs_running_cfg_nice.setPath(path);
            loop_delay.setPath(path);
            boost_cycle_delay.setPath(path);
            ac_offset.setPath(path);
            processor_guid_tweak.setPath(path);
            generatedCLK.setPath(path);
            throttle_median.setPath(path);

            special_programs.setName("special_programs");
            programs_running_cfg_cpu.setName("programs_running_cfg_cpu");
            programs_running_cfg_xtu.setName("programs_running_cfg_xtu");
            programs_running_cfg_nice.setName("programs_running_cfg_nice");
            loop_delay.setName("loop_delay");
            boost_cycle_delay.setName("boost_cycle_delay");
            ac_offset.setName("ac_offset");
            processor_guid_tweak.setName("processor_guid_tweak");
            generatedCLK.setName("generatedCLK");
            throttle_median.setName("throttle_median");

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
            loop_delay.setContent(@"loop_delay = 1");
            boost_cycle_delay.setContent(@"boost_cycle_delay = 5");
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
            generatedCLK.setContent("");
            throttle_median.setContent(@"throttle_median = 80");

            //set key value pair type
            special_programs.Tkey = typeof(string);
            special_programs.Tval = typeof(int);
            programs_running_cfg_cpu.Tkey = typeof(int);
            programs_running_cfg_cpu.Tval = typeof(int);
            programs_running_cfg_xtu.Tkey = typeof(int);
            programs_running_cfg_xtu.Tval = typeof(float);
            programs_running_cfg_nice.Tkey = typeof(int);
            programs_running_cfg_nice.Tval = typeof(ProcessPriorityClass);
            loop_delay.Tkey = typeof(string);
            loop_delay.Tval = typeof(int);
            boost_cycle_delay.Tkey = typeof(string);
            boost_cycle_delay.Tval = typeof(int);
            ac_offset.Tkey = typeof(string);
            ac_offset.Tval = typeof(int);
            processor_guid_tweak.Tkey = typeof(string);
            processor_guid_tweak.Tval = typeof(int);
            generatedCLK.Tkey = typeof(int);
            generatedCLK.Tval = typeof(int);
            throttle_median.Tkey = typeof(string);
            throttle_median.Tval = typeof(int);

            //batch create first/read settings
            batchCheckFiles();


            //and then get last modified date
            special_programs.setLastModifiedTime(File.GetLastWriteTime(special_programs.getFullName()).Ticks);
            programs_running_cfg_cpu.setLastModifiedTime(File.GetLastWriteTime(programs_running_cfg_cpu.getFullName()).Ticks);
            programs_running_cfg_xtu.setLastModifiedTime(File.GetLastWriteTime(programs_running_cfg_xtu.getFullName()).Ticks);
            programs_running_cfg_nice.setLastModifiedTime(File.GetLastWriteTime(programs_running_cfg_nice.getFullName()).Ticks);
            loop_delay.setLastModifiedTime(File.GetLastWriteTime(loop_delay.getFullName()).Ticks);
            boost_cycle_delay.setLastModifiedTime(File.GetLastWriteTime(boost_cycle_delay.getFullName()).Ticks);
            ac_offset.setLastModifiedTime(File.GetLastWriteTime(ac_offset.getFullName()).Ticks);
            processor_guid_tweak.setLastModifiedTime(File.GetLastWriteTime(processor_guid_tweak.getFullName()).Ticks);
            generatedCLK.setLastModifiedTime(File.GetLastWriteTime(generatedCLK.getFullName()).Ticks);
            throttle_median.setLastModifiedTime(File.GetLastWriteTime(throttle_median.getFullName()).Ticks);
        }
        
        public void initPath()
        {
            //path init
            path = AppDomain.CurrentDomain.BaseDirectory + cfgname;   //verbatim string literal @: for directory string

        }
    }
}
