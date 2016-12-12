using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LiteNetLib;
using LiteNetLib.Utils;

namespace catclient.cs
{
    class Program
    {

        class ClientHandler :  INetEventListener
        {
            public void OnNetworkError(NetEndPoint endPoint, int socketErrorCode)
            {
            }

            public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
            {
            }

            public void OnNetworkReceive(NetPeer peer, NetDataReader reader)
            {
                Console.WriteLine("client received:{0} bytes",reader.Data.Length);
            }

            public void OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader, UnconnectedMessageType messageType)
            {
                Console.WriteLine("client received {0} unconnected :{1}",remoteEndPoint,reader.GetString(10));
                Connectted = false;
            }

            public bool Connectted { set; get; }
            public void OnPeerConnected(NetPeer peer)
            {
                Console.WriteLine("connected to {0}",peer);
                Connectted = true;
            }

            public void OnPeerDisconnected(NetPeer peer, DisconnectReason disconnectReason, int socketErrorCode)
            {
                Console.WriteLine("client receive peer {0} disconnected:{1}", peer, disconnectReason);
                Connectted = false;
            }
        }

        static void Main(string[] args)
        {

            var handler= new ClientHandler();

            var client = new NetClient(handler,"a1");


            if (!client.Start())
            {
                Console.WriteLine("client can not start");
                return;
            }

            client.Connect(new NetEndPoint("3.35.159.147"));

            byte[] testData = new byte[13218];

            for (int i=0;i<testData.Length;++i)
            {
                testData[i] = (byte)((int)('0') + (i % 10));
            }

            var counter = 0;
            while (true)
            {
                client.PollEvents();
                Thread.Sleep(15);

                if (handler.Connectted && (counter%100 ==0))
                {
                    Console.WriteLine("send");
                    client.Peer.Send(testData, SendOptions.ReliableOrdered);
                }

                ++counter;
            }

        }
    }
}
