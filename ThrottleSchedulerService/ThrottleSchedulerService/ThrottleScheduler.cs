using System;
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
        * 
        * 
        * 10. generate CLK list                         - DONE
        * 11. add mechanism for new apps
        * 12. IPC with GUI app
        * 
        */

        //CLK = powerplan value, PWR = real clockspeed

        public TweakerChecker checker;
        public SettingsManager settings;
        public Logger log;
        public TweakerController controller;

        int msec;

        
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

            controller = new TweakerController(log, checker);

            //initial load
            initflow();

        }

        public void initflow() {
            controller.initPowerCFG(settings);
        }

        //start main loop
        public void mainflow()
        {
         
            if (settings.timeSync)
            {
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
                controller.setProcNice(currfg, settings);
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
                log.WriteLog("clockspeed:" + checker.getPWR() + ", load:" + checker.getLoad() + ", temp:" + checker.getTemp());

                /*current app info
                *  1. skip if its not in list
                *  2. which throttle mode
                */

                //current profile:
                int currprof = controller.checkInList(currfg, settings);

                //if app has a profile:
                if (currprof != -1){
                    if (checker.isCurrentlyThrottling(settings, controller)){
                    
                        int mode = settings.throttleMode;   //save mode
                        settings.throttleMode = 0;  //reset

                        //new clk value for cpu throttle (newclk)
                        var gclklist = settings.generatedCLK.configList.Keys.Cast<int>().ToList();
                        gclklist.Sort((a, b) => a.CompareTo(b));    //ascending order
                        int newindex = gclklist.IndexOf(controller.getCLK(false));
                        if (newindex > 0) newindex--; //ensure 0 is the end(noindex is -1)
                        int newclk = gclklist.ElementAt(newindex);    //clk value goes in

                        //new xtu value for gpu throttle (newxtu)
                        float newxtu = controller.getXTU();
                        if (newxtu > controller.getBaseXTU(settings)) newxtu -= 1.0f;

                        switch (mode) {
                            case 0: break;
                            case 1: //cpu
                                settings.programs_running_cfg_cpu.appendChanges(currprof, newclk);

                                break;
                            case 2: //gpu
                                settings.programs_running_cfg_xtu.appendChanges(currprof, newxtu);


                                break;
                        
                        }

                    }
                }
                    
                    
                    
            }
            settings.updateTimeSync();
        }

        
    }

}
