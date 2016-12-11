using PortMap;
using LiteNetLib;
using System;

namespace TestPortMap
{

    class Program
    {
        static void Main(string[] args)
        {

            PortMapServer server = new PortMapServer(new System.Net.IPEndPoint(NetUtils.DetectHost(), 8899));

            PortMapClient client = new PortMapClient(new System.Net.IPEndPoint(NetUtils.DetectHost(), 8888));

            server.Start();
            client.Start();

            Console.ReadKey();
        }
    }
}
