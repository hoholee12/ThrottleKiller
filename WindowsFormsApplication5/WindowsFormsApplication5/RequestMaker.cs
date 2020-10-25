using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace WindowsFormsApplication5
{
    class RequestMaker
    {
        public string query(int mode)
        {
            while (true)
            {
                try
                {
                    TcpClient client = new TcpClient("127.0.0.1", 1999);
                    /*
                        * sysinfo
                        * reset
                        * shutdown
                        * cleanup
                        * 
                        */
                    string temp = null;
                    switch (mode) { 
                        case 0:
                            temp = "sysinfo";
                            break;
                        case 1:
                            temp = "cleanup";
                            break;
                        case 2:
                            temp = "shutdown";
                            break;
                        case 3:
                            temp = "reset";
                            break;
                        case 4:
                            temp = "location";
                            break;
                        case 5:
                            temp = "topspeed";
                            break;
                        case 6:
                            temp = "pause";
                            break;
                        case 7:
                            temp = "resume";
                            break;

                    }

                    Byte[] data = System.Text.Encoding.ASCII.GetBytes(temp);

                    NetworkStream stream = client.GetStream();

                    stream.Write(data, 0, data.Length);

                    data = new Byte[256];
                    String response = null;

                    int i = stream.Read(data, 0, data.Length);
                    response = System.Text.Encoding.ASCII.GetString(data, 0, i);
                    stream.Close();
                    client.Close();

                    return response;
                }
                catch (Exception) { }
            }

        }
    }
}
