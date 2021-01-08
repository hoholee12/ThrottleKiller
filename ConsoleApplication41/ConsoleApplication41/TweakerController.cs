using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Diagnostics;
using System.Timers;

namespace ThrottleSchedulerService
{

    //XTU VERY HEAVY DO NOT USE IT TO PROBE
    class TweakerController
    {
        Process pshell;
        Logger log;
        TweakerChecker checker;

        int count = 0;

        //magic number to reduce wasted time
        public int lastCLK = 1234;
        public int lastBaseCLK = 1234;
        public float lastXTU = 1234;

        //force reapply
        public bool forceApply = false;

        //wrong
        public bool wrong = false;

        //xtu apply daemon
        public bool xtuapplynested = false;
        public double xtuapplyvalue = 0.0;
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

        public float MaxXTU = 1234;   //initial xtu value(safe measure)
        public float BaseXTU = 1234;

        public TweakerController(Logger log, TweakerChecker checker, SettingsManager sm)
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
                pshell.PriorityClass = ProcessPriorityClass.Idle;   //make sure it dont disrupt others
                pshell.WaitForExit();

                if (pshell.ExitCode == 1)
                {
                    log.WriteLog("XTU cmdlet missing, cannot continue.");
                    wrong = true;
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

            //max xtu
            if (sm.xtudefault.getCount() == 0)
            {
                MaxXTU = getXTU();  //get initial xtu value
                sm.xtudefault.configList.Add("xtudefault", MaxXTU);
                sm.xtudefault.completeWriteBack();
            }
            else {
                MaxXTU = float.Parse(sm.xtudefault.configList["xtudefault"].ToString());
                lastXTU = MaxXTU;
            }
            BaseXTU = getBaseXTU(sm);
        }

        public float getBaseXTU(SettingsManager sm) {
            sm.IPClocked = true;

            if (BaseXTU != 1234) return BaseXTU;
            
            //backup
            float lastxtutemp = lastXTU;

            //lowest xtu
            xtuapplynested = true;
            xtuapplyvalue = 0.5;
            setXTU(sm, 0.5);
            //get mhz
            //sometimes xtucli can be unreliable, try
            try
            {
                pshell.StartInfo.FileName = "xtucli";
                pshell.StartInfo.Arguments = "-m -id 6";
                pshell.Start();
                pshell.PriorityClass = ProcessPriorityClass.Idle;   //make sure it dont disrupt others
                string[] result = pshell.StandardOutput.ReadToEnd().Split(' ');
                pshell.WaitForExit();
                string temp = result.Last().Replace("MHz", "").Trim();
                BaseXTU = (float)Math.Round(double.Parse(temp) / 100 * 2, MidpointRounding.AwayFromZero) / 2;
                log.WriteLog("real BaseXTU speed: " + temp + " BaseXTU: " + BaseXTU);
            }
            catch{}

            //restore
            xtuapplynested = true;
            xtuapplyvalue = lastxtutemp;
            setXTU(sm, lastxtutemp);

            sm.IPClocked = false;

            return BaseXTU;
        }


        public string runpowercfg(string str) {
            powercfg.StartInfo.Arguments = str;
            powercfg.Start();
            powercfg.PriorityClass = ProcessPriorityClass.Idle;   //make sure it dont disrupt others
            string output = powercfg.StandardOutput.ReadToEnd();
            powercfg.WaitForExit();
            return output;
        }

        public float getXTU()
        {
            if (!forceApply) if (lastXTU != 1234) return lastXTU;
            forceApply = false;


            pshell.StartInfo.Arguments = "-t -id 59";
            pshell.Start();
            pshell.PriorityClass = ProcessPriorityClass.Idle;   //make sure it dont disrupt others
            string[] result = pshell.StandardOutput.ReadToEnd().Split(' ');
            pshell.WaitForExit();
            string temp = result.Last().Replace("x", "").Trim();
            lastXTU = float.Parse(temp);

            log.WriteLog("result = " + lastXTU);
            return lastXTU;
        }

