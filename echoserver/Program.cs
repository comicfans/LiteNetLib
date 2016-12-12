using System;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace echoserver
{

    class ServerHandler : INetEventListener
    {
        public void OnNetworkError(NetEndPoint endPoint, int socketErrorCode)
        {
            Console.WriteLine("network error");
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
        }

        public void OnNetworkReceive(NetPeer peer, NetDataReader reader)
        {
            Console.WriteLine("server got peer {0} message :{1} bytes", peer, reader.Data.Length);
            peer.Send(reader.GetBytes(), SendOptions.ReliableOrdered);
        }

        public void OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader, UnconnectedMessageType messageType)
        {
            Console.WriteLine("remote {0}, disconnected:{1}, str:{2}",remoteEndPoint,messageType,reader.GetString(10));
        }

        public void OnPeerConnected(NetPeer peer)
        {
            Console.WriteLine("peer {0} connected", peer);
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectReason disconnectReason, int socketErrorCode)
        {

            Console.WriteLine("peer {0} connected {1}", peer,disconnectReason);
        }
    }
    class Program
    {

        static void Main(string[] args)
        {
            var handler = new ServerHandler();

            var server = new NetServer(handler, 10, "a1");

            if (!server.Start())
            {
                Console.WriteLine("can not start");

                return;
            }

            while (true)
            {
                server.PollEvents();
                Thread.Sleep(15);
            }

        }
    }
}
