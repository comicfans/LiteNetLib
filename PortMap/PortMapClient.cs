using LiteNetLib;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using LiteNetLib.Utils;

namespace PortMap
{
    public class PortMapClient: INetEventListener
    {

        private Socket _listenSocket;

        private static SimpleLogger _l = new SimpleLogger("client");
        public PortMapClient(IPEndPoint listenEndPoint,IPAddress serverAddr){


            _serverAddr = serverAddr;
            _eventLoopThread =new Thread(EventLoop);
            _listenSocket=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);
            _listenSocket.Bind(listenEndPoint);
            _listenSocket.Listen(1);

        }

        private bool _running=true;

        private IPAddress _serverAddr;

        private Thread _eventLoopThread;
        public void Start(){
            _clientRunning=true;
            _eventLoopThread.Start();
        }

        public void Stop(){
            _clientRunning=false;
            _eventLoopThread.Join();
        }

        private bool _clientRunning=true;


        private BlockingCollection<DataPair> _fromClient = new BlockingCollection<DataPair>();

        private void EventLoop(){

            while(_clientRunning){
                var clientSocket=_listenSocket.Accept();
                _running = true;

                _netClient=new NetClient(this,"myapp1");
                _netClient.SimulationMaxLatency= 1500;
                _netClient.MergeEnabled = true;
                if (!_netClient.Start()){
                    _l.E("client start failed!");
                    Thread.Sleep(500);
                    continue;
                }

                _netClient.Connect(new NetEndPoint(new IPEndPoint(_serverAddr,0)));

                DataCallback func=pair=> {
                    _fromClient.Add(pair);
                };
                _targetThread=new TargetSocketThread(clientSocket,func);
                _targetThread.Name = "client";
                _targetThread.OnDisconnect+=(sender,nouse)=>{
                    _l.I("local socket disconnect");
                    _netClient.Disconnect();
                };
                _targetThread.Start();

                while(_running){
                    _l.T("poll events");
                    _netClient.PollEvents();

                    DataPair fromClient;

                    while(_fromClient.TryTake(out fromClient))
                    {
                        if (_netClient.Peer==null)
                        {
                            continue;
                        }
                        _l.T("send to peer {0},{1} bytes",_netClient.Peer.EndPoint, fromClient.Size);
                        _netClient.Peer.Send(fromClient.Data, 0, fromClient.Size,SendOptions.ReliableOrdered);
                    }
                    Thread.Sleep(50);
                }

                _targetThread.Stop();
            }
        }

        
        private TargetSocketThread _targetThread;

        private NetClient _netClient;

        public void OnPeerConnected(NetPeer peer)
        {
            _l.I("connected to: {0}", peer.EndPoint);
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectReason disconnectReason, int socketErrorCode)
        {
            if (peer == null)
            {
                _l.W("null peer disconnect ?reason {0}",disconnectReason);
                return;
            }
            _l.W("peer to server {0} disconnect,reason {1}",peer.EndPoint,disconnectReason);
            _running = false;
        }

        public void OnNetworkError(NetEndPoint endPoint, int socketErrorCode)
        {
            _l.E("network error to {0}:{1}", endPoint,socketErrorCode);
        }

        public void OnNetworkReceive(NetPeer peer, NetDataReader reader)
        {
            if (reader.Data.Length==0)
            {
                _running = false;
                return;
            }
            _l.T("receive {0} bytes from {1}", reader.Data.Length,peer.EndPoint);
            byte[] copy=new byte[reader.Data.Length];
            Array.Copy(reader.Data, copy, copy.Length);
            _targetThread.AppendToTarget(new DataPair(copy));
        }

        public void OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader, UnconnectedMessageType messageType)
        {
            _l.I("receive {0} unconnect {1}", remoteEndPoint,messageType);
            _running = false;
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {

        }
    }
 }
