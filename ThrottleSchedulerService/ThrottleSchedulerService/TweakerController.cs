using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Diagnostics;

namespace ThrottleSchedulerService
{

    //VERY HEAVY DO NOT USE IT TO PROBE
    class TweakerController
    {
        Process pshell;
        Logger log;

        //for powercfg init
        string guid0 = "381b4222-f694-41f0-9685-ff5bb260df2e";		// you can change to any powerplan you want as default!
        string guid1 = "54533251-82be-4824-96c1-47b60b740d00";		// processor power management
        string guid2 = "bc5038f7-23e0-4960-96da-33abaf5935ec";	    // processor high clockspeed limit
        string guid3 = "893dee8e-2bef-41e0-89c6-b55d0929964c";		// processor low clockspeed limit
        string guid4 = "44f3beca-a7c0-460e-9df2-bb8b99e0cba6";		// intel graphics power management
        string guid5 = "3619c3f2-afb2-4afc-b0e9-e7fef372de36";	    // submenu of intel graphics power management
        //intel graphics settings
        //	1 = Balanced(Maximum Battery Life is useless)
        //	2 = Maximum Performance(seems to remove long term throttling...)
        Process powercfg;

        public TweakerController(Logger log)
        {
            this.log = log;

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



        public void runpowercfg(string str) {
            powercfg.StartInfo.Arguments = str;
            powercfg.Start();
            powercfg.WaitForExit();
        }

        public float getXTU() {
            //pshell.StartInfo.Arguments = "-noprofile -command \"((& xtucli -t -id 59 | select-string \\\"59\\\" | %{ -split $_ | select -index 5} | out-string) -replace \\\"x\\\",'').trim()\"";
            pshell.StartInfo.Arguments = "-t -id 59";
            pshell.Start();
            string[] result = pshell.StandardOutput.ReadToEnd().Split(' ');
            pshell.WaitForExit();
            string temp = result.Last().Replace("x", "").Trim();
            return float.Parse(temp);
        }

        public void setXTU(double value) {
            pshell.StartInfo.Arguments = "-t -id 59 -v " + value;
            pshell.Start();
            pshell.WaitForExit();
        }

        public void initPowerCFG(SettingsManager sm) {
            
            foreach (string temp in sm.processor_guid_tweak.configList.Keys) {
                runpowercfg("/attributes " + guid1 + " " + temp + " -ATTRIB_HIDE");
                runpowercfg("/setdcvalueindex " + guid0 + " " + guid1 + " " + temp + " " + sm.processor_guid_tweak.configList[temp]);
                runpowercfg("/setacvalueindex " + guid0 + " " + guid1 + " " + temp + " " + sm.processor_guid_tweak.configList[temp]);
            }
            runpowercfg("/attributes " + guid4 + " " + guid5 + " -ATTRIB_HIDE");
           
            runpowercfg("/setactive " + guid0); //apply
        
        }

        //intel graphics settings
        //	1 = Balanced(Maximum Battery Life is useless)
        //	2 = Maximum Performance(seems to remove long term throttling...)
        public void setCLK(SettingsManager sm, int setval, int setgpu) {
            log.WriteLog("setting CLK:" + setval + " GPU:" + setgpu);

            runpowercfg("/setdcvalueindex " + guid0 + " " + guid1 + " " + guid2 + " " + setval);
            runpowercfg("/setacvalueindex " + guid0 + " " + guid1 + " " + guid2 + " " + (setval - (int)sm.ac_offset.configList["ac_offset"])); //hotter when plugged in

            //gpu power scheduler
            runpowercfg("/setdcvalueindex " + guid0 + " " + guid4 + " " + guid5 + " " + setgpu);
            runpowercfg("/setacvalueindex " + guid0 + " " + guid4 + " " + guid5 + " " + setgpu);

            runpowercfg("/setactive " + guid0); //apply
        }

    }


}
