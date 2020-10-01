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
        * 6. check list and apply accordingly           
        * 7. change setting if still throttles
        * 7.5 writeback changes to settings file
        * 8. sleep                                      - DONE
        * 9. loop to 2.                                 - DONE
        * 
        */

        public TweakerChecker checker;
        public SettingsManager settings;
        public Logger log;
        public TweakerController controller;

        
        //init every objects here!
        public ThrottleScheduler()
        {
            checker = new TweakerChecker();

            //messy init
            settings = new SettingsManager();
            settings.initPath();
            log = new Logger(settings.path, settings.cfgname);
            settings.initConfig(log);
            checker.initCLK(log);
            checker.initTemp();

            controller = new TweakerController(log, checker);


            //initial load
            initflow();

        }

        public void initflow() {
            controller.initPowerCFG(settings);
            checker.initXTU(controller);
        }

        //start main loop
        public void mainflow()
        {
            settings.checkSettingsInit();   //no need to save io here
            log.WriteLog("clk:" + checker.getCLK() + ", load:" + checker.getLoad() + ", temp:" + checker.getTemp());
            if (checker.isCurrentlyThrottling(settings)) log.WriteLog("throttle detected!");
            //controller.setXTU(10.5);
            //checker.detectFgProc(settings);
            //controller.setCLK(settings, 100, 1);
            controller.setProcNice(checker.detectFgProc(settings), settings);

            controller.getPWR();
        }

        
    }

}
