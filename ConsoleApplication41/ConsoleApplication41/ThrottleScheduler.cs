using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Diagnostics;
using System.IO;
using System.Management;
using OpenHardwareMonitor.Hardware;

using System.Timers;
using System.Threading;

namespace ThrottleSchedulerService
{
    class ThrottleScheduler
    {
        /*TODO: port everything from xtu_scheduler.ps1
        auto_powermanager core
        * 1. get list from db                           - DONE
        * 2. check hardware                             - DONE
        * 3. check list and apply accordingly(nice)     - DONE
        * 4. check if list changed(reload if changed)   - DONE
        * 5. check throttle                             - DONE
        * 6. check list and apply accordingly           - DONE
        * 7. change setting if still throttles          - DONE
        * 7.5 writeback changes to settings file        - DONE
        * 8. sleep                                      - DONE
        * 9. loop to 2.                                 - DONE
        * 10. generate CLK list                         - DONE
        * 11. add mechanism for new apps                - DONE
        * 12. IPC with GUI app                          - DONE
        * 
        * 13. detect processor/io based workload with getProcessIoCounters  - WIP
        * 
        */

        //CLK = powerplan value, PWR = real clockspeed

        public TweakerChecker checker;
        public SettingsManager settings;
        public Logger log;
        public TweakerController controller;

        public bool pause = false;

        int msec;

        bool shutdownval = false;

        Process prev_currfg = null;
        
        //init every objects here!
        public ThrottleScheduler(int msec)
        {
            this.msec = msec;
            checker = new TweakerChecker();

            //messy init
            settings = new SettingsManager();
            settings.initPath();
            log = new Logger(settings.path, settings.cfgname);
            settings.initConfig(log);
            checker.initPWR(log);
            checker.initTemp();

            //timesync
            settings.initTimeSync(msec);

            controller = new TweakerController(log, checker, settings);

            //initial load
            initflow();

        }

        public void initflow() {
            controller.initPowerCFG(settings);

            //init value counters            
            log.WriteLog("init value counters: maxpwr = " + checker.getMaxPWR() + ", turbopwr = " + checker.getTurboPWR());
            log.WriteLog("====================end of core init====================");
        }

