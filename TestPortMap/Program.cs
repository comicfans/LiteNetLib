using PortMap;
using LiteNetLib;
using System;

namespace TestPortMap
{

    class Program
    {
        static void Main(string[] args)
        {

            PortMapServer server = new PortMapServer(new System.Net.IPEndPoint(NetUtils.DetectHost(), 3389));

            PortMapClient client = new PortMapClient(new System.Net.IPEndPoint(NetUtils.DetectHost(), 3388),NetUtils.DetectHost());

            server.Start();
            client.Start();

            Console.ReadKey();
        }
    }
}
