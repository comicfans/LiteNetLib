﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;

namespace PortMap
{
    public class TargetSocketThread
    {

        private SimpleLogger _rl; 
        private SimpleLogger _wl;
        
        public event EventHandler OnDisconnect;

        private readonly Thread _readThread;
        private readonly Thread _writeThread;
        private DataCallback _fromSocket;

        private readonly Socket _socket;
        public TargetSocketThread(Socket targetSocket,DataCallback callback)
        {
            
            _socket = targetSocket;
            _fromSocket = callback;
            _readThread = new Thread(ReadThreadFunc);
            _writeThread = new Thread(WriteThreadFunc);
        }

        public string Name { set; get; } = "SocketThread";

        public void Start(){

            _readThread.Name = Name + ":read";
            _writeThread.Name = Name + ":write";
                
            _rl = new SimpleLogger(_readThread.Name);
            _wl = new SimpleLogger(_writeThread.Name);

            _readThread.Start();
            _writeThread.Start();
        }

        private BlockingCollection<DataPair> _toTarget=new BlockingCollection<DataPair>();

        private bool _running = true;

        public void AppendToTarget(DataPair towrite)
        {
            _toTarget.Add(towrite);
        }

        private void ReadThreadFunc()
        {

            try{
                while (_running)
                {
                    _rl.T("begin read");
                    var readed=new byte[2048] ;
                    var length=_socket.Receive(readed);
                    if (length == 0)
                    {
                        throw new SocketException((int)SocketError.Shutdown);
                    }
                    _rl.D("read {0}", length);
                    _fromSocket(new DataPair(readed, length));
                }
            }
            catch(SocketException e){
                _rl.E("socket exception {0},call disconnect",e);
                OnDisconnect(this,null);
            }
        }

        private void WriteThreadFunc()
        {
            try
            {
                while (_running)
                {
                    var data= _toTarget.Take();
                    if (data == null)
                    {
                        //stopped
                        break;
                    }
                    _wl.D("taken {0} and send to socket",data.Size);
                    _socket.Send(data.Data,0,data.Size,SocketFlags.None);
                }
            }
            catch(SocketException e)
            {
                _wl.E("socket exception: {0},call disconnect", e);
                OnDisconnect(this,null);
            }
        }

        private static SimpleLogger _l= new SimpleLogger("TargetSocketThread");
        public void Stop()
        {

            _running = false;
            _l.D("call stop,disconnect/close socket");
            _socket.Disconnect(false);
            _socket.Close();
            _l.D("socket closed, wait thread join");
            _toTarget.Add(null);
            _readThread.Join();
            _writeThread.Join();
            _l.D("thread joined");
            OnDisconnect(this,null);
        }
    }
}