        ///////////////////////////////////////////for client
        public string getSysInfo() {
            while (settings.IPClocked) Thread.Sleep(100);
            return checker.autofilterPWR(checker.getPWR()) + " " + checker.getLoad() + " " + checker.getTemp() + " " + checker.lastprocname
                 + " " + settings.new_acc_msec.ToString() + " " + settings.bc_acc_msec.ToString() + " " + settings.resur_acc_msec.ToString()
                 + " " + controller.wrong.ToString();
        }
        public string reset() {
            while (settings.IPClocked) Thread.Sleep(100);
            settings.generatedCLK.resetFiles();
            settings.generatedXTU.resetFiles();
            settings.programs_running_cfg_cpu.resetFiles();
            settings.programs_running_cfg_nice.resetFiles();
            settings.programs_running_cfg_xtu.resetFiles();
            return log.WriteLog("clklist_reset.");
        }
        public string shutdown() {
            //while (settings.IPClocked) Thread.Sleep(100);
            shutdownval = true;
            return log.WriteLog("shutting_down.");
        }
        public string cleanup() {
            while (settings.IPClocked) Thread.Sleep(100);
            settings.special_programs.completeWriteBack();
            settings.programs_running_cfg_cpu.completeWriteBack();
            settings.programs_running_cfg_xtu.completeWriteBack();
            return log.WriteLog("tidying up config files.");
        }
        public string pauseme() {
            settings.resetNewlistSync();
            settings.resetResurSync();
            settings.resetThrottleSync();
            pause = true;
            return log.WriteLog("paused.");
        }
        public string resumeme() {
            pause = false;
            return log.WriteLog("resumed.");
        }
        ///////////////////////////////////////////for client
        //start main loop
        public void mainflow()
        {
            if (shutdownval) Environment.Exit(0);

            settings.checkTimeSync();   //update changed time settings
            
            //internal sync
            if (settings.timeSync && !controller.wrong && !pause) //skip when xtu settings bad, or user request
            {
                controller.XTUdaemon(settings, checker);

                //1. check config
                controller.generateCLKlist(settings, checker);  //before batchCheck
                settings.batchCheckFiles();   //no need to save io here
                if (settings.checkPowerCFGFlag)
                {
                    controller.initPowerCFG(settings);
                    controller.forceApply = true;   //just in case
                }

                //2. apply settings(add app if dont exist)
                var currfg = checker.detectFgProc(settings);
                checker.autoCheckInsert(currfg, settings, controller);
                //controller.setProcNice(currfg, settings);
                controller.setPower(currfg, settings);

                //3. check throttle
                /*
                 * 
                 *  per profile scheduling.
                 * 
                 *  store intermediate values on settingsmanager.
                 * 
                 *  on throttle:
                 *      cpuload 80%(tweakable)
                 *      -get median upto throttleSync(tweakable)
                 *  
                 *  if app on the same profile throttles:
                 *      profile gets modified affecting other relevant apps.
                 *  
                 *  if app is not in any profile:
                 *      monitor performance for throttleSync(tweakable) cycles and assign to closest one.
                 *      
                 */
                

                /*current app info
                *  1. skip if its not in list
                *  2. which throttle mode
                */

                //reset timers (except newlist does it internally)
                if(prev_currfg != null)
                    if (currfg.Id != prev_currfg.Id) {
                        settings.resetThrottleSync();
                        settings.resetResurSync();
                    }
                prev_currfg = currfg;

                //current profile:
                int currprof = controller.checkInList(currfg, settings);

                //if app has a profile:
                if (currprof != -1)
                {
                    if (checker.isCurrentlyThrottling(settings, controller))
                    {

                        int mode = settings.throttleMode;   //save mode
                        settings.throttleMode = 0;  //reset

                        //median limit
                        int limit = (int)settings.newlist_median.configList["newlist_median"];    //ex) 50
                        int listcount = settings.generatedCLK.configList.Count();                 //ex) 12
                        int indexlimit = listcount - listcount * limit / 100;                     //ex) 6

                        //new clk value for cpu throttle (newclk)
                        var gclklist = checker.sortedCLKlist(settings);
                        int newindex = gclklist.IndexOf(controller.getCLK(false));
                        if (newindex > indexlimit) newindex--; //ensure 0 is the end(noindex is -1)
                        int newclk = gclklist.ElementAt(newindex);    //clk value goes in

                        //new xtu value for gpu throttle (newxtu)
                        float newxtu = controller.getXTU();
                        if (newxtu > controller.getBaseXTU(settings)) newxtu -= 0.5f;

                        switch (mode)
                        {
                            case 0: break;
                            case 1: //cpu
                                settings.programs_running_cfg_cpu.appendChanges(currprof, newclk);

                                break;
                            case 2: //gpu
                                settings.programs_running_cfg_xtu.appendChanges(currprof, newxtu);


                                break;

                        }

                    }
                    if (checker.isViableForResurrect(settings, controller))
                    {
                        int mode = settings.resurrectMode;
                        settings.resurrectMode = 0;

                        //median limit
                        int limit = (int)settings.newlist_median.configList["newlist_median"];    //ex) 50
                        int listcount = settings.generatedCLK.configList.Count();                 //ex) 12
                        int indexlimit = listcount - listcount * limit / 100;                     //ex) 6

                        string name = controller.checkNameInList(currfg, settings);
                        switch (mode)
                        {
                            case 0: break;
                            case 1:
                                if (currprof > 0) settings.special_programs.appendChanges(name, currprof - 1);
                                break;
                            case 2:
                                if (currprof < indexlimit) settings.special_programs.appendChanges(name, currprof + 1);
                                break;
                        }

                    }
                }   
                    
                    
            }
            settings.updateTimeSync();
            checker.resettick();

        }
        
        
    }

}
