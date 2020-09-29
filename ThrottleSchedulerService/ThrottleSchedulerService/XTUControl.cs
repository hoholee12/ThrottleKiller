using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System;
using System.Diagnostics;

namespace ThrottleSchedulerService
{
    class XTUControl
    {
        Process pshell;

        public XTUControl(Logger log) {
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
                pshell.WaitForExit();

                if (pshell.ExitCode == 1)
                {
                    log.WriteLog("XTU cmdlet missing, cannot continue.");
                    Environment.Exit(1);
                }
            }
            catch (Exception e)
            {
                log.WriteLog(e.ToString());
            }
            finally {
                log.WriteLog("XTU ok");
            }

        }

        public float getXTU() {
            pshell.StartInfo.Arguments = "-noprofile -command \"((& xtucli -t -id 59 | select-string \\\"59\\\" | %{ -split $_ | select -index 5} | out-string) -replace \\\"x\\\",'').trim()\"";
            pshell.Start();
            string result = pshell.StandardOutput.ReadToEnd();
            pshell.WaitForExit();
            return float.Parse(result);
        }

        public void setXTU(float value) {
            pshell.StartInfo.Arguments = "-noprofile -command \"((& xtucli -t -id 59 -v " + value.ToString() + " | select-string \\\"59\\\" | %{ -split $_ | select -index 5} | out-string) -replace \\\"x\\\",'').trim()\"";
            pshell.Start();
            pshell.WaitForExit();
        }
    }
}
