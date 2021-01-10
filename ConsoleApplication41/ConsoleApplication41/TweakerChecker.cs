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
    class TweakerChecker
    {

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public Logger log;

        int MaxClockSpeed = 1234;
        int TurboClockSpeed = 1234;

        public string lastprocname = "null"; //for query

        int throttlecheck = 0; //throttling check aging margin
        List<int> throttle_acc = new List<int>();

        Process temp = null;
        List<int> newlist_high_acc = new List<int>();
        List<int> newlist_low_acc = new List<int>();
        int newlistcheck = 0;   //newlist check average margin

        int resurcheck = 0; //resur check aging margin
        List<int> resur_acc = new List<int>();

        int prevtemp_i = 0, prevtemp_j = 0;   //slow down io usage

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


        public void resettick() {
            loadtick = true;
            pwrtick = true;
            thermaltick = true;
        }

        public bool loadtick = true;
        public int prevload = 0;
        public int getLoad()
        {
            while (true)
            {
                try
                {
                    if (loadtick)
                    {
                        obj.Get();
                        prevload = int.Parse(obj["LoadPercentage"].ToString());
                        loadtick = false;
                    }
                    return prevload;
                }
                catch (Exception) { }
            }

        }
        public bool pwrtick = true;
        public int prevpwr = 0;
        public int getPWR()
        {
            while (true)
            {
                try
                {
                    if (pwrtick)
                    {
                        obj.Get();
                        prevpwr = int.Parse(obj["CurrentClockSpeed"].ToString());
                        pwrtick = false;
                    }
                    return prevpwr;
                }
                catch (Exception) { }
            }
        }
        public int getMaxPWR() {
            while (true)
            {
                if (MaxClockSpeed != 1234) return MaxClockSpeed;
                try
                {
                    obj.Get();
                    MaxClockSpeed = int.Parse(obj["MaxClockSpeed"].ToString());
                    return MaxClockSpeed;
               }
                catch (Exception) { }
            }
        }
        //maxpwr does not return accurate top speed
        //do not rely on this value. only use it for mid throttle check
        //borrowed code from: https://stackoverflow.com/a/57873464/2214712
        private void InfiniteLoop()
        {
            int i = 0;

            while (true)
                i = i + 1 - 1;
        }
        public int getTurboPWR() {
            while (true)
            {
                if (TurboClockSpeed != 1234) return TurboClockSpeed;
                try
                {
                    PerformanceCounter cpuCounter = new PerformanceCounter("Processor Information", "% Processor Performance", "_Total");
                    double cpuValue = cpuCounter.NextValue();

                    Thread loop = new Thread(() => InfiniteLoop());
                    loop.Start();

                    Thread.Sleep(1000);
                    cpuValue = cpuCounter.NextValue();
                    loop.Abort();

                    TurboClockSpeed = (int)cpuValue * getMaxPWR() / 100;
                    return TurboClockSpeed;
                }
                catch (Exception) { }
            }
        }

        public void initTemp() {
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
                            prevtemp_i = i;
                            prevtemp_j = j;
                            return;
                        }
                    }
                }
            }
        }

        public bool thermaltick = true;
        public int prevthermal = 0;
        public int getTemp()
        {
            if (thermaltick)
            {
                computer.Accept(updateVisitor);
                prevthermal = int.Parse(computer.Hardware[prevtemp_i].Sensors[prevtemp_j].Value.ToString());
                thermaltick = false;
            }
            return prevthermal;
            
        }
        //for management wmi

        public void initPWR(Logger log) {
            this.log = log;
            MaxClockSpeed = getMaxPWR();
        }

        //true turboboost speed
        public int autofilterPWR(int pwr) {
            if (pwr == getMaxPWR()) pwr = getTurboPWR();
            return pwr;
        }

        public List<int> sortedCLKlist(SettingsManager sm) {
            sm.IPClocked = true;
            var temp = sm.generatedCLK.configList.Keys.Cast<int>().ToList();
            temp.Sort();
            sm.IPClocked = false;
            return temp;
        }

        public List<int> sortedPWRlist(SettingsManager sm)
        {
            sm.IPClocked = true;
            var temp = sm.generatedCLK.configList.Values.Cast<int>().ToList();
            temp.Sort();
            sm.IPClocked = false;
            return temp;
        }


        //assume cpu is clk 100
        public void autoCheckInsert(Process proc, SettingsManager sm, TweakerController ts)
        {
            sm.IPClocked = true;

            if (ts.checkInList(proc, sm) != -1) return;    //its in the list

            //different process!
            if (temp != null)
                if (temp.Id != proc.Id)
                {
                    newlist_high_acc.Clear();
                    newlist_low_acc.Clear();
                    sm.resetNewlistSync();
                    newlistcheck = 0;
                }

            //insert proc
            temp = proc;


            //launch timer
            sm.startNewlistSync();
            //default cpu speed is always MaxPWR
            int currpwr = autofilterPWR(getPWR());

            int load = getLoad() * currpwr / getTurboPWR();        //include circumstance of throttling
            if (load > 100) load = 100;         //oob
            int throttle_median = (int)sm.throttle_median.configList["throttle_median"];
            int upperlimit = (int)sm.processor_guid_tweak.configList["06cadf0e-64ed-448a-8927-ce7bf90eb35d"];

            
            //capture high load
            if (load >= throttle_median){
                //int hload = (load - throttle_median) * 100 / (100 - throttle_median);
                int hload = load;
                log.WriteLog("newlist accumulation pwr:" + currpwr + " high load:" + hload);
                newlist_high_acc.Add(hload);
            }
            //capture low load
            else{
                int lload = load * 100 / (throttle_median - 1);
                log.WriteLog("newlist accumulation pwr:" + currpwr + " low load:" + lload);
                newlist_low_acc.Add(lload);
            }

            //if time
            if (sm.checkNewlistSync())
            {
                
                /*
                 * get avg.
                 * heavily modified version of exponential moving average(EMA)
                 * use ignoreFlag to ignore repeating high loads
                 * for games with high load on loading time and low load on gameplay
                 */

                //ema for high load
                float havg = 100.0f;
                try { havg = newlist_high_acc[0]; }
                catch { }
                float hsum = 0.0f;
                for (int i = 1; i < newlist_high_acc.Count(); i++) 
                {
                    float next = newlist_high_acc[i];
                    hsum = next + havg + 1.0f;  //prevent divbyzero
                    havg = havg * (1.0f - next / hsum) + next * (next / hsum);
                }

                //ema for low load
                float lavg = 0.0f;
                try { lavg = newlist_low_acc[0]; }
                catch { }
                float lsum = 0.0f;
                for (int i = 1; i < newlist_low_acc.Count(); i++)
                {
                    float next = newlist_low_acc[i];
                    lsum = next + lavg + 1.0f;  //prevent divbyzero
                    lavg = lavg * (1.0f - next / lsum) + next * (next / lsum);
                }
                log.WriteLog("EMA result low:" + (int)lavg + " high:" + (int)havg);
                int medload = 100;                      //pcsx2
                if (havg > lavg) medload = (int)lavg * (throttle_median - 1) / 100;   //dirt rally 2.0

                //anything over throttle_median is CPU heavy and needs immediate attention!
                newlist_high_acc.Clear();    //clear
                newlist_low_acc.Clear();    //clear



                //average pwr(clockspeed)
                //ex) 3100mhz * 3(load) / 100 = 93mhz
                int target = getTurboPWR() * medload / 100;

                //find index of low limit(newlist median)
                int index = 0;
                int limit = (int)sm.newlist_median.configList["newlist_median"];    //ex) 50
                int listcount = sm.generatedCLK.configList.Count();                 //ex) 12
                int indexlimit = listcount * limit / 100;                           //ex) 6
                //get low limit through sorted clk
                foreach(int val in sortedCLKlist(sm)){
                    if (index < indexlimit) {
                        index++;
                    }
                    else
                    {
                        limit = val;
                        break;
                    }
                }

                //convert low limit to pwr
                int pwr = autofilterPWR((int)sm.generatedCLK.configList[limit]);

                //find index for pwr
                //with low limit
                index = sm.generatedCLK.configList.Count();
                foreach(int val in sortedPWRlist(sm)){
                    int val2 = autofilterPWR(val);

                    if (val2 > target && val2 >= pwr)
                    {
                        break;
                    }
                    else
                    {
                        index--;
                    }
                }

                //insert profile
                sm.special_programs.appendChanges(proc.ProcessName, index);
            }


            sm.IPClocked = false;
        }

        /* its currently throttling if:
         * 1. get default high watermark from:
         *              06cadf0e-64ed-448a-8927-ce7bf90eb35d = 30   //rocket
         * 2. if load is over 30 and clockspeed is not fullspeed -> throttling.
         */
        /*
         * sm.throttleMode:
         * 0 -> nein
         * 1 -> cpu(cpu usage under 80)
         * 2 -> gpu(cpu usage over 80)
         * cpu is more important than gpu
         */
        public bool isCurrentlyThrottling(SettingsManager sm, TweakerController ts){
            sm.IPClocked = true;

            try
            {
                /*
                 * ex) 1 sync = 5 cycles
                 * 
                 *  all cycles synchronized under:
                 *      -throttleSync
                 *  
                 *  throttle decision is done by:
                 *      -throttledelay(2/5 no throttle: nothing happens in this sync)
                 *  
                 *  which side to throttle is decided by:
                 *      -throttle_acc median(accumulates all throttled cycle over throttledelay)
                 *      -user tweakable throttle_median bar
                 */
                
                //<string, int>
                int currpwr = autofilterPWR(getPWR());
                int load = getLoad();
                int currclk = ts.getCLK(false);
                int target_pwr = autofilterPWR((int)sm.generatedCLK.configList[currclk]);
                int throttle_median = (int)sm.throttle_median.configList["throttle_median"];
                int upperlimit = (int)sm.processor_guid_tweak.configList["06cadf0e-64ed-448a-8927-ce7bf90eb35d"];

                if (throttlecheck == 0) throttle_acc.Clear();

                if (load >= upperlimit && currpwr < target_pwr)
                {

                    sm.startThrottleSync();
                    throttle_acc.Add(load);
                    throttlecheck++;
                }
                else {
                    if(throttlecheck > 0) throttlecheck--;
                }

                log.WriteLog("accumulation for throttle load = " + load + " ,clk = " + currpwr + " ,throttlecheck = " + throttlecheck);
                

                /*
                 *  on throttle:
                 *      cpuload 80%(tweakable)
                 *      -get average upto throttleSync(tweakable)
                 */

                //on throttleSync timer
                if (sm.checkThrottleSync())
                {
                    sm.throttleMode = 0;

                    /*
                     * get avg.
                     * heavily modified version of exponential moving average(EMA)
                     * EMA is highest, EMA + CMA helps in some scenarios
                     * designed to be more favorable towards higher load for avg
                     */
                    float favg = throttle_acc[0];
                    float lsum = 0.0f;
                    bool ignoreFlag = false;
                    for (int i = 1; i < throttle_acc.Count(); i++)
                    {
                        //ignore
                        if (throttle_acc[i] == 100 && ignoreFlag == false)
                        {
                            ignoreFlag = true;
                            favg = 100.0f;
                            //skip the next one
                        }
                        else if (throttle_acc[i] == 100 && ignoreFlag == true)
                        {
                            continue;   //skip
                        }
                        else if (throttle_acc[i] != 100 && ignoreFlag == true)
                        {
                            ignoreFlag = false;
                        }

                        //favg
                        float next = throttle_acc[i];
                        lsum = next + favg + 1.0f;   //prevent divbyzero
                        favg = favg * (1.0f - next / lsum) + next * (next / lsum);

                    }
                    int avg = (int)favg;    //int version


                    int medload = avg;
                    log.WriteLog("EMA result medload:" + medload);

                    //this is count instead of value
                    int countval = 100 * throttlecheck / (throttle_acc.Count() + 1);    //prevent divbyzero
                    log.WriteLog("count percentage:" + countval);
                    throttle_acc.Clear();    //clear
                    throttlecheck = 0;       //reset acc
                    if (countval < throttle_median)
                    {
                        log.WriteLog("throttle check ignored.");
                        return false;
                    }
                    

                    //throttleMode notifier:
                    //
                    //      dont confuse!
                    //      *throttle_median: cpu load median
                    //      *newlist_median: low limit clk median.
                    //
                    //  if throttle_acc median doesnt exceed default median (ex)80:
                    //      decrease cpu
                    //  else if cpu cant be decreased:
                    //      decrease gpu

                    log.WriteLog("complete throttle sync load(median) = " + medload);

                    //find index of low limit(newlist median)
                    int index = 0;
                    int limit = (int)sm.newlist_median.configList["newlist_median"];    //ex) 50
                    int listcount = sm.generatedCLK.configList.Count();                 //ex) 12
                    int indexlimit = listcount * limit / 100;                           //ex) 6
                    //get low limit through sorted clk
                    foreach (int val in sortedCLKlist(sm))
                    {
                        if (index < indexlimit)
                        {
                            index++;
                        }
                        else
                        {
                            limit = val;
                            break;
                        }
                    }

                    //dont exceed throttle median && current clk is over limit
                    int limitclk = (int)sm.programs_running_cfg_cpu.configList[indexlimit];
                    if (medload < throttle_median && limitclk < currclk)
                    {
                        log.WriteLog("cpu throttle detected!");
                        sm.throttleMode = 1;
                    }
                    else {
                        log.WriteLog("gpu throttle detected!");
                        sm.throttleMode = 2;
                        if (limitclk >= currclk) log.WriteLog("reason: cpu low limit reached (newlist_median)");
                    }

                    sm.IPClocked = false;
                    //initiate throttle!
                    return true;
                }

                sm.IPClocked = false;
                //skip for now
                return false;

            }
            catch (Exception) { //config file bug
                //log.WriteErr("config file is broken");

            }

            sm.IPClocked = false;
            return false;   //this will never reach
        }

        //copy of throttlecheck
        public bool isViableForResurrect(SettingsManager sm, TweakerController ts)
        {
            sm.IPClocked = true;

            try
            {

                /*
                 * resurrect:
                 * 1. needs to check long enough
                 * 2. accumulate average load
                 * 3. use temp instead of clockspeed(pwr)
                 * 
                 * use thermal_median for limit, use newlist_cycle_delay for check
                 */

                //<string, int>
                int currtemp = getTemp();
                int load = getLoad();
                int limit = (int)sm.thermal_median.configList["thermal_median"];
                int currpwr = autofilterPWR(getPWR());
                int accload = getLoad() * currpwr / getTurboPWR();        //include circumstance of throttling
                if (load > 100) load = 100;         //oob
                int throttle_median = (int)sm.throttle_median.configList["throttle_median"];
                int upperlimit = (int)sm.processor_guid_tweak.configList["06cadf0e-64ed-448a-8927-ce7bf90eb35d"];


                if (resurcheck == 0) resur_acc.Clear();

                if (load >= upperlimit && currtemp < limit)
                {


                    //add 100 to mix
                    if (accload >= throttle_median)
                    {
                        accload = 100;
                    }

                    sm.startResurSync();
                    resur_acc.Add(accload);
                    resurcheck++;
                }
                else
                {
                    if (resurcheck > 0) resurcheck--;
                }

                log.WriteLog("accumulation for resur: load:" + accload + " temperature:" + currtemp + " resurcheck:" + resurcheck);


                if (sm.checkResurSync())
                {
                    sm.resurrectMode = 0;

                    /*
                     * get avg.
                     * heavily modified version of exponential moving average(EMA)
                     * use ignoreFlag to ignore repeating high loads
                     * for games with high load on loading time and low load on gameplay
                     */
                    float favg = resur_acc[0];
                    float lsum = 0.0f;
                    bool ignoreFlag = false;
                    for (int i = 1; i < resur_acc.Count(); i++)
                    {
                        //ignore
                        if (resur_acc[i] == 100 && ignoreFlag == false)
                        {
                            ignoreFlag = true;
                            favg = 100.0f;
                            //skip the next one
                        }
                        else if (resur_acc[i] == 100 && ignoreFlag == true)
                        {
                            continue;   //skip
                        }
                        else if (resur_acc[i] != 100 && ignoreFlag == true)
                        {
                            ignoreFlag = false;
                        }

                        //favg
                        float next = resur_acc[i];
                        lsum = next + favg + 1.0f;   //prevent divbyzero
                        favg = favg * (1.0f - next / lsum) + next * (next / lsum);

                    }
                    int avg = (int)favg;    //int version


                    int medload = avg;
                    log.WriteLog("EMA result medload:" + medload);

                    //this is count instead of value
                    int countval = 100 * resurcheck / (resur_acc.Count() + 1);    //prevent divbyzero
                    log.WriteLog("count percentage:" + countval);
                    resur_acc.Clear();    //clear
                    resurcheck = 0;       //reset acc
                    if (countval < throttle_median)
                    {
                        log.WriteLog("resur check ignored.");
                        return false;
                    }


                    if (medload >= throttle_median)
                    {
                        sm.resurrectMode = 1;
                        log.WriteLog("resurrection activated + 1(better cpu)");
                    }
                    else
                    {
                        sm.resurrectMode = 2;
                        log.WriteLog("resurrection activated - 1(better gpu)");
                    }

                    sm.IPClocked = false;

                    //do sth
                    return true;
                }

                sm.IPClocked = false;
                //skip for now
                return false;

            }
            catch { }

            sm.IPClocked = false;
            return false;   //this will never reach
        }

        //return the process obj if any process in the list runs as focused
        public Process detectFgProc(SettingsManager sm)
        {
            uint processID = 0;
            IntPtr hWnd = GetForegroundWindow(); // Get foreground window handle
            uint threadID = GetWindowThreadProcessId(hWnd, out processID); // Get PID from window handle
            Process fgProc = Process.GetProcessById(Convert.ToInt32(processID)); // Get it as a C# obj.
            // NOTE: In some rare cases ProcessID will be NULL. Handle this how you want. 

            lastprocname = fgProc.ProcessName;

            return fgProc;
        }


    }
}
