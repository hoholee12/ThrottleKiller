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
    class Tweaker
    {

        public Logger log;

        int MaxClockSpeed;

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
            obj.Get();
            return int.Parse(obj["LoadPercentage"].ToString());
        }
        public int getCLK()
        {
            obj.Get();
            return int.Parse(obj["CurrentClockSpeed"].ToString());
        }
        public int getMaxCLK() {
            obj.Get();
            return int.Parse(obj["MaxClockSpeed"].ToString());
        }

        public int getTemp()
        {
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
                            int hello = int.Parse(computer.Hardware[i].Sensors[j].Value.ToString());
                            computer.Close();
                            return hello;
                        }
                    }
                }
            }
            computer.Close();
            return -1;
        }
        //for management wmi

        public void init(Logger log) {
            this.log = log;
            MaxClockSpeed = getMaxCLK();
        }

        /* its currently throttling if:
         * 1. get default high watermark from:
         *              06cadf0e-64ed-448a-8927-ce7bf90eb35d = 30   //rocket
         * 2. if load is over 30 and clockspeed is not fullspeed -> throttling.
         */
        public bool isCurrentlyThrottling(SettingsToken st){

            return false;
        }

    }
}