        //prevent slowdown on high cpu apps by delaying
        public void XTUdaemon(SettingsManager sm, TweakerChecker tc) {
            int currpwr = tc.autofilterPWR(tc.getPWR());
            int load = tc.getLoad();
            if (load >= (int)sm.throttle_median.configList["throttle_median"]) {
                xtuapplynested = false;
                return;
            }
            if (xtuapplyvalue != 0.0)
            {
                xtuapplynested = true;
            }
            
        }

        //intel graphics settings
        //	false = Balanced(Maximum Battery Life is useless)
        //	true = Maximum Performance(seems to remove long term throttling...)
        public void setXTU(SettingsManager sm, double value) {
            //skip delay if xtu demand
            if (value > lastXTU) xtuapplyvalue = value;

            if (!xtuapplynested && xtuapplyvalue == 0.0)
            {
                log.WriteLog("setting XTU nested");
                xtuapplyvalue = value;
                return;
            }
            else
            {
                value = xtuapplyvalue;
                xtuapplyvalue = 0.0;
                xtuapplynested = false;
            }

            lastXTU = (float)value;

            log.WriteLog("setting XTU: " + value);
            pshell.StartInfo.Arguments = "-t -id 59 -v " + value;
            pshell.Start();
            pshell.PriorityClass = ProcessPriorityClass.Idle;   //make sure it dont disrupt others
            
            pshell.WaitForExit();
            pshell.StandardOutput.ReadToEnd();  //extra delay just in case!
            
            
            int gpux = 1;   //balanced
            
            
            if (forceApply) return; //passthrough


            if ((int)sm.gpuplan.configList["gpuplan"] == 1)
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
            sm.IPClocked = true;

            log.WriteLog("init PowerCFG settings");
            foreach (string temp in sm.processor_guid_tweak.configList.Keys) {
                runpowercfg("/attributes " + processor + " " + temp + " -ATTRIB_HIDE");
                runpowercfg("/setdcvalueindex " + powerplan + " " + processor + " " + temp + " " + sm.processor_guid_tweak.configList[temp]);
                runpowercfg("/setacvalueindex " + powerplan + " " + processor + " " + temp + " " + sm.processor_guid_tweak.configList[temp]);
            }
            runpowercfg("/attributes " + gpupplan + " " + gpuppsub + " -ATTRIB_HIDE");
           
            runpowercfg("/setactive " + powerplan); //apply

            sm.IPClocked = false;
        
        }

        public void setCLK(SettingsManager sm, int setval, bool low) {
            sm.IPClocked = true;

            if (low)
            {
                lastBaseCLK = setval;
                runpowercfg("/setdcvalueindex " + powerplan + " " + processor + " " + procsubl + " " + setval);
                runpowercfg("/setacvalueindex " + powerplan + " " + processor + " " + procsubl + " " + setval);
                log.WriteLog("setting base CLK: " + setval);
            }
            else
            {
                if (setval == lastCLK) return;
                lastCLK = setval;   //maxCLK for other use
                runpowercfg("/setdcvalueindex " + powerplan + " " + processor + " " + procsubh + " " + setval);
                //runpowercfg("/setacvalueindex " + powerplan + " " + processor + " " + procsubh + " " + (setval - (int)sm.ac_offset.configList["ac_offset"])); //hotter when plugged in
                runpowercfg("/setacvalueindex " + powerplan + " " + processor + " " + procsubh + " " + setval);
                log.WriteLog("setting CLK: " + setval);
            }

            runpowercfg("/setactive " + powerplan); //apply

            sm.IPClocked = false;
        }

