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
        
       
        static void Main(string[] args)
        {
            Auto_PowerManager apm = new Auto_PowerManager();

            if (args.Length == 1) {
                apm.changePath(args[1]);
            
            
            }

            apm.start();
            
        }
    }
}
