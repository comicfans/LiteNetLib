#if !WINRT || UNITY_EDITOR
using System;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Threading;
using System.Net.NetworkInformation;

namespace LiteNetLib
{
    internal sealed class NetSocket
    {
        private Socket _udpSocketv4;
        private Socket _udpSocketv6;
        private NetEndPoint _localEndPoint;
        private Thread _threadv4;
        private Thread _threadv6;
        private bool _running;
        private readonly NetBase.OnMessageReceived _onMessageReceived;

        private static readonly IPAddress MulticastAddressV6 = IPAddress.Parse(NetConstants.MulticastGroupIPv6);
        private static readonly bool IPv6Support = false;//Socket.OSSupportsIPv6;
        private const int SocketReceivePollTime = 100000;
        private const int SocketSendPollTime = 5000;

        public NetEndPoint LocalEndPoint
        {
            get { return _localEndPoint; }
        }

        public NetSocket(NetBase.OnMessageReceived onMessageReceived)
        {
            _onMessageReceived = onMessageReceived;
        }

        private void ReceiveLogic(object state)
        {
            Socket socket = (Socket)state;
            EndPoint bufferEndPoint = new IPEndPoint(socket.AddressFamily == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any, 0);
            NetEndPoint bufferNetEndPoint = new NetEndPoint((IPEndPoint)bufferEndPoint);
            byte[] receiveBuffer = new byte[NetConstants.PacketSizeLimit];

            while (_running)
            {
                //wait for data
                if (!socket.Poll(SocketReceivePollTime, SelectMode.SelectRead))
                {
                    continue;
                }

                int result;

                //Reading data
                try
                {
                    byte[] rawpacket= new byte[NetConstants.PacketSizeLimit+Ipv4Header.Ipv4HeaderLength];
                    int rawresult = socket.ReceiveFrom(rawpacket, 0, rawpacket.Length, SocketFlags.None, ref bufferEndPoint);
                    result = rawresult - Ipv4Header.Ipv4HeaderLength;
                    Array.Copy(rawpacket, Ipv4Header.Ipv4HeaderLength, receiveBuffer, 0, result);
                    if (!bufferNetEndPoint.EndPoint.Equals(bufferEndPoint))
                    {
                        bufferNetEndPoint = new NetEndPoint((IPEndPoint)bufferEndPoint);
                    }
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.ConnectionReset ||
                        ex.SocketErrorCode == SocketError.MessageSize)
                    {
                        //10040 - message too long
                        //10054 - remote close (not error)
                        //Just UDP
                        NetUtils.DebugWrite(ConsoleColor.DarkRed, "[R] Ingored error: {0} - {1}", ex.ErrorCode, ex.ToString() );
                        continue;
                    }
                    NetUtils.DebugWriteError("[R]Error code: {0} - {1}", ex.ErrorCode, ex.ToString());
                    _onMessageReceived(null, 0, (int)ex.SocketErrorCode, bufferNetEndPoint);
                    continue;
                }

                //All ok!
                NetUtils.DebugWrite(ConsoleColor.Blue, "[R]Recieved data from {0}, result: {1}", bufferNetEndPoint.ToString(), result);
                _onMessageReceived(receiveBuffer, result, 0, bufferNetEndPoint);
            }
        }