        //track lastCLK because launching app is quite heavy...
        public int getCLK(bool low) {
            if (!forceApply)
            if (low)
            {
                if (lastBaseCLK != 1234) { return lastBaseCLK; }
            }
            else
            {
                if (lastCLK != 1234) { return lastCLK; }
            }

            forceApply = false;

            string temp;
            if (low)
            {
                temp = runpowercfg("/query " + powerplan + " " + processor + " " + procsubl);
            }
            else
            {
                temp = runpowercfg("/query " + powerplan + " " + processor + " " + procsubh);
            }
            string temp2 = temp.Split(' ').Last().Trim();
            int temp3 = Convert.ToInt32(temp2, 16);

            if (low)
            {
                lastBaseCLK = temp3;
            }
            else 
            {
                lastCLK = temp3;
            }

            return temp3;
        }

        //-1: not found
        public int checkInList(Process proc, SettingsManager sm) {
            sm.IPClocked = true;

            int temp = -1;
            foreach (string name in sm.special_programs.configList.Keys)
            {
                if (proc.ProcessName.ToLower().Contains(name.ToLower()))
                {
                    temp = (int)sm.special_programs.configList[name];
                    break;
                }
            }

            sm.IPClocked = false;

            return temp;
        }

        public string checkNameInList(Process proc, SettingsManager sm) {
            sm.IPClocked = true;

            string temp = proc.ProcessName;

            foreach (string name in sm.special_programs.configList.Keys) {
                if (proc.ProcessName.ToLower().Contains(name.ToLower()))
                {
                    temp = name;
                    break;
                }
            }

            sm.IPClocked = false;
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
            sm.IPClocked = true;

            int temp = checkInList(proc, sm);
            if (temp == -1) {
                try
                {
                    int limit = (int)sm.newlist_median.configList["newlist_median"];    //ex) 50
                    int listcount = sm.generatedCLK.configList.Count();                 //ex) 12
                    int indexlimit = listcount * limit / 100;                           //ex) 6
                    float xtuval = (float)sm.programs_running_cfg_xtu.configList[indexlimit];
                    if (getCLK(false) != 100 || getXTU() != xtuval || forceApply)
                    {
                        forceApply = false;
                        log.WriteLog("newlist mode 100 " + xtuval);
                        //set clk to highest, set xtu to highest(newlist_median)
                        //certain limit to reduce framerate makes newlist monitor stable
                        setCLK(sm, 100, false);
                        setXTU(sm, xtuval);
                    }
                }
                catch { }
            }
            else
            {

                try
                {
                    int temp2 = (int)sm.programs_running_cfg_cpu.configList[temp];
                    double temp3 = (float)sm.programs_running_cfg_xtu.configList[temp];

                    if (getCLK(false) != temp2 || getXTU() != temp3 || forceApply)   //forceApply for throttle config
                    {
                        forceApply = false;

                        if (temp != 0) log.WriteLog("setting power: " + proc.ProcessName + " to " + temp2.ToString());
                        else log.WriteLog("setting power back to " + temp2.ToString()); //default 0
                        setCLK(sm, temp2, false);
                        if (MaxXTU < temp3)
                        {
                            log.WriteLog("oh no, XTU value bad... you may want to restart your computer");
                            wrong = true;
                            return;
                        }
                        setXTU(sm, temp3);
                    }
                }
                catch (Exception) { }
            }

            sm.IPClocked = false;

        }

