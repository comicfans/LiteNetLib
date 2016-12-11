using System;
using System.Threading;
using System.Net;
using System.Collections.Concurrent;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Net.Sockets;




namespace PortMap
{
    public class PortMapServer : INetEventListener
    {
        private static SimpleLogger _l = new SimpleLogger("server");

        public NetServer _server;

        private ConcurrentDictionary<NetPeer,TargetSocketThread> _peerMap=new ConcurrentDictionary<NetPeer,TargetSocketThread>();
        private ConcurrentDictionary<NetPeer,BlockingCollection<DataPair> > _peerData=new ConcurrentDictionary<NetPeer,BlockingCollection<DataPair>>();

        private IPEndPoint _targetEndPoint;
        public PortMapServer(IPEndPoint targetEndPoint)
        {
            _targetEndPoint=targetEndPoint;
            _server=new NetServer(this,1,"myapp1");

            if(!_server.Start()){

                _l.E("server start failed...");
                throw new SocketException((int)SocketError.Fault);
            }

            _eventLoopThread = new Thread(EventLoop);
        }


        private Thread _eventLoopThread;
        public void Start(){
            _serverRunning=true;
            _eventLoopThread.Start();
        }

        private bool _serverRunning=true;

        public void Stop(){
            _serverRunning=true;
            _eventLoopThread.Join();
        }

        private void EventLoop(){
            _l.I("start event loop...");
            while(_serverRunning){
                _l.T("poll server events...");
                _server.PollEvents();


                var en = _peerData.GetEnumerator();
                while (en.MoveNext())
                {
                    DataPair pair;


                    _l.D("sending queue for {0}",en.Current.Key);
                    while (en.Current.Value.TryTake(out pair))
                    {
                        _l.T("before send for {0}",en.Current.Key);
                        en.Current.Key.Send(pair.Data, 0, pair.Size, SendOptions.ReliableOrdered);
                        _l.T("end send for {0}",en.Current.Key);
                    }
                }

                Thread.Sleep(50);
            }
        }

        BlockingCollection<DataPair> _fromServer = new BlockingCollection<DataPair>();
        public void OnPeerConnected(NetPeer peer)
        {

            _l.I("[Server] Peer connected: {0}", new object[] { peer.EndPoint });

            DataCallback func = (DataPair pair) =>
            {
                _fromServer.Add(pair);
            };

            try
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Blocking = true;

                socket.Connect(_targetEndPoint);
                _l.I("[Server] connect to {0}", new object[] { _targetEndPoint });

                var newThread = new TargetSocketThread(socket, func);
                newThread.Name = "Server";
                newThread.OnDisconnect += (sender, nouse) => { _server.DisconnectPeer(peer); };
                bool shouldTrue=_peerMap.TryAdd(peer, newThread);

                _l.D("add id {0} to peer map ", peer.Id);
                if (!shouldTrue)
                {
                    _l.D("add id {0} to peer map failed!", peer.Id);
                }
                newThread.Start();
            }
            catch (SocketException)
            {
                _l.E("peer {0} can not connect to {1}, disconnect", new object[] { peer, _targetEndPoint });
                _server.DisconnectPeer(peer);
            }
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectReason disconnectReason, int socketErrorCode)
        {
            _l.W("[Server] Peer disconnected: {0}, reason: {1}", new object[] { peer.EndPoint, disconnectReason });

            TargetSocketThread thread;
            var removed = _peerMap.TryRemove(peer, out thread);
            if (!removed)
            {
                //thread not create
                return;
            }
            _l.I("stop thread", peer.EndPoint, disconnectReason );
            thread.Stop();
        }

        public void OnNetworkError(NetEndPoint endPoint, int socketErrorCode)
        {
            _l.E("[Server] error: ");
        }

        public void OnNetworkReceive(NetPeer peer, NetDataReader reader)
        {
            TargetSocketThread thread;

            _l.D("[Server] receive {0} bytes", reader.Data.Length);

            _peerMap.TryGetValue(peer, out thread);

            byte[] copy = new byte[reader.Data.Length];

            Array.Copy(reader.Data, copy, copy.Length);

            thread.AppendToTarget(new DataPair(copy));

        }

        public void OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader, UnconnectedMessageType messageType)
        {
            _l.I("[Server] ReceiveUnconnected: {0}",messageType);
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {

        }

    }
}
