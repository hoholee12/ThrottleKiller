using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Net;
using System.Net.Sockets;

using System.Threading;

namespace ConsoleApplication44
{
    class Program
    {
        static void Main(string[] args)
        {
            while (true)
            {
                try
                {
                    TcpClient client = new TcpClient("127.0.0.1", 1999);
                    Byte[] data = System.Text.Encoding.ASCII.GetBytes("sysinfo");

                    NetworkStream stream = client.GetStream();

                    stream.Write(data, 0, data.Length);

                    data = new Byte[256];
                    String response = null;

                    int i = stream.Read(data, 0, data.Length);
                    response = System.Text.Encoding.ASCII.GetString(data, 0, i);
                    Console.WriteLine(response);
                    stream.Close();
                    client.Close();
                }
                catch (Exception) { }
                Thread.Sleep(1000);
            }
        }
    }
}