        //generate CLK list
        //CLK = powerplan value, PWR = real clockspeed
        /*
         *  profile number: (ascending order)
         *      least = CPU high/ GPU low
         *      most = CPU low/ GPU high
         * 
         *      **by default least is done to monitor correct cpu load for new apps.
         * 
         * 
         * 
         * 
         */
        public void generateCLKlist(SettingsManager sm, TweakerChecker tc) {
            sm.IPClocked = true;

            if(!forceApply)
            if ((sm.generatedCLK.getCount() > 1)
                && (sm.generatedXTU.getCount() > 1)
                && (sm.programs_running_cfg_cpu.getCount() > 1)
                && (sm.programs_running_cfg_xtu.getCount() > 1)
                ) return;  //all generated
            forceApply = false;

            int clkbackuph = getCLK(false);
            int clkbackupl = getCLK(true);
            float xtubackup = getXTU();

            forceApply = true;
            sm.generatedCLK.configList.Clear();
            sm.generatedXTU.configList.Clear();
            sm.programs_running_cfg_cpu.configList.Clear();
            sm.programs_running_cfg_xtu.configList.Clear();

            //else
            log.WriteLog("================start of CLK + XTU list generation================");
            log.WriteLog("do NOT run anything power intensive!!!");

            Dictionary<int, int> hello = new Dictionary<int, int>();

            int bcount = 0;     //val
            int bcontender = 0; //key
            
            int prevPWR = int.MaxValue;
            int exPWR = prevPWR;
            
            //start looping from 100 down to 0
            int x = 0;
            for(int i = 100; i >= 0; i--){
                tc.resettick();
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
                    sm.programs_running_cfg_cpu.configList.Add(x++, i);
                    log.WriteLog("new CLK: " + i + " clockspeed: " + prevPWR);

                    //contender
                    log.WriteLog("diff: " + (exPWR - prevPWR));
                    if (!hello.ContainsKey(exPWR - prevPWR))
                    {
                        hello.Add(exPWR - prevPWR, 1);
                    }
                    else
                    {
                        hello[exPWR - prevPWR]++;
                    }
                    exPWR = prevPWR;
                }
            }

            //calculate artificial base xtu(basextu func unreliable)
            /* howto:
             *      1. find lowest clockspeed stepping difference(contender)
             *      2. maxxtu - (hi - lo cpustep) / contender * xtustep
             */
            foreach (KeyValuePair<int, int> kv in hello)
            {
                if (bcount < kv.Value)
                {
                    bcount = kv.Value;
                    bcontender = kv.Key;
                }
            }

            log.WriteLog("bcontender: " + bcontender);

            BaseXTU = (float)Math.Round(
                (MaxXTU * 100 - (tc.getTurboPWR() - prevPWR) / bcontender * 50) / 100 * 2, MidpointRounding.AwayFromZero) / 2;


            //calculate proper xtu for each clk value
            float ratio = (MaxXTU - BaseXTU) * 100 / (tc.getTurboPWR() - (int)sm.generatedCLK.configList.Last().Value);
            float xtutemp = MaxXTU * 100;
            var listtemp = tc.sortedCLKlist(sm);
            try
            {
                foreach (int j in listtemp)
                {
                    xtutemp -= ((int)sm.generatedCLK.configList[listtemp[x - 1]] - (int)sm.generatedCLK.configList[listtemp[x - 2]]) * ratio;
                    float xtutemp2 = (float)Math.Round(xtutemp / 100 * 2, MidpointRounding.AwayFromZero) / 2;

                    sm.generatedXTU.configList.Add(j, xtutemp2);
                    sm.programs_running_cfg_xtu.configList.Add(--x, xtutemp2);

                    log.WriteLog("new XTU: " + xtutemp2 + " for CLK: " + j);
                }
            }
            catch (Exception) { }
            sm.generatedXTU.configList.Add(100, BaseXTU);
            sm.programs_running_cfg_xtu.configList.Add(--x, BaseXTU);

            //write back
            log.WriteLog("writeback to file commencing...");
            sm.generatedCLK.completeWriteBack();
            sm.generatedXTU.completeWriteBack();
            sm.programs_running_cfg_cpu.completeWriteBack();
            sm.programs_running_cfg_xtu.completeWriteBack();

            setCLK(sm, clkbackuph, false);   //restore old clk
            setCLK(sm, clkbackupl, true);   //restore old clk
            setXTU(sm, xtubackup);          //restore old xtu
            log.WriteLog("================end of CLK + XTU list generation================");

            sm.IPClocked = false;
        }
        

        //apply based on profile
        public void setNiceProfile(SettingsManager sm)
        {
            sm.IPClocked = true;

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

            sm.IPClocked = false;
        }

    }


}
