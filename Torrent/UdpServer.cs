using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System.Threading;
using Tracker.Models;

namespace Torrent
{
    public class UdpServer
    {
        private readonly Logger _logger;
        private readonly int _port;
        private readonly Socket _socket;
        private Timer _timer;
        private bool _isStart = false;
        private Dictionary<ConnecttionId_TransactionId, TorrentModel> _dic = new Dictionary<ConnecttionId_TransactionId, TorrentModel>();
        private Dictionary<ReplayItem, int> _connectingLs = new Dictionary<ReplayItem, int>();

        public static int Port { get; set; } = 8099;
        public static bool IsOk { get; set; }

        public UdpServer(int port)
        {
            _logger = new Logger();
            _port = port;
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            //_timer = new Timer(RepeatConnectingUDP, null, 15000, Timeout.Infinite);
        }

        private void RepeatConnectingUDP(object state)
        {
            try
            {
                foreach (var item in _connectingLs)
                {
                    _socket.SendTo(item.Key.Ids.ToArray(), item.Key.EndPoint);
                    _logger.LogInformation("请求connecting:" + item.Key.Ids.Transaction_ID);
                }
                foreach (var item in _connectingLs.Keys.ToArray())
                {
                    _connectingLs[item] += 1;
                }

                _connectingLs = _connectingLs.Where(m => m.Value < 5).ToDictionary(m => m.Key, m => m.Value);
            }
            finally
            {
                _timer?.Dispose();
                _timer = new Timer(RepeatConnectingUDP, null, 15000, Timeout.Infinite);
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
                    try
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
                    catch (Exception ex)
                    {
                        _logger.LogInformation(ex.Message);
                    }
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
            using (var ms = new MemoryStream(buf, 0, receiveLen))
            using (var br = new BinaryReader(ms))
            {
                var action = (ActionsType)IPAddress.NetworkToHostOrder(br.ReadInt32());
                if (action == ActionsType.Connect)
                {
                    var transaction_id = IPAddress.NetworkToHostOrder(br.ReadInt32());
                    var connection_id = IPAddress.NetworkToHostOrder(br.ReadInt64());
                    _logger.LogInformation($"收到字节长度：{receiveLen}，i32:{transaction_id},i64:{connection_id}");

                    var ids = ConnecttionId_TransactionId.Create(transaction_id, connection_id);
                    if (_dic.ContainsKey(ids))
                    {
                        var model = _dic[ids];
                        //_logger.LogInformation(model.Info.Name ?? model.Info.Files.FirstOrDefault()?.Path.FirstOrDefault());

                        Announcing(model, connection_id, transaction_id, remoteEP);
                        //Scraping(model, connection_id, transaction_id, remoteEP);
                    }
                    var rk = new ReplayItem() { EndPoint = remoteEP, Ids = ids };
                    if (_connectingLs.ContainsKey(rk))
                    {
                        //_logger.LogInformation("已获取udp返回的值，从循环数据源删除相关数据");
                        _connectingLs.Remove(rk);
                    }
                    _connectingLs.Remove(new ReplayItem { Ids = ids, EndPoint = remoteEP });
                }
                else if (action == ActionsType.Announce)
                {
                    var transaction_id = IPAddress.NetworkToHostOrder(br.ReadInt32());
                    var interval = IPAddress.NetworkToHostOrder(br.ReadInt32());
                    var leechers = IPAddress.NetworkToHostOrder(br.ReadInt32());
                    var seeders = IPAddress.NetworkToHostOrder(br.ReadInt32());

                    var ls = new List<IPEndPoint>();
                    while (ms.Position != ms.Length)
                    {
                        var ip = br.ReadBytes(4);
                        var port = br.ReadUInt16();
                        var ipendpoint = new IPEndPoint(new IPAddress(ip), port);
                        ls.Add(ipendpoint);
                    }
                    _logger.LogInformation("收到响应 transaction_id:" + transaction_id + ",ip-" + ls.Aggregate("", (s, i) => s + i + ";"));
                    var ids = ConnecttionId_TransactionId.Create(transaction_id);
                    if (_dic.ContainsKey(ids))
                    {
                        var model = _dic[ids];
                        var tr = new TrackerResponse(remoteEP as IPEndPoint)
                        {
                            Peers = ls.ToArray(),
                            Interval = interval,
                            Complete = seeders,
                            Incomplete = leechers
                        };
                        model.Download(tr);
                        IsOk = true;
                        _dic.Remove(ids);

                        if (!model.IsFinish)
                        {
                            _ = Task.Delay(TimeSpan.FromSeconds(tr.Interval))
                                .ContinueWith(t =>
                                {
                                    _socket.SendTo(ids.ToArray(), remoteEP);
                                    _dic.Add(ids, model);
                                    _connectingLs.Add(new ReplayItem { Ids = ids, EndPoint = remoteEP }, 0);
                                });
                        }
                    }
                }
                else if (action == ActionsType.Scrape)
                {
                    var transaction_id = IPAddress.NetworkToHostOrder(br.ReadInt32());
                    var ids = ConnecttionId_TransactionId.Create(transaction_id);

                    var complete = IPAddress.NetworkToHostOrder(br.ReadInt32());
                    var downloaded = IPAddress.NetworkToHostOrder(br.ReadInt32());
                    var incomplete = IPAddress.NetworkToHostOrder(br.ReadInt32());

                    // todo do something for Scrape
                }
                else if (action == ActionsType.Error)
                {
                    br.ReadInt32();
                    var i1 = br.BaseStream.Length;
                    var i2 = br.BaseStream.Position;
                    bool ishavestr = i1 != i2;
                    if (ishavestr)
                    {
                        var strbuf = br.ReadBytes((int)(i1 - i2));
                        var errMsg = Encoding.UTF8.GetString(strbuf);
                        _logger.LogWarnning($"{remoteEP}：udp errmsg:" + errMsg);
                    }
                }
                else
                {
                    _logger.LogInformation("收到某些响应。。。");
                }
            }
        }

        private void Scraping(TorrentModel model, long connectionId, int transactionId, EndPoint remoteEP)
        {
            long connection_id = connectionId;
            int action = (int)ActionsType.Scrape;
            int transaction_id = transactionId;
            var info_hash = model.Info.Sha1Hash;

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(IPAddress.HostToNetworkOrder(connection_id));
                bw.Write(IPAddress.HostToNetworkOrder(action));
                bw.Write(IPAddress.HostToNetworkOrder(transaction_id));
                bw.Write(info_hash);

                _socket.SendTo(ms.ToArray(), remoteEP);
            }
        }

