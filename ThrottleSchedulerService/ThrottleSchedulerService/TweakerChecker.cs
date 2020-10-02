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

        int MaxClockSpeed;
        float MaxXTU;   //safe measure

        int throttledelay = 0; //for throttling check margin

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


        public int getLoad()
        {
            while (true)
            {
                try
                {
                    obj.Get();
                    return int.Parse(obj["LoadPercentage"].ToString());
                }
                catch (Exception) { }
            }

        }
        public int getPWR()
        {
            while (true)
            {
                try
                {
                    obj.Get();
                    return int.Parse(obj["CurrentClockSpeed"].ToString());
                }
                catch (Exception) { }
            }
        }
        public int getMaxPWR() {
            while (true)
            {
                try
                {
                    obj.Get();
                    return int.Parse(obj["MaxClockSpeed"].ToString());
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

        public int getTemp()
        {
            computer.Accept(updateVisitor);
            return int.Parse(computer.Hardware[prevtemp_i].Sensors[prevtemp_j].Value.ToString());
            
        }
        //for management wmi

        public void initPWR(Logger log) {
            this.log = log;
            MaxClockSpeed = getMaxPWR();
        }

        /* its currently throttling if:
         * 1. get default high watermark from:
         *              06cadf0e-64ed-448a-8927-ce7bf90eb35d = 30   //rocket
         * 2. if load is over 30 and clockspeed is not fullspeed -> throttling.
         */
        public bool isCurrentlyThrottling(SettingsManager sm){
            try
            {
                /*
                 * 1. keep checking throttle
                 * 2. on throttleSync:  //for margin of error
                 * 3.   if throttledelay != 0:
                 * 4.       return true
                 */
                
                //<string, int>
                int high = (int)sm.processor_guid_tweak.configList["06cadf0e-64ed-448a-8927-ce7bf90eb35d"];
                int load = getLoad();
                int clk = getPWR();
                if (load > high && clk < MaxClockSpeed)
                {
                    log.WriteLog("throttle? load = " + load + " ,clk = " + clk);
                    throttledelay++;
                }
                else{
                    if(throttledelay > 0)
                        throttledelay--;
                }

                if (sm.throttleSync && throttledelay > 0) return true;
                else return false;

            }
            catch (Exception) { //config file bug
                log.WriteErr("config file is broken");
                return false;   //this will never be reached
            }
        }

        //return the process obj if any process in the list runs as focused
        public Process detectFgProc(SettingsManager sm)
        {
            uint processID = 0;
            IntPtr hWnd = GetForegroundWindow(); // Get foreground window handle
            uint threadID = GetWindowThreadProcessId(hWnd, out processID); // Get PID from window handle
            Process fgProc = Process.GetProcessById(Convert.ToInt32(processID)); // Get it as a C# obj.
            // NOTE: In some rare cases ProcessID will be NULL. Handle this how you want. 

            return fgProc;
        }

        //safe measure
        public void initXTU(TweakerController controller) {
            MaxXTU = controller.getXTU();
        }


    }
}
