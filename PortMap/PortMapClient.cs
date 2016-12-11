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
        public PortMapClient(IPEndPoint listenEndPoint){


            _eventLoopThread =new Thread(EventLoop);
            _listenSocket=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);
            _listenSocket.Bind(listenEndPoint);
            _listenSocket.Listen(1);

        }

        private bool _running=true;


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

                _netClient.Connect(new NetEndPoint(new IPEndPoint(NetUtils.DetectHost(),0)));

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
                    _l.I("before poll");
                    _netClient.PollEvents();
                    _l.I("end poll");

                    DataPair fromClient;

                    while(_fromClient.TryTake(out fromClient))
                    {
                        if (_netClient.Peer==null)
                        {
                            continue;
                        }
                        _l.I("begin send to netpeer");
                        _netClient.Peer.Send(fromClient.Data, 0, fromClient.Size,SendOptions.ReliableOrdered);
                        _l.T("end send to netpeer");
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
            _l.I("[Client] connected to: {0}:{1}", peer.EndPoint.Host, peer.EndPoint.Port);
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectReason disconnectReason, int socketErrorCode)
        {
            _l.W("[Client] peer to server disconnect");
            _running = false;
        }

        public void OnNetworkError(NetEndPoint endPoint, int socketErrorCode)
        {
            _l.E("[Client] error! {}", socketErrorCode);
        }

        public void OnNetworkReceive(NetPeer peer, NetDataReader reader)
        {
            if (reader.Data.Length==0)
            {
                _running = false;
                return;
            }
            _l.T("[client] receive {0} bytes", reader.Data.Length);
            byte[] copy=new byte[reader.Data.Length];
            Array.Copy(reader.Data, copy, copy.Length);
            _targetThread.AppendToTarget(new DataPair(copy));
        }

        public void OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader, UnconnectedMessageType messageType)
        {
            _l.I("receive unconnect {}", messageType);
            _running = false;
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {

        }
    }
 }