        public void Announcing(TorrentModel model, long connectionId, int transactionId, EndPoint remoteEP)
        {
            long connection_id = connectionId;
            int action = (int)ActionsType.Announce;
            int transaction_id = transactionId;
            var info_hash = model.Info.Sha1Hash;
            var peer_id = Http.PeerIdBytes;
            long downloaded = 0;
            long left = model.Info.Length;
            long uploaded = 0;
            int eventVal = (int)EventType.Started;
            int ip = 0;
            //  A unique key that is randomized by the client
            int key = new Random().Next(0, 999999);
            int num_want = -1;
            UInt16 port = (UInt16)Http.Port;
            UInt16 extensions = 0;

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(IPAddress.HostToNetworkOrder(connection_id));
                bw.Write(IPAddress.HostToNetworkOrder(action));
                bw.Write(IPAddress.HostToNetworkOrder(transaction_id));
                bw.Write(info_hash);
                bw.Write(peer_id);
                bw.Write(IPAddress.HostToNetworkOrder(downloaded));
                bw.Write(IPAddress.HostToNetworkOrder(left));
                bw.Write(IPAddress.HostToNetworkOrder(uploaded));
                bw.Write(IPAddress.HostToNetworkOrder(eventVal));
                bw.Write(IPAddress.HostToNetworkOrder(ip));
                bw.Write(IPAddress.HostToNetworkOrder(key));
                bw.Write(IPAddress.HostToNetworkOrder(num_want));
                bw.Write(IPAddress.HostToNetworkOrder(port));
                bw.Write(IPAddress.HostToNetworkOrder(extensions));

                _socket.SendTo(ms.ToArray(), remoteEP);
            }

        }

        public void Connecting(TorrentModel model)
        {
            var ls = new List<AnnounceItem>();
            ls.AddRange(model.Announce_list.SelectMany(m => m));
            if (!string.IsNullOrEmpty(model.Announce.Url))
            {
                ls.Add(model.Announce);
            }


            var us = ls.Where(m => m.Url.StartsWith("udp"));
            _logger.LogInformation("udp announce数量：" + us.Count());
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
                _logger.LogInformation($"对{item.Url}发送请求,Ids:{ids}");
                _socket.SendTo(ids.ToArray(), iPEndPoint);


                _dic.Add(ids, model);
                _connectingLs.Add(new ReplayItem { Ids = ids, EndPoint = iPEndPoint }, 0);
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
                        _logger.LogInformation("host地址无法进行dns解析");
                        return null;
                    }
                    return r;
                }
                else
                {
                    _logger.LogInformation("host地址未知");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.Message + ":" + u);
                return null;
            }
        }
    }
}
