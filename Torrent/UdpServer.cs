using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System.Threading;

namespace Torrent
{
    public class UdpServer
    {
        private readonly int _port;
        private readonly Socket _socket;
        private Timer _timer;
        private bool _isStart = false;
        private Dictionary<ConnecttionId_TransactionId, TorrentModel> _dic = new Dictionary<ConnecttionId_TransactionId, TorrentModel>();
        private Dictionary<ReplayItem, int> _replayLs = new Dictionary<ReplayItem, int>();

        public UdpServer(int port)
        {
            _port = port;
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _timer = new Timer(RepeatCallUDP, null, 15000, Timeout.Infinite);
        }

        private void RepeatCallUDP(object state)
        {
            try
            {
                foreach (var item in _replayLs)
                {
                    _socket.SendTo(item.Key.Ids.ToArray(), item.Key.EndPoint);
                }
                foreach (var item in _replayLs.Keys.ToArray())
                {
                    _replayLs[item] += 1;
                }

                _replayLs = _replayLs.Where(m => m.Value < 5).ToDictionary(m => m.Key, m => m.Value);
            }
            finally
            {
                _timer?.Dispose();
                _timer = new Timer(RepeatCallUDP, null, 15000, Timeout.Infinite);
            }
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
            _timer?.Dispose();
        }

        private void HandleTrack(byte[] buf, int receiveLen, EndPoint remoteEP)
        {
            using (var ms = new MemoryStream(buf))
            using (var br = new BinaryReader(ms))
            {
                var res = IPAddress.NetworkToHostOrder(br.ReadInt32());
                if (res == 0)
                {
                    var i32 = IPAddress.NetworkToHostOrder(br.ReadInt32());
                    var i64 = IPAddress.NetworkToHostOrder(br.ReadInt64());
                    Console.WriteLine($"收到字节长度：{receiveLen}，i32:{i32},i64:{i64}");

                    var ids = ConnecttionId_TransactionId.Create(i32, i64);
                    if (_dic.ContainsKey(ids))
                    {
                        var model = _dic[ids];
                        Console.WriteLine(model.Info.Name ?? model.Info.Files.FirstOrDefault()?.Path.FirstOrDefault());

                        Announcing(model, i64, i32);
                    }
                    var rk = new ReplayItem() { EndPoint = remoteEP, Ids = ids };
                    if (_replayLs.ContainsKey(rk))
                    {
                        Console.WriteLine("已获取udp返回的值，从循环数据源删除相关数据");
                        _replayLs.Remove(rk);
                    }
                    _replayLs.Remove(new ReplayItem { Ids = ids, EndPoint = remoteEP });
                }
                else if (res == 3)
                {
                    br.ReadInt32();
                    var errMsg = br.ReadString();
                    Console.WriteLine("errmsg:" + errMsg);
                }
                else
                {
                    Console.WriteLine("收到某些响应。。。");
                }
            }
        }

        public void Announcing(TorrentModel model, long connectionId, int transactionId)
        {
            var connection_id = connectionId;
            var action = (int)UdpActions.Announce;
            var transaction_id = transactionId;
            var info_hash = model.Info.Sha1Hash;
            var peer_id = Http.PeerIdBytes;
            var downloaded = 0;
            var left = model.Info.Length;
        }

        public void Connecting(TorrentModel model)
        {
            var ls = new List<AnnounceItem>();
            ls.AddRange(model.Announce_list.SelectMany(m => m));
            ls.Add(model.Announce);

            var us = ls.Where(m => m.Url.StartsWith("udp"));
            Console.WriteLine("udp announce数量：" + us.Count());
            foreach (var item in us)
            {
                var u = new Uri(item.Url);
                var ip = GetIp(u);
                if (ip == null)
                {
                    continue;
                }
                IPEndPoint iPEndPoint = new IPEndPoint(ip, u.Port);
                var ids = ConnecttionId_TransactionId.CreateNext();
                //Console.WriteLine($"对{item.Url}发送请求,Ids:{ids}");
                _socket.SendTo(ids.ToArray(), iPEndPoint);


                _dic.Add(ids, model);
                _replayLs.Add(new ReplayItem { Ids = ids, EndPoint = iPEndPoint }, 0);
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

    public enum UdpActions
    {
        Connect = 0,
        Announce = 1,
        Scrape = 2,
        Error = 3,// (only in server replies)
    }
}
