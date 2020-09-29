using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System;
using System.Diagnostics;

namespace ThrottleSchedulerService
{

    //VERY HEAVY DO NOT USE IT TO PROBE
    class TweakerController
    {
        Process pshell;

        public TweakerController(Logger log)
        {
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


            pshell.StartInfo.FileName = "xtucli";



        }

        public float getXTU() {
            //pshell.StartInfo.Arguments = "-noprofile -command \"((& xtucli -t -id 59 | select-string \\\"59\\\" | %{ -split $_ | select -index 5} | out-string) -replace \\\"x\\\",'').trim()\"";
            pshell.StartInfo.Arguments = "-t -id 59";
            pshell.Start();
            string[] result = pshell.StandardOutput.ReadToEnd().Split(' ');
            pshell.WaitForExit();
            string temp = result.Last().Replace("x", "").Trim();
            return float.Parse(temp);
        }

        public void setXTU(double value) {
            pshell.StartInfo.Arguments = "-t -id 59 -v " + value;
            pshell.Start();
            pshell.WaitForExit();
        }
    }
}
