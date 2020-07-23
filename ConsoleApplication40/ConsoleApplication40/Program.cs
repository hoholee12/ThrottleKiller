using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace ConsoleApplication40
{
    class Program
    {
        static AutoPowerManager autoPowerManager;
       
        static void Main(string[] args)
        {
            autoPowerManager = new AutoPowerManager();

            if (args.Length == 1) {
                autoPowerManager.changePath(args[1]);
            
            
            }

            autoPowerManager.start();
            
        }
    }
}
