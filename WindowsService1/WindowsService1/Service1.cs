using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Runtime.InteropServices;
using OpenHardwareMonitor;
using System.Management;
using System.IO;

namespace myFuckingService
{
    public partial class MyFuckingService : ServiceBase
    {
        public enum ServiceState
        {
            SERVICE_STOPPED = 0x00000001,
            SERVICE_START_PENDING = 0x00000002,
            SERVICE_STOP_PENDING = 0x00000003,
            SERVICE_RUNNING = 0x00000004,
            SERVICE_CONTINUE_PENDING = 0x00000005,
            SERVICE_PAUSE_PENDING = 0x00000006,
            SERVICE_PAUSED = 0x00000007
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct ServiceStatus {
            public int dwServiceType;
            public ServiceState dwCurrentState;
            public int dwControlsAccepted;
            public int dwWin32ExitCode;
            public int dwServiceSpecificExitCode;
            public int dwCheckPoint;
            public int dwWaitHint;
        }
        
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(System.IntPtr handle, ref ServiceStatus serviceStatus);
        
        //get wmiobject
        //https://docs.microsoft.com/en-us/windows/win32/wmisdk/retrieving-an-instance
        ManagementObject objInst = new ManagementObject("Win32_Processor");

        //power policy classes
        //https://docs.microsoft.com/en-us/previous-versions//dd904518(v=vs.85)?redirectedfrom=MSDN
        //power scheme management
        //https://docs.microsoft.com/ko-kr/windows/win32/power/managing-power-schemes?redirectedfrom=MSDN
        
        //other things
        /*https://docs.microsoft.com/en-us/windows/win32/wmisdk/retrieving-an-instance
         *https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-how-to
         * https://docs.microsoft.com/en-us/dotnet/framework/data/adonet/sql/linq/walkthrough-simple-object-model-and-query-csharp
         * https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.marshal?view=netcore-3.1
         * https://docs.microsoft.com/en-us/dotnet/api/system.data.dataset.load?view=netcore-3.1
         * https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/serialization/
         * https://docs.microsoft.com/en-us/dotnet/api/system.io.file?view=netcore-3.1
         */




        private int eventId = 1;
        private string[] args;
        public MyFuckingService()
        {
            InitializeComponent();
            eventLog1 = new EventLog();
           
            eventLog1.Source = "MySource";
            eventLog1.Log = "MyNewLog";

            createSource();
        }

        public MyFuckingService(bool flag)
        {
            InitializeComponent();
            eventLog1 = new EventLog();

            eventLog1.Source = "MySource";
            eventLog1.Log = "MyNewLog";
        }

        public MyFuckingService(string[] args): this(true)  //ctor chaining
        {
            if (args.Length > 0) eventLog1.Source = args[0];
            if (args.Length > 1) eventLog1.Log = args[1];
            
            createSource();
        }

        private void createSource() {
            if (!EventLog.SourceExists(eventLog1.Source))
            {
                EventLog.CreateEventSource(eventLog1.Source, eventLog1.Log);
            }
        }

        protected override void OnStart(string[] args)
        {
            eventLog1.WriteEntry("auto_powermanager service started.");
            Timer timer = new Timer();
            timer.Interval = 60000;
            timer.Elapsed += new ElapsedEventHandler(this.OnTimer);
            timer.Start();

            ServiceStatus serviceStatus = new ServiceStatus();
            serviceStatus.dwCurrentState = ServiceState.SERVICE_START_PENDING;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

            serviceStatus.dwCurrentState = ServiceState.SERVICE_RUNNING;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);


        }

        protected void OnTimer(object sender, ElapsedEventArgs e)
        {
            //throw new NotImplementedException();
            //eventLog1.WriteEntry("monitoring system", EventLogEntryType.Information, eventId);
            eventId++;
        }

        protected override void OnStop()
        {
            eventLog1.WriteEntry("auto_powermanager service stopped.");

            ServiceStatus serviceStatus = new ServiceStatus();
            serviceStatus.dwCurrentState = ServiceState.SERVICE_STOP_PENDING;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

            serviceStatus.dwCurrentState = ServiceState.SERVICE_STOPPED;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);
        }

        protected override void OnContinue()
        {
            
        }
    }
}
