using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Torrent
{
    public class ConnecttionId_TransactionId
    {
        public byte[] Transaction_ID { get; set; }
        public byte[] Connecttion_ID { get; set; }
    }
    public class UdpServer
    {
        private readonly int _port;
        private readonly Socket _socket;
        private bool _isStart = false;



        public UdpServer(int port)
        {
            _port = port;
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        }

        public void Start()
        {
            if (_isStart)
            {
                return;
            }
            _isStart = true;
            _socket.ReceiveBufferSize = 1024 * 8;
            _socket.Bind(new IPEndPoint(IPAddress.Any, _port));

            Task.Factory.StartNew(obj =>
            {
                var socket = obj as Socket;
                while (true)
                {
                    var buf = new byte[1024 * 8];
                    EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                    var receiveLen = socket.ReceiveFrom(buf, ref remoteEP);
                    Task.Factory.StartNew(p =>
                    {
                        var state = p as ReceiveState;
                        if (state.ReceiveLen == 16)
                        {
                            //Handle();
                        }

                    }, new ReceiveState { Buffer = buf, ReceiveLen = receiveLen, RemoteEndPoint = remoteEP });
                }
            }, _socket, TaskCreationOptions.LongRunning);
        }

        public void Track_Step1(TorrentModel model)
        {

        }

        public class ReceiveState
        {
            public byte[] Buffer { get; set; }
            public int ReceiveLen { get; set; }
            public EndPoint RemoteEndPoint { get; set; }
        }
    }
}
