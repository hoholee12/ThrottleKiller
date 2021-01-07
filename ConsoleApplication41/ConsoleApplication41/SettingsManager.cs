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
        public SettingsToken newlist_cycle_delay;
        //public SettingsToken ac_offset;
        public SettingsToken processor_guid_tweak;
        public SettingsToken generatedCLK;
        public SettingsToken generatedXTU;
        public SettingsToken throttle_median;
        public SettingsToken newlist_median;
        public SettingsToken thermal_median;
        public SettingsToken gpuplan;
        public SettingsToken xtudefault;

        //BASE DIRECTORY
        public string cfgname = @"xtu_scheduler_config";
        public string path; //later

        public Logger log;  //init from other side

        //=======================================================================
        //why on settingsmanager? -  its the most frequently passed around object
        public int base_msec = 0;
        
        //for target
        public int accumulated_msec = 0;
        public int target_msec = 0;
        public bool timeSync { get; set; }   //run myself on true

        //for bc_target
        public int bc_acc_msec = 0;
        public int bc_target_msec = 0;
        public bool throttleSync { get; set; }   //throttle delay sync

        //for newlist_target
        public int new_acc_msec = 0;
        public int new_target_msec = 0;
        public bool newlistSync { get; set; }   //newlist delay sync

        //for resur_target(use newlist settings)
        public int resur_acc_msec = 0;
        public int resur_target_msec = 0;
        public bool resurSync { get; set; }   //resurrection delay sync



        //=======================================================================

        //simple locking mechanism for important stuff that cant be interrupted midway
        public bool IPClocked = false;

        public void checkTimeSync() {
            try
            {
                if (loop_delay.changed) target_msec = int.Parse(loop_delay.configList["loop_delay"].ToString()) * 1000;
                if (boost_cycle_delay.changed) bc_target_msec = int.Parse(boost_cycle_delay.configList["boost_cycle_delay"].ToString()) * 1000;
                if (newlist_cycle_delay.changed) new_target_msec = int.Parse(newlist_cycle_delay.configList["newlist_cycle_delay"].ToString()) * 1000;
                if (newlist_cycle_delay.changed) resur_target_msec = int.Parse(newlist_cycle_delay.configList["newlist_cycle_delay"].ToString()) * 1000;
            }
            catch { }
        }

        public void initTimeSync(int msec) {            
            base_msec = msec;
            loop_delay.changed = true;
            boost_cycle_delay.changed = true;
            newlist_cycle_delay.changed = true;
            checkTimeSync();
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
        //to state 1
        public void resetThrottleSync() { throttleSync = false; bc_acc_msec = 0; }

        public void startNewlistSync()
        {
            //state1 to state2
            if (!newlistSync && new_acc_msec == 0)
            {
                newlistSync = true;
            }
        }
        public bool checkNewlistSync()
        {
            //state4 to state1
            if (newlistSync && new_acc_msec != 0)
            {
                new_acc_msec = 0;
                newlistSync = false;
                return true;
            }
            else
            {
                return false;
            }
        }
        //to state 1
        public void resetNewlistSync() { newlistSync = false; new_acc_msec = 0; }

        public void startResurSync()
        {
            //state1 to state2
            if (!resurSync && resur_acc_msec == 0)
            {
                resurSync = true;
            }
        }
        public bool checkResurSync()
        {
            //state4 to state1
            if (resurSync && resur_acc_msec != 0)
            {
                resur_acc_msec = 0;
                resurSync = false;
                return true;
            }
            else
            {
                return false;
            }
        }
        //to state 1
        public void resetResurSync() { resurSync = false; resur_acc_msec = 0; }

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
                        log.WriteLog("time to sync for throttle");
                        throttleSync = true;
                        bc_target_msec = int.Parse(boost_cycle_delay.configList["boost_cycle_delay"].ToString()) * 1000;
                    }
                    else
                    {
                        bc_acc_msec += base_msec;
                    }
                }

                //newlist timer - copy of throttle timer
                /* states:
                 * 1. newlistSync = false, new_acc_msec = 0: (launch)
                 * 2. newlistSync = true, new_acc_msec = 0:
                 *      start timer:
                 *          accumulate new_acc_msec
                 * 3. newlistSync = false, new_acc_msec != 0:
                 *      check sync:
                 *          set newlistSync = true
                 *          set new_acc_msec = 0
                 *      else:
                 *          acumulate new_acc_msec
                 * 4. newlistSync = true, new_acc_msec != 0: (exit)
                 *      to: state 1
                 */

                //state 2
                if (newlistSync && new_acc_msec == 0)
                {
                    log.WriteLog("start of newlist timer");
                    newlistSync = false;
                    new_acc_msec += base_msec;
                }
                //state 3
                else if (!newlistSync && new_acc_msec != 0)
                {
                    if (new_acc_msec % new_target_msec == 0)
                    {
                        log.WriteLog("time to sync for newlist");
                        newlistSync = true;
                        new_target_msec = int.Parse(newlist_cycle_delay.configList["newlist_cycle_delay"].ToString()) * 1000;
                    }
                    else
                    {
                        new_acc_msec += base_msec;
                    }
                }

                //resur timer - copy of throttle timer
                /* states:
                 * bleh
                 */

                //state 2
                if (resurSync && resur_acc_msec == 0)
                {
                    log.WriteLog("start of resurrection timer");
                    resurSync = false;
                    resur_acc_msec += base_msec;
                }
                //state 3
                else if (!resurSync && resur_acc_msec != 0)
                {
                    if (resur_acc_msec % resur_target_msec == 0)
                    {
                        log.WriteLog("time to sync for resurrection");
                        resurSync = true;
                        resur_target_msec = int.Parse(newlist_cycle_delay.configList["newlist_cycle_delay"].ToString()) * 1000;
                    }
                    else
                    {
                        resur_acc_msec += base_msec;
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
         * 1 -> cpu(cpu usage under throttlemedian)
         * 2 -> gpu(cpu usage over throttlemedian)
         * cpu is more important than gpu
         */
        public int throttleMode { get; set; }

        /*
         * sm.resurrectMode:
         * 0 -> nein
         * 1 -> switch to cpu higher profile
         * 2 -> switch to gpu higher profile
         */
        public int resurrectMode { get; set; }

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
            newlist_cycle_delay.checkFiles();
            //ac_offset.checkFiles();
            checkPowerCFGFlag = processor_guid_tweak.checkFiles();
            generatedCLK.checkFiles();
            generatedXTU.checkFiles();
            throttle_median.checkFiles();
            newlist_median.checkFiles();
            thermal_median.checkFiles();
            gpuplan.checkFiles();
            xtudefault.checkFiles();
        }
        //for client
        public void batchCompleteWriteback() {
            special_programs.completeWriteBack();
            programs_running_cfg_cpu.completeWriteBack();
            programs_running_cfg_xtu.completeWriteBack();
            programs_running_cfg_nice.completeWriteBack();
            loop_delay.completeWriteBack();
            boost_cycle_delay.completeWriteBack();
            newlist_cycle_delay.completeWriteBack();
            //ac_offset.completeWriteBack();
            processor_guid_tweak.completeWriteBack();
            generatedCLK.completeWriteBack();
            generatedXTU.completeWriteBack();
            throttle_median.completeWriteBack();
            newlist_median.completeWriteBack();
            thermal_median.completeWriteBack();
            gpuplan.completeWriteBack();
            xtudefault.completeWriteBack();
        }
        //for client
        public void batchResetFiles() {
            special_programs.resetFiles();
            programs_running_cfg_cpu.resetFiles();
            programs_running_cfg_xtu.resetFiles();
            programs_running_cfg_nice.resetFiles();
            loop_delay.resetFiles();
            boost_cycle_delay.resetFiles();
            newlist_cycle_delay.resetFiles();
            //ac_offset.resetFiles();
            processor_guid_tweak.resetFiles();
            generatedCLK.resetFiles();
            generatedXTU.resetFiles();
            throttle_median.resetFiles();
            newlist_median.resetFiles();
            thermal_median.resetFiles();
            gpuplan.resetFiles();
            xtudefault.resetFiles();
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
            newlist_cycle_delay = new SettingsToken(log);
            //ac_offset = new SettingsToken(log);
            processor_guid_tweak = new SettingsToken(log);
            generatedCLK = new SettingsToken(log);
            generatedXTU = new SettingsToken(log);
            throttle_median = new SettingsToken(log);
            newlist_median = new SettingsToken(log);
            thermal_median = new SettingsToken(log);
            gpuplan = new SettingsToken(log);
            xtudefault = new SettingsToken(log);
            
            
            //initialize paths
            special_programs.setPath(path);
            programs_running_cfg_cpu.setPath(path);
            programs_running_cfg_xtu.setPath(path);
            programs_running_cfg_nice.setPath(path);
            loop_delay.setPath(path);
            boost_cycle_delay.setPath(path);
            newlist_cycle_delay.setPath(path);
            //ac_offset.setPath(path);
            processor_guid_tweak.setPath(path);
            generatedCLK.setPath(path);
            generatedXTU.setPath(path);
            throttle_median.setPath(path);
            newlist_median.setPath(path);
            thermal_median.setPath(path);
            gpuplan.setPath(path);
            xtudefault.setPath(path);

            special_programs.setName("special_programs");
            programs_running_cfg_cpu.setName("programs_running_cfg_cpu");
            programs_running_cfg_xtu.setName("programs_running_cfg_xtu");
            programs_running_cfg_nice.setName("programs_running_cfg_nice");
            loop_delay.setName("loop_delay");
            boost_cycle_delay.setName("boost_cycle_delay");
            newlist_cycle_delay.setName("newlist_cycle_delay");
            //ac_offset.setName("ac_offset");
            processor_guid_tweak.setName("processor_guid_tweak");
            generatedCLK.setName("generatedCLK");
            generatedXTU.setName("generatedXTU");
            throttle_median.setName("throttle_median");
            newlist_median.setName("newlist_median");
            thermal_median.setName("thermal_median");
            gpuplan.setName("gpuplan");
            xtudefault.setName("xtudefault");

            //initialize contents
            special_programs.setContent(
@"
");    //apps can be added
            programs_running_cfg_cpu.setContent(
@"0 = 100
1 = 99
2 = 98
3 = 95
4 = 84
5 = 73
6 = 65
7 = 58
8 = 50
9 = 43
10 = 32
11 = 24
");
            programs_running_cfg_xtu.setContent(
@"11 = 10
10 = 9.5
9 = 9
8 = 8.5
7 = 8
6 = 7.5
5 = 7
4 = 6.5
3 = 6
2 = 5.5
1 = 5
0 = 4.5
");
            programs_running_cfg_nice.setContent(
@"0 = normal
1 = normal
2 = normal
3 = normal
4 = normal
5 = normal
6 = normal
7 = normal");
            loop_delay.setContent(@"loop_delay = 5");
            boost_cycle_delay.setContent(@"boost_cycle_delay = 30");
            newlist_cycle_delay.setContent(@"newlist_cycle_delay = 120");
            //ac_offset.setContent(@"ac_offset = 1");
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
            generatedXTU.setContent("");
            throttle_median.setContent(@"throttle_median = 70");
            newlist_median.setContent(@"newlist_median = 50");
            thermal_median.setContent(@"thermal_median = 90");
            gpuplan.setContent(@"gpuplan = 1");
            xtudefault.setContent("");

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
            newlist_cycle_delay.Tkey = typeof(string);
            newlist_cycle_delay.Tval = typeof(int);
            //ac_offset.Tkey = typeof(string);
            //ac_offset.Tval = typeof(int);
            processor_guid_tweak.Tkey = typeof(string);
            processor_guid_tweak.Tval = typeof(int);
            generatedCLK.Tkey = typeof(int);
            generatedCLK.Tval = typeof(int);
            generatedXTU.Tkey = typeof(int);
            generatedXTU.Tval = typeof(float);
            throttle_median.Tkey = typeof(string);
            throttle_median.Tval = typeof(int);
            newlist_median.Tkey = typeof(string);
            newlist_median.Tval = typeof(int);
            thermal_median.Tkey = typeof(string);
            thermal_median.Tval = typeof(int);
            gpuplan.Tkey = typeof(string);
            gpuplan.Tval = typeof(int);
            xtudefault.Tkey = typeof(string);
            xtudefault.Tval = typeof(float);

            //batch create first/read settings
            batchCheckFiles();


            //and then get last modified date
            special_programs.setLastModifiedTime(File.GetLastWriteTime(special_programs.getFullName()).Ticks);
            programs_running_cfg_cpu.setLastModifiedTime(File.GetLastWriteTime(programs_running_cfg_cpu.getFullName()).Ticks);
            programs_running_cfg_xtu.setLastModifiedTime(File.GetLastWriteTime(programs_running_cfg_xtu.getFullName()).Ticks);
            programs_running_cfg_nice.setLastModifiedTime(File.GetLastWriteTime(programs_running_cfg_nice.getFullName()).Ticks);
            loop_delay.setLastModifiedTime(File.GetLastWriteTime(loop_delay.getFullName()).Ticks);
            boost_cycle_delay.setLastModifiedTime(File.GetLastWriteTime(boost_cycle_delay.getFullName()).Ticks);
            newlist_cycle_delay.setLastModifiedTime(File.GetLastWriteTime(newlist_cycle_delay.getFullName()).Ticks);
            //ac_offset.setLastModifiedTime(File.GetLastWriteTime(//ac_offset.getFullName()).Ticks);
            processor_guid_tweak.setLastModifiedTime(File.GetLastWriteTime(processor_guid_tweak.getFullName()).Ticks);
            generatedCLK.setLastModifiedTime(File.GetLastWriteTime(generatedCLK.getFullName()).Ticks);
            generatedXTU.setLastModifiedTime(File.GetLastWriteTime(generatedXTU.getFullName()).Ticks);
            throttle_median.setLastModifiedTime(File.GetLastWriteTime(throttle_median.getFullName()).Ticks);
            newlist_median.setLastModifiedTime(File.GetLastWriteTime(newlist_median.getFullName()).Ticks);
            thermal_median.setLastModifiedTime(File.GetLastWriteTime(thermal_median.getFullName()).Ticks);
            gpuplan.setLastModifiedTime(File.GetLastWriteTime(gpuplan.getFullName()).Ticks);
            xtudefault.setLastModifiedTime(File.GetLastWriteTime(xtudefault.getFullName()).Ticks);
        }
        
        public void initPath()
        {
            //path init
            path = AppDomain.CurrentDomain.BaseDirectory + cfgname;   //verbatim string literal @: for directory string

        }
    }
}
