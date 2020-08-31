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
        ThrottleScheduler ts = new ThrottleScheduler();

        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            ess.WriteLog("service started");
            
            timer.Elapsed += new ElapsedEventHandler(OnTimerCount);
            timer.Interval = 5000;
            timer.Enabled = true;
            
        }

        private void OnTimerCount(Object src, ElapsedEventArgs args) {
            ts.mainflow();
        }

        protected override void OnStop()
        {
            ess.WriteLog("service stopped");
        }
    }
}
