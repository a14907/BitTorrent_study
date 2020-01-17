using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Linq;
using System.IO;

namespace Torrent
{
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
            _isStart = true;
            _socket.ReceiveBufferSize = 1024 * 8;
            _socket.Bind(new IPEndPoint(IPAddress.Any, _port));

            Task.Factory.StartNew(obj =>
            {
                var socket = obj as Socket;
                while (_isStart)
                {
                    var buf = new byte[1024 * 8];
                    EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                    var receiveLen = socket.ReceiveFrom(buf, ref remoteEP);
                    Task.Factory.StartNew(p =>
                    {
                        var state = p as ReceiveState;
                        HandleTrack(buf, receiveLen, remoteEP);

                    }, new ReceiveState { Buffer = buf, ReceiveLen = receiveLen, RemoteEndPoint = remoteEP });
                }
            }, _socket, TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            _isStart = false;
            _socket?.Dispose();
        }

        private void HandleTrack(byte[] buf, int receiveLen, EndPoint remoteEP)
        {
            using (var ms = new MemoryStream(buf))
            using (var br = new BinaryReader(ms))
            {
                var res = br.ReadInt32();
                if (res == 0)
                {
                    var i32 = br.ReadInt32();
                    var i64 = br.ReadInt64();
                    var ids = ConnecttionId_TransactionId.Create(i32, i64);
                    if (_dic.ContainsKey(ids))
                    {
                        var model = _dic[ids];
                        Console.WriteLine(model.Info.Name);
                    }
                }
                else if (res == 3)
                {
                    br.ReadInt32();
                    var errMsg = br.ReadString();
                    Console.WriteLine("errmsg:" + errMsg);
                }
            }
        }

        private Dictionary<ConnecttionId_TransactionId, TorrentModel> _dic = new Dictionary<ConnecttionId_TransactionId, TorrentModel>();

        public void Track(TorrentModel model)
        {
            var ls = new List<AnnounceItem>();
            ls.AddRange(model.Announce_list.SelectMany(m => m));
            ls.Add(model.Announce);

            foreach (var item in ls.Where(m => m.Url.StartsWith("udp")))
            {
                var u = new Uri(item.Url);
                var ip = GetIp(u);
                if (ip == null)
                {
                    continue;
                }
                IPEndPoint iPEndPoint = new IPEndPoint(ip, u.Port);
                var ids = ConnecttionId_TransactionId.CreateNext();
                Console.WriteLine($"对{item.Url}发送请求");
                _socket.SendTo(ids.ToArray(), iPEndPoint);
                _dic.Add(ids, model);
            }

        }

        private IPAddress GetIp(Uri u)
        {
            try
            {
                if (u.HostNameType == UriHostNameType.IPv4 || u.HostNameType == UriHostNameType.IPv6)
                {
                    return IPAddress.Parse(u.Host);
                }
                else if (u.HostNameType == UriHostNameType.Dns)
                {
                    var rs = Dns.GetHostAddresses(u.Host);
                    var r = rs.FirstOrDefault(m => m.AddressFamily == AddressFamily.InterNetwork);
                    if (r == null)
                    {
                        Console.WriteLine("host地址无法进行dns解析");
                        return null;
                    }
                    return r;
                }
                else
                {
                    Console.WriteLine("host地址未知");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }
    }

    public class ReceiveState
    {
        public byte[] Buffer { get; set; }
        public int ReceiveLen { get; set; }
        public EndPoint RemoteEndPoint { get; set; }
    }
}
