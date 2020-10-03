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

namespace ThrottleSchedulerService
{
    public partial class Service1 : ServiceBase
    {
        Timer timer = new Timer();
        essentials ess = new essentials();
        int msec = 1000;
        ThrottleScheduler ts;
        Stopwatch stopwatch;

        public Service1()
        {
            InitializeComponent();
            ts = new ThrottleScheduler(msec);
            stopwatch = Stopwatch.StartNew();
        }

        protected override void OnStart(string[] args)
        {
            ess.WriteLog("service started");
            
            timer.Elapsed += new ElapsedEventHandler(OnTimerCount);
            timer.Interval = msec;
            timer.Enabled = true;
            
        }

        private void OnTimerCount(Object src, ElapsedEventArgs args) {
            //actual sync
            stopwatch.Start();
            timer.Enabled = false;

            ts.mainflow();

            //actual sync
            stopwatch.Stop();
            double interval = msec - stopwatch.ElapsedMilliseconds;
            if (interval < 0.1) interval = 0.1;
            timer.Interval = interval;
            timer.Enabled = true;

            ess.WriteLog("service timer interval = " + timer.Interval);
        }

        protected override void OnStop()
        {
            ess.WriteLog("service stopped");
        }
    }
}