        public bool Bind()
        {
            _udpSocketv4 = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp);
            _udpSocketv4.Blocking = false;
            _udpSocketv4.ReceiveBufferSize = NetConstants.SocketBufferSize;
            _udpSocketv4.SendBufferSize = NetConstants.SocketBufferSize;
            _udpSocketv4.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, NetConstants.SocketTTL);
            _udpSocketv4.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.DontFragment, true);
            _udpSocketv4.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.HeaderIncluded, 1);

            IPAddress myaddr = NetUtils.DetectHost();
            
            if(myaddr == null)
            {
                return false;
            }

            if (!BindSocket(_udpSocketv4, new IPEndPoint(myaddr, 0)))
            {
                return false;
            }

            try
            {
                _udpSocketv4.IOControl(IOControlCode.ReceiveAll, new byte[] { 1, 0, 0, 0 }, new byte[] { 1, 0, 0, 0 });
            }
            catch (SocketException)
            {
                return false;
            }
            _localEndPoint = new NetEndPoint((IPEndPoint)_udpSocketv4.LocalEndPoint);

            _running = true;
            _threadv4 = new Thread(ReceiveLogic);
            _threadv4.Name = "SocketThreadv4";
            _threadv4.IsBackground = true;
            _threadv4.Start(_udpSocketv4);

            return true;
        }

        private bool BindSocket(Socket socket, IPEndPoint ep)
        {
            try
            {
                socket.Bind(ep);
                NetUtils.DebugWrite(ConsoleColor.Blue, "[B]Succesfully binded to port: {0}", ((IPEndPoint)socket.LocalEndPoint).Port);
            }
            catch (SocketException ex)
            {
                NetUtils.DebugWriteError("[B]Bind exception: {0}", ex.ToString());
                //TODO: very temporary hack for iOS (Unity3D)
                if (ex.SocketErrorCode == SocketError.AddressFamilyNotSupported)
                {
                    return true;
                }
                return false;
            }
            return true;
        }

        byte[] AppendIpHeader(byte[] data,int offset,int size,IPAddress thatIp)
        {
            Ipv4Header packet = new Ipv4Header();
            packet.Version = 4;
            packet.Protocol = (byte)1;
            packet.Ttl = NetConstants.SocketTTL;
            packet.Offset = 0;
            packet.Length = (byte)Ipv4Header.Ipv4HeaderLength;
            packet.TotalLength = (ushort)(Ipv4Header.Ipv4HeaderLength + size);
            packet.SourceAddress = _localEndPoint.EndPoint.Address;
            packet.DestinationAddress = thatIp;
            byte[] ret = packet.BuildPacket(data, offset, size);
            return ret;
        }

        public int SendTo(byte[] data, int offset, int size, NetEndPoint remoteEndPoint, ref int errorCode)
        {
            try
            {
                int result = 0;
                if (remoteEndPoint.EndPoint.AddressFamily == AddressFamily.InterNetwork)
                {
                    if (!_udpSocketv4.Poll(SocketSendPollTime, SelectMode.SelectWrite))
                        return -1;
                    byte[] appended = AppendIpHeader(data, offset, size, remoteEndPoint.EndPoint.Address);
                    result = _udpSocketv4.SendTo(appended, new IPEndPoint(remoteEndPoint.EndPoint.Address,0))-Ipv4Header.Ipv4HeaderLength;
                }else
                {
                    return -1;
                }

                NetUtils.DebugWrite(ConsoleColor.Blue, "[S]Send packet to {0}, result: {1}", remoteEndPoint.EndPoint, result);
                return result;
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode != SocketError.MessageSize)
                {
                    NetUtils.DebugWriteError("[S]" + ex);
                }
                
                errorCode = ex.ErrorCode;
                return -1;
            }
            catch (Exception ex)
            {
                NetUtils.DebugWriteError("[S]" + ex);
                return -1;
            }
        }

        public void Close()
        {
            _running = false;

            //Close IPv4
            if (Thread.CurrentThread != _threadv4)
            {
                _threadv4.Join();
            }
            _threadv4 = null;
            if (_udpSocketv4 != null)
            {
                _udpSocketv4.Close();
                _udpSocketv4 = null;
            }

            //No ipv6
            if (_udpSocketv6 == null)
                return;

            //Close IPv6
            if (Thread.CurrentThread != _threadv6)
            {
                _threadv6.Join();
            }
            _threadv6 = null;
            if (_udpSocketv6 != null)
            {
                _udpSocketv6.Close();
                _udpSocketv6 = null;
            }
        }
    }
}
#else
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace LiteNetLib
{
    internal sealed class NetSocket
    {
        private DatagramSocket _datagramSocket;
        private readonly Dictionary<NetEndPoint, IOutputStream> _peers = new Dictionary<NetEndPoint, IOutputStream>();
        private readonly NetBase.OnMessageReceived _onMessageReceived;
        private readonly byte[] _byteBuffer = new byte[NetConstants.PacketSizeLimit];
        private readonly IBuffer _buffer;
        private NetEndPoint _bufferEndPoint;
        private NetEndPoint _localEndPoint;
        private static readonly HostName BroadcastAddress = new HostName("255.255.255.255");
        private static readonly HostName MulticastAddressV6 = new HostName(NetConstants.MulticastGroupIPv6);

        public NetEndPoint LocalEndPoint
        {
            get { return _localEndPoint; }
        }

        public NetSocket(NetBase.OnMessageReceived onMessageReceived)
        {
            _onMessageReceived = onMessageReceived;
            _buffer = _byteBuffer.AsBuffer();
        }
        
        private void OnMessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            var result = args.GetDataStream().ReadAsync(_buffer, _buffer.Capacity, InputStreamOptions.None).AsTask().Result;
            int length = (int)result.Length;
            if (length <= 0)
                return;

            if (_bufferEndPoint == null ||
                !_bufferEndPoint.HostName.IsEqual(args.RemoteAddress) ||
                !_bufferEndPoint.PortStr.Equals(args.RemotePort))
            {
                _bufferEndPoint = new NetEndPoint(args.RemoteAddress, args.RemotePort);
            }
            _onMessageReceived(_byteBuffer, length, 0, _bufferEndPoint);
        }

        public bool Bind(int port)
        {
            _datagramSocket = new DatagramSocket();
            _datagramSocket.Control.InboundBufferSizeInBytes = NetConstants.SocketBufferSize;
            _datagramSocket.Control.DontFragment = true;
            _datagramSocket.Control.OutboundUnicastHopLimit = NetConstants.SocketTTL;
            _datagramSocket.MessageReceived += OnMessageReceived;

            try
            {
                _datagramSocket.BindServiceNameAsync(port.ToString()).AsTask().Wait();
                _datagramSocket.JoinMulticastGroup(MulticastAddressV6);
                _localEndPoint = new NetEndPoint(_datagramSocket.Information.LocalAddress, _datagramSocket.Information.LocalPort);
            }
            catch (Exception ex)
            {
                NetUtils.DebugWriteError("[B]Bind exception: {0}", ex.ToString());
                return false;
            }
            return true;
        }

        public bool SendBroadcast(byte[] data, int offset, int size, int port)
        {
            var portString = port.ToString();
            try
            {
                var outputStream =
                    _datagramSocket.GetOutputStreamAsync(BroadcastAddress, portString)
                        .AsTask()
                        .Result;
                var writer = outputStream.AsStreamForWrite();
                writer.Write(data, offset, size);
                writer.Flush();

                outputStream =
                    _datagramSocket.GetOutputStreamAsync(MulticastAddressV6, portString)
                        .AsTask()
                        .Result;
                writer = outputStream.AsStreamForWrite();
                writer.Write(data, offset, size);
                writer.Flush();
            }
            catch (Exception ex)
            {
                NetUtils.DebugWriteError("[S][MCAST]" + ex);
                return false;
            }
            return true;
        }

        public int SendTo(byte[] data, int offset, int length, NetEndPoint remoteEndPoint, ref int errorCode)
        {
            Task<uint> task = null;
            try
            {
                IOutputStream writer;
                if (!_peers.TryGetValue(remoteEndPoint, out writer))
                {
                    writer =
                        _datagramSocket.GetOutputStreamAsync(remoteEndPoint.HostName, remoteEndPoint.PortStr)
                            .AsTask()
                            .Result;
                    _peers.Add(remoteEndPoint, writer);
                }

                task = writer.WriteAsync(data.AsBuffer(offset, length)).AsTask();
                return (int)task.Result;
            }
            catch (Exception ex)
            {
                if (task?.Exception?.InnerExceptions != null)
                {
                    ex = task.Exception.InnerException;
                }
                var errorStatus = SocketError.GetStatus(ex.HResult);
                switch (errorStatus)
                {
                    case SocketErrorStatus.MessageTooLong:
                        errorCode = 10040;
                        break;
                    default:
                        errorCode = (int)errorStatus;
                        NetUtils.DebugWriteError("[S " + errorStatus + "(" + errorCode + ")]" + ex);
                        break;
                }
                
                return -1;
            }
        }

        internal void RemovePeer(NetEndPoint ep)
        {
            _peers.Remove(ep);
        }

        public void Close()
        {
            _datagramSocket.Dispose();
            _datagramSocket = null;
            ClearPeers();
        }

        internal void ClearPeers()
        {
            _peers.Clear();
        }
    }
}
#endif
