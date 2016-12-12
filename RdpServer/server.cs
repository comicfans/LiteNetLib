using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PortMap;
using LiteNetLib;

namespace RdpServer
{
    class server
    {
        static void Main(string[] args)
        {
            int port = 3389;
            if (args.Length != 0)
            {
                Int32.TryParse(args[0], out port);
            }

            PortMapServer server = new PortMapServer(new System.Net.IPEndPoint(NetUtils.DetectHost(), port));

            server.EventLoop();
        }
    }
}
