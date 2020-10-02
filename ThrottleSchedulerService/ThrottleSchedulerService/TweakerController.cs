using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Diagnostics;

namespace ThrottleSchedulerService
{

    //XTU VERY HEAVY DO NOT USE IT TO PROBE
    class TweakerController
    {
        Process pshell;
        Logger log;
        TweakerChecker checker;

        int count = 0;



/*
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
*/

        //for powercfg init
        string powerplan = "381b4222-f694-41f0-9685-ff5bb260df2e";		// you can change to any powerplan you want as default!
        string processor = "54533251-82be-4824-96c1-47b60b740d00";		// processor power management
        string procsubh = "bc5038f7-23e0-4960-96da-33abaf5935ec";	    // processor high clockspeed limit
        string procsubl = "893dee8e-2bef-41e0-89c6-b55d0929964c";		// processor low clockspeed limit
        string gpupplan = "44f3beca-a7c0-460e-9df2-bb8b99e0cba6";		// intel graphics power management
        string gpuppsub = "3619c3f2-afb2-4afc-b0e9-e7fef372de36";	    // submenu of intel graphics power management
        //intel graphics settings
        //	1 = Balanced(Maximum Battery Life is useless)
        //	2 = Maximum Performance(seems to remove long term throttling...)
        Process powercfg;

        public TweakerController(Logger log, TweakerChecker checker)
        {
            this.log = log;
            this.checker = checker;
            try
            {
                pshell = new Process();
                pshell.StartInfo = new ProcessStartInfo();
                pshell.StartInfo.FileName = "powershell";
                pshell.StartInfo.Arguments = "get-command xtucli";
                pshell.StartInfo.UseShellExecute = false;
                pshell.StartInfo.RedirectStandardOutput = true;
                pshell.StartInfo.CreateNoWindow = true;

                //check if exists
                log.WriteLog("checking XTU cmdlet");
                pshell.Start();
                pshell.WaitForExit();

                if (pshell.ExitCode == 1)
                {
                    log.WriteLog("XTU cmdlet missing, cannot continue.");
                    Environment.Exit(1);
                }
            }
            catch (Exception e)
            {
                log.WriteLog(e.ToString());
            }
            finally {
                log.WriteLog("XTU ok");
            }


            pshell.StartInfo.FileName = "xtucli";

            powercfg = new Process();
            powercfg.StartInfo = new ProcessStartInfo();
            powercfg.StartInfo.FileName = "powercfg";
            powercfg.StartInfo.Arguments = "";
            powercfg.StartInfo.UseShellExecute = false;
            powercfg.StartInfo.RedirectStandardOutput = true;
            powercfg.StartInfo.CreateNoWindow = true;


        }



        public string runpowercfg(string str) {
            powercfg.StartInfo.Arguments = str;
            powercfg.Start();
            string output = powercfg.StandardOutput.ReadToEnd();
            powercfg.WaitForExit();
            return output;
        }

        public float getXTU() {
            pshell.StartInfo.Arguments = "-t -id 59";
            pshell.Start();
            string[] result = pshell.StandardOutput.ReadToEnd().Split(' ');
            pshell.WaitForExit();
            string temp = result.Last().Replace("x", "").Trim();
            return float.Parse(temp);
        }


        //intel graphics settings
        //	false = Balanced(Maximum Battery Life is useless)
        //	true = Maximum Performance(seems to remove long term throttling...)
        public void setXTU(SettingsManager sm, double value, bool gpuplan) {
            log.WriteLog("setting XTU: " + value);
            pshell.StartInfo.Arguments = "-t -id 59 -v " + value;
            pshell.Start();
            pshell.WaitForExit();
            int gpux = 1;   //balanced
            if (gpuplan)
            {
                log.WriteLog("setting GPU: performance");
                gpux = 2;  //performance
            }
            else {
                log.WriteLog("setting GPU: balanced");
            }
            //gpu power scheduler
            runpowercfg("/setdcvalueindex " + powerplan + " " + gpupplan + " " + gpuppsub + " " + gpux);
            runpowercfg("/setacvalueindex " + powerplan + " " + gpupplan + " " + gpuppsub + " " + gpux);

            runpowercfg("/setactive " + powerplan); //apply

        }

        public void initPowerCFG(SettingsManager sm) {
            
            foreach (string temp in sm.processor_guid_tweak.configList.Keys) {
                runpowercfg("/attributes " + processor + " " + temp + " -ATTRIB_HIDE");
                runpowercfg("/setdcvalueindex " + powerplan + " " + processor + " " + temp + " " + sm.processor_guid_tweak.configList[temp]);
                runpowercfg("/setacvalueindex " + powerplan + " " + processor + " " + temp + " " + sm.processor_guid_tweak.configList[temp]);
            }
            runpowercfg("/attributes " + gpupplan + " " + gpuppsub + " -ATTRIB_HIDE");
           
            runpowercfg("/setactive " + powerplan); //apply
        
        }

