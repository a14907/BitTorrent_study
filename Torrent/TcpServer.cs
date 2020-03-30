using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Torrent
{
    public class Tcp
    {
        public static int TotalPeer;
        public static int ErrNum;
        public static SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(10, 10);
        public Tcp()
        {
        }

        public void Download(TorrentModel torrentModel)
        {
            var localIp = IPAddress.Parse("127.0.0.1");
            var data = torrentModel.TrackerResponse.SelectMany(m => m.Peers).Where(m => !m.Address.Equals(localIp)).Distinct(new IPEndPointCompare()).OrderBy(m => Guid.NewGuid()).ToList();

            ////测试，调试
            //data = new List<IPEndPoint> {
            //    new IPEndPoint(IPAddress.Parse("192.168.1.102"), 29512),
            //    //new IPEndPoint(IPAddress.Parse("192.168.1.102"), 18123)
            //};

            if (data.Count == 0)
            {
                Console.WriteLine("没有满足条件的track");
                return;
            }

            TotalPeer = data.Count;
            Console.WriteLine("ip总数：" + TotalPeer);
            var lockObj = new object();
            foreach (var item in data)
            {
                SemaphoreSlim.Wait();
                var peer = new Peer(lockObj);
                peer.Process(item, torrentModel.Info, torrentModel);
                torrentModel.Peers.Add(peer);
                if (peer.IsConnect)
                {
                    break;
                }
            }

            for (int i = 0; i < 10; i++)
            {
                SemaphoreSlim.Wait();
            }

            Console.WriteLine("全部尝试完毕");
        }
    }

    public class Peer
    {
        public Peer(object lockObj)
        {
            _lock = lockObj;
        }

        private static readonly byte[] _reserved = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 };
        /// <summary>
        /// BitTorrent protocol
        /// </summary>
        private static readonly byte[] _pstr = new byte[] { 66, 105, 116, 84, 111, 114, 114, 101, 110, 116, 32, 112, 114, 111, 116, 111, 99, 111, 108 };

        public bool _am_choking = true;// 本客户端正在choke远程peer。 
        public bool _am_interested = false;// 本客户端对远程peer感兴趣。 
        public bool _peer_choking = true;// 远程peer正choke本客户端。 
        public bool _peer_interested = false;// 远程peer对本客户端感兴趣。
        private readonly List<int> _haveIndexArray = new List<int>();
        private readonly object _lock;
        public Dictionary<int, bool> PeerHaveState = new Dictionary<int, bool>();
        private Socket _socket;
        public IPEndPoint ip;
        private Info _info;
        private TorrentModel TorrentModel;
        public bool IsConnect = false;


        public void Process(IPEndPoint pip, Info pinfo, TorrentModel torrentModelIns)
        {
            torrentModelIns.DownloadComplete += TorrentModelIns_DownloadComplete;
            try
            {
                ip = pip;
                _info = pinfo;
                TorrentModel = torrentModelIns;
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                //Console.WriteLine(ip + "， 开始连接");
                _socket.BeginConnect(ip, ConnectCallBack, null);
                //Console.WriteLine(ip + "， 等待连接回调");

                void ConnectCallBack(IAsyncResult ar)
                {
                    try
                    {
                        _socket.EndConnect(ar);
                        Console.WriteLine(ip + "， 连接成功");
                        IsConnect = true;
                        //handshake:<pstrlen><pstr><reserved><info_hash><peer_id>
                        //"BitTorrent protocol"
                        byte[] handshakeByte = null;
                        using (var ms = new MemoryStream())
                        using (var bw = new BinaryWriter(ms))
                        {
                            bw.Write((byte)_pstr.Length);
                            bw.Write(_pstr);
                            bw.Write(_reserved);
                            bw.Write(_info.Sha1Hash);
                            bw.Write(Http.PeerIdBytes);
                            handshakeByte = ms.ToArray();
                        }

                        _socket.SendEnsure(handshakeByte, handshakeByte.Length, SocketFlags.None, $"{ip}, sendHandShake ");
                        Console.WriteLine(ip + ",发送handshake成功");

                        //发送Bitfield
                        SendBitfield();
                        //发送unchok
                        SendUnChoke();

                        Task.Factory.StartNew(obj =>
                        {
                            try
                            {
                                var soc = obj as Socket;
                                int messageLen = 0;

                                //handshake:<pstrlen><pstr><reserved><info_hash><peer_id>
                                //接收handshake消息
                                ReceiveHandshake();

                                SendInterested();

                                while (true)
                                {
                                    var bufBegin = new byte[4];
                                    soc.ReceiveEnsure(bufBegin, 4, SocketFlags.None, $"{ip}, 获取消息的长度 ");
                                    messageLen = BitConverter.ToInt32(bufBegin, 0);
                                    messageLen = IPAddress.NetworkToHostOrder(messageLen);
                                    if (messageLen == 0)
                                    {
                                        KeepAlive();
                                    }
                                    else
                                    {
                                        var bufType = new byte[1];
                                        soc.ReceiveEnsure(bufType, 1, SocketFlags.None, $"{ip}, 获取消息类型 ");
                                        var id = bufType[0];
                                        switch (id)
                                        {
                                            case 0:
                                                Choke();
                                                break;
                                            case 1:
                                                UnChoke();
                                                break;
                                            case 2:
                                                Interested();
                                                break;
                                            case 3:
                                                NotInterested();
                                                break;
                                            case 4:
                                                Have();
                                                break;
                                            case 5:
                                                Bitfield();
                                                break;
                                            case 6:
                                                Request();
                                                break;
                                            case 7:
                                                Piece();
                                                break;
                                            case 8:
                                                Cancel();
                                                break;
                                            case 9:
                                                Port();
                                                break;
                                            default:
                                                HandleOtherType();
                                                break;
                                        }

                                        void HandleOtherType()
                                        {
                                            var extLen = messageLen - 1;
                                            var ignoreBuf = new byte[extLen];
                                            soc.ReceiveEnsure(ignoreBuf, extLen, SocketFlags.None, $"{ip} , 接收未知的扩展message");
                                            Console.WriteLine("处理未知的message类型，长度：" + extLen);
                                        }

                                        void Port()
                                        {
                                            var buf = new byte[2];
                                            soc.ReceiveEnsure(buf, 2, SocketFlags.None, $"{ip}, Port ");
                                            Console.WriteLine(ip + ",处理：port");
                                        }

                                        void Cancel()
                                        {
                                            var buf = new byte[4];
                                            soc.ReceiveEnsure(buf, 4, SocketFlags.None, $"{ip}, Cancel ");
                                            var index = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buf, 0));
                                            soc.ReceiveEnsure(buf, 4, SocketFlags.None, $"{ip}, Cancel ");
                                            var begin = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buf, 0));
                                            soc.ReceiveEnsure(buf, 4, SocketFlags.None, $"{ip}, Cancel ");
                                            var length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buf, 0));
                                            Console.WriteLine(ip + ",处理：Cancel");
                                        }

                                        void Piece()
                                        {
                                            //piece: <len=0009+X><id=7><index><begin><block>
                                            var buf = new byte[4];
                                            soc.ReceiveEnsure(buf, 4, SocketFlags.None, $"{ip}, Piece ");
                                            var index = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buf, 0));
                                            soc.ReceiveEnsure(buf, 4, SocketFlags.None, $"{ip}, Piece ");
                                            var begin = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buf, 0));

                                            Console.WriteLine(ip + ",处理：Piece-" + index + " 接收到的数据长度-" + (messageLen - 9));
                                            buf = new byte[messageLen - 9];
                                            if (buf.Length == 0)
                                            {
                                                return;
                                            }
                                            soc.ReceiveEnsure(buf, buf.Length, SocketFlags.None, $"{ip}, Piece ");

                                            TorrentModel.AddWriteToFile(buf, index, begin, this);
                                        }

                                        void Request()
                                        {
                                            Console.WriteLine(ip + ",处理：Request");
                                            var buf = new byte[4];
                                            soc.ReceiveEnsure(buf, 4, SocketFlags.None, $"{ip}, Request ");
                                            var index = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buf, 0));
                                            soc.ReceiveEnsure(buf, 4, SocketFlags.None, $"{ip}, Request ");
                                            var begin = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buf, 0));
                                            soc.ReceiveEnsure(buf, 4, SocketFlags.None, $"{ip}, Request ");
                                            var length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buf, 0));
                                        }

                                        void Bitfield()
                                        {
                                            //<len=0001+X><id=5><bitfield>
                                            Console.WriteLine(ip + ",处理：Bitfield");
                                            var len = messageLen - 1;
                                            var buf = new byte[len];
                                            soc.ReceiveEnsure(buf, len, SocketFlags.None, $"{ip}, Bitfield ");
                                            SetHaveState(buf);

                                            _ = Task.Factory.StartNew(async () =>
                                            {
                                                Console.WriteLine(ip + " :开始下载任务");

                                                while (_peer_choking)
                                                {
                                                    await Task.Delay(3000);
                                                }

                                                foreach (var i in GetIndex())
                                                {
                                                    if (!IsConnect)
                                                    {
                                                        break;
                                                    }
                                                    //await Task.Delay(200);
                                                    Console.WriteLine(ip + " :===========判断序号是否存在：" + i);
                                                    var item = TorrentModel.DownloadState[i];
                                                    Console.WriteLine(ip + " :===========当前peer存在序号：" + i);
                                                    if (!item.IsDownloded)
                                                    {
                                                        if (IsConnect && PeerHaveState.ContainsKey(i) && _am_interested)
                                                        {
                                                            var p = this;
                                                            Console.WriteLine(ip + " :======" + p.ip + "======请求" + i + "开始");
                                                            //torrentModel.DownloadSemaphore.Wait();
                                                            p.SendRequest(i);
                                                            item.Peer = this;
                                                            Console.WriteLine(ip + " :======" + p.ip + "======请求" + i + "完毕");
                                                        }
                                                    }
                                                }


                                            }, TaskCreationOptions.LongRunning);
                                        }

                                        void Have()
                                        {
                                            var buf = new byte[4];
                                            soc.ReceiveEnsure(buf, 4, SocketFlags.None, $"{ip}, Have ");
                                            var haveIndex = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buf, 0));
                                            _haveIndexArray.Add(haveIndex);
                                            Console.WriteLine(ip + ",处理：Have-" + haveIndex);
                                            //SendRequest(haveIndex);
                                        }

                                        void NotInterested()
                                        {
                                            Console.WriteLine(ip + ",处理：NotInterested");
                                            _peer_interested = false;
                                        }

                                        void Interested()
                                        {
                                            Console.WriteLine(ip + ",处理：Interested");
                                            _peer_interested = true;
                                        }

                                        void UnChoke()
                                        {
                                            Console.WriteLine(ip + ",处理：UnChoke");
                                            _peer_choking = false;

                                        }

                                        void Choke()
                                        {
                                            Console.WriteLine(ip + ",处理：Choke");
                                            _peer_choking = true;
                                        }
                                    }

                                    void KeepAlive()
                                    {
                                        Console.WriteLine(ip + ",处理：KeepAlive");
                                    }

                                    void SetHaveState(byte[] buf)
                                    {
                                        if (buf.Length != TorrentModel.Bitfield.Length)
                                        {
                                            Console.WriteLine($"{ip} Bitfield的长度错误");
                                            return;
                                        }
                                        int sum = 0;
                                        string str = "";
                                        for (int i = 0; i < buf.Length; i++)
                                        {
                                            var item = buf[i];
                                            for (int j = 0; j < 8; j++)
                                            {
                                                if (sum >= TorrentModel.DownloadState.Count)
                                                {
                                                    break;
                                                }
                                                PeerHaveState[sum] = (item & (1 << (7 - j))) == 0 ? false : true;
                                                str += PeerHaveState[sum] ? "1" : "0";
                                                sum++;
                                            }
                                        }
                                        Console.WriteLine($"{ip} Bitfield的结果 base64:" + Convert.ToBase64String(buf) + " str.length:" + str.Length + " str：" + str + " BitArray:" + new BitArray(buf).Cast<bool>().Select(m => m ? "1" : "0").Aggregate("", (s, item) => s + item));
                                    }
                                }

                                void ReceiveHandshake()
                                {
                                    Console.WriteLine(ip + "开始接收handshake");
                                    var buf = new byte[1];
                                    soc.ReceiveEnsure(buf, 1, SocketFlags.None, $"{ip}, ReceiveHandshake(pstrlen) ");
                                    buf = new byte[buf[0]];
                                    soc.ReceiveEnsure(buf, buf.Length, SocketFlags.None, $"{ip}, ReceiveHandshake(pstr) ");
                                    Console.WriteLine("协议的标识符:" + Encoding.UTF8.GetString(buf));
                                    buf = new byte[8];
                                    soc.ReceiveEnsure(buf, 8, SocketFlags.None, $"{ip}, ReceiveHandshake(reserved-保留字节) ");
                                    Console.WriteLine("reserved-保留字节：" + string.Join("-", buf));
                                    buf = new byte[20];
                                    soc.ReceiveEnsure(buf, buf.Length, SocketFlags.None, $"{ip}, ReceiveHandshake(info_hash) ");
                                    var isequal = _info.Sha1Hash.SequenceEqual(buf);
                                    Console.WriteLine($"{ip}, Handshake接收到的info_hash和种子本身的：" + (isequal ? "相同" : "不相同"));
                                    buf = new byte[20];
                                    soc.ReceiveEnsure(buf, buf.Length, SocketFlags.None, $"{ip}, ReceiveHandshake(peer_id) ");
                                    isequal = Http.PeerIdBytes.SequenceEqual(buf);
                                    Console.WriteLine($"{ip}, Handshake接收到的peer_id和客户端传送的：" + (isequal ? "相同" : "不相同"));
                                }
                            }
                            catch (Exception ex)
                            {
                                Tcp.ErrNum++;
                                IsConnect = false;
                                Console.WriteLine(ip + "，结束 " + ex.Message);
                                Tcp.SemaphoreSlim.Release();
                                TorrentModel.Peers.Remove(this);
                                TorrentModel.SetPeerNull(this);
                                if (Tcp.ErrNum == Tcp.TotalPeer)
                                {
                                    Console.WriteLine("全部结束");
                                }
                            }

                        }, _socket, TaskCreationOptions.LongRunning);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ip + " , " + ex.Message);
                        IsConnect = false;
                        Tcp.SemaphoreSlim.Release();
                        TorrentModel.Peers.Remove(this);
                        return;
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ip + " , " + ex.Message);
                IsConnect = false;
                Tcp.SemaphoreSlim.Release();
                TorrentModel.Peers.Remove(this);
                return;
            }

        }

        private IEnumerable<int> GetIndex()
        {
            var h = PeerHaveState.Where(m => m.Value).Select(m => m.Key).ToList();
            while (true)
            {
                foreach (var item in _haveIndexArray.Union(h))
                {
                    yield return item;
                }
            }
        }

        private void TorrentModelIns_DownloadComplete()
        {
            this._socket?.Disconnect(true);
            _socket.Dispose();
        }

        private void SendBitfield()
        {
            lock (_lock)
            {
                //bitfield: <len=0001+X><id=5><bitfield>
                var buf = TorrentModel.Bitfield;
                var len = 1 + buf.Length;

                _socket.SendEnsure(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(len)), 4, SocketFlags.None, $"{ip} 发送Bitfield");
                _socket.SendEnsure(new byte[] { 5 }, 1, SocketFlags.None, $"{ip} 发送Bitfield");
                _socket.SendEnsure(buf, buf.Length, SocketFlags.None, $"{ip} 发送Bitfield");

                Console.WriteLine($"{ip} 发送Bitfield成功");
            }
        }

        private void SendUnChoke()
        {
            lock (_lock)
            {
                //<len=0001><id=1>
                var type = IPAddress.HostToNetworkOrder(1);
                byte id = 1;
                var data = BitConverter.GetBytes(type).ToList(); ;
                data.Add(id);
                _socket.SendEnsure(data.ToArray(), data.Count, SocketFlags.None, $"{ip}, SendUnChoke ");
                _am_choking = false;
                Console.WriteLine($"{ip}, SendUnChoke 成功");
            }
        }

        private void SendInterested()
        {
            lock (_lock)
            {
                //<len=0001><id=2>
                var type = IPAddress.HostToNetworkOrder(1);
                byte id = 2;
                var data = BitConverter.GetBytes(type).ToList(); ;
                data.Add(id);
                _socket.SendEnsure(data.ToArray(), data.Count, SocketFlags.None, $"{ip}, SendInterested ");
                _am_interested = true;
                Console.WriteLine($"{ip}, SendInterested 成功");
            }
        }

        public void SendHave(int index)
        {
            lock (_lock)
            {
                //<len=0005><id=4><piece index>
                var type = IPAddress.HostToNetworkOrder(5);
                byte id = 4;
                var data = BitConverter.GetBytes(type).ToList(); ;
                data.Add(id);
                data.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(index)));
                _socket.SendEnsure(data.ToArray(), data.Count, SocketFlags.None, $"{ip}, SendHave ");
                _am_choking = false;
                Console.WriteLine($"{ip}, SendHave 成功");

            }
        }

        public void SendRequest(int requestIndex)
        {
            lock (_lock)
            {
                Console.WriteLine(ip + ",发送：Request 请求序号：" + requestIndex);
                //request: < len = 0013 >< id = 6 >< index >< begin >< length > 
                //request报文长度固定，用于请求一个块(block)。payload包含如下信息： 
                //index: 整数，指定从零开始的piece索引。 
                //begin: 整数，指定piece中从零开始的字节偏移。 
                //length: 整数，指定请求的长度。

                //16kb
                var oneceLen = 16384;
                int total = (int)_info.Piece_length;
                if (requestIndex == TorrentModel.DownloadState.Count - 1)
                {
                    if (_info.Files == null)
                    {
                        total = (int)(_info.Length - (_info.Piece_length * (TorrentModel.DownloadState.Count - 1)));
                    }
                    else
                    {
                        total = (int)(_info.Files.Sum(m => m.Length) - (_info.Piece_length * (TorrentModel.DownloadState.Count - 1)));
                    }
                }
                var time = (int)Math.Ceiling(total * 1.0 / oneceLen);
                int begin = 0;
                int length = 0;
                for (int i = 0; i < time; i++)
                {
                    begin = oneceLen * i;
                    length = (int)Math.Min(oneceLen, total - begin);

                    var buf = new List<byte>(17);
                    buf.AddRange(BitConverter.GetBytes(IPAddress.NetworkToHostOrder(13)));
                    buf.Add(6);
                    buf.AddRange(BitConverter.GetBytes(IPAddress.NetworkToHostOrder(requestIndex)));
                    buf.AddRange(BitConverter.GetBytes(IPAddress.NetworkToHostOrder(begin)));
                    buf.AddRange(BitConverter.GetBytes(IPAddress.NetworkToHostOrder(length)));

                    Console.WriteLine($"{ip}, 发送request, 序号：" + requestIndex + " begin:" + begin + " length:" + length);

                    _socket.SendEnsure(buf.ToArray(), buf.Count, SocketFlags.None, $"{ip}, 发送request, 序号：" + requestIndex);
                }
            }
        }

    }
}
