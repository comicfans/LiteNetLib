using System;
using System.Net;
using PortMap;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RdpClient
{
    class client
    {
        static void Main(string[] args)
        {
            PortMapClient client = new PortMapClient(new IPEndPoint(IPAddress.Parse("127.0.0.1"),8888),IPAddress.Parse(args[0]));

            client.EventLoop();
        }
    }
}