        public void setCLK(SettingsManager sm, int setval, bool low) {
            
            

            if (low)
            {
                runpowercfg("/setdcvalueindex " + powerplan + " " + processor + " " + procsubl + " " + setval);
                runpowercfg("/setacvalueindex " + powerplan + " " + processor + " " + procsubl + " " + setval);
                log.WriteLog("setting base CLK: " + setval);
            }
            else
            {
                runpowercfg("/setdcvalueindex " + powerplan + " " + processor + " " + procsubh + " " + setval);
                runpowercfg("/setacvalueindex " + powerplan + " " + processor + " " + procsubh + " " + (setval - (int)sm.ac_offset.configList["ac_offset"])); //hotter when plugged in
                log.WriteLog("setting CLK: " + setval);
            }

            runpowercfg("/setactive " + powerplan); //apply
        }

        public int getCLK(bool low) {
            string temp;
            if (low) temp = runpowercfg("/query " + powerplan + " " + processor + " " + procsubl);
            else temp = runpowercfg("/query " + powerplan + " " + processor + " " + procsubh);
            string temp2 = temp.Split(' ').Last().Trim();
            int temp3 = Convert.ToInt32(temp2, 16);
            return temp3;
        }

        //-1: not found
        public int checkInList(Process proc, SettingsManager sm) {
            int temp = -1;
            foreach (string name in sm.special_programs.configList.Keys)
            {
                if (proc.ProcessName.ToLower().Contains(name.ToLower()))
                {
                    temp = (int)sm.special_programs.configList[name];
                    break;
                }
            }
            return temp;
        }

        //apply nice per process
        public void setProcNice(Process proc, SettingsManager sm) {
            int temp = checkInList(proc, sm);
            if (temp == -1) return; //not in my list

            try
            {
                ProcessPriorityClass temp2 =
                    (ProcessPriorityClass)sm.programs_running_cfg_nice.configList[temp];
                if (proc.PriorityClass != temp2)
                {
                    log.WriteLog("setting niceness: " + proc.ProcessName + " to " + temp2.ToString());
                    proc.PriorityClass = temp2;
                }
            }
            catch (Exception) { }
        }

        //apply power per process
        public void setPower(Process proc, SettingsManager sm) {
            int temp = checkInList(proc, sm);
            if (temp == -1) return; //not in my list
            
            try
            {
                int temp2 = (int)sm.programs_running_cfg_cpu.configList[temp];
                double temp3 = (float)sm.programs_running_cfg_xtu.configList[temp];

                if (getCLK(false) != temp2)
                {
                    log.WriteLog("setting power: " + proc.ProcessName + " to " + temp2.ToString());
                    setCLK(sm, temp2, false);
                    setXTU(sm, temp3, false);
                }
            }
            catch (Exception) { }

        }

        //generate CLK list
        //CLK = powerplan value, PWR = real clockspeed
        public void generateCLKlist(SettingsManager sm, TweakerChecker tc) {
            if (sm.generatedCLK.getCount() > 1) return; //all generated

            int clkbackuph = getCLK(false);
            int clkbackupl = getCLK(true);

            sm.generatedCLK.configList.Clear();

            //else
            log.WriteLog("================start of CLK list generation================");
            log.WriteLog("do NOT run anything power intensive!!!");

            int prevPWR = int.MaxValue;
            //start looping from 100 down to 50
            for(int i = 100; i > 50; i--){
                runpowercfg("/setdcvalueindex " + powerplan + " " + processor + " " + procsubh + " " + i);
                runpowercfg("/setacvalueindex " + powerplan + " " + processor + " " + procsubh + " " + i);
                runpowercfg("/setdcvalueindex " + powerplan + " " + processor + " " + procsubl + " " + i);
                runpowercfg("/setacvalueindex " + powerplan + " " + processor + " " + procsubl + " " + i);
                runpowercfg("/setactive " + powerplan); //apply

                if (prevPWR != tc.getPWR())
                {
                    prevPWR = tc.getPWR();
                    
                    //add
                    sm.generatedCLK.configList.Add(i, prevPWR);
                    log.WriteLog("new CLK: " + i + " clockspeed: " + prevPWR);
                }
            
            }
            //write back
            log.WriteLog("writeback to file commencing...");
            sm.generatedCLK.completeWriteBack();

            setCLK(sm, clkbackuph, false);   //restore old clk
            setCLK(sm, clkbackupl, true);   //restore old clk
            log.WriteLog("================end of CLK list generation================");

        }

        //apply based on profile
        public void setNiceProfile(SettingsManager sm)
        {

            Process[] plist = Process.GetProcesses();


            //simple check to see if theres new processes or not
            int ctemp = plist.Count();
            if (count != ctemp) {
                count = ctemp;

                plist.ToList().ForEach(proc =>
                {
                    int temp = -1;
                    foreach (string name in sm.special_programs.configList.Keys)
                    {
                        if (proc.ProcessName.ToLower().Contains(name.ToLower()))
                        {
                            temp = (int)sm.special_programs.configList[name];
                        }
                    }
                    if (temp == -1) return; //not in my list

                    try
                    {
                        ProcessPriorityClass temp2 =
                            (ProcessPriorityClass)sm.programs_running_cfg_nice.configList[temp];
                        if (proc.PriorityClass != temp2)
                        {
                            log.WriteLog("setting niceness: " + proc.ProcessName + " to " + temp2.ToString());
                            proc.PriorityClass = temp2;
                        }
                    }
                    catch (Exception) { }
                });
            }
        }

    }


}
