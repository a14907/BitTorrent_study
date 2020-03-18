﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Torrent
{
    public class Tcp
    {
        public static int TotalPeer;
        public static int ErrNum;
        public Tcp()
        {
        }

        public void Download(TorrentModel torrentModel)
        {
            var localIp = IPAddress.Parse("127.0.0.1");
            var data = torrentModel.TrackerResponse.SelectMany(m => m.Peers).Where(m => !m.Address.Equals(localIp)).OrderBy(m => Guid.NewGuid()).ToList();
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
                try
                {
                    var peer = new Peer(lockObj);
                    peer.Process(item, torrentModel.Info, torrentModel);
                    torrentModel.Peers.Add(peer);
                    //while (peer.IsConnect)
                    //{
                    //    Thread.Sleep(1000);
                    //}
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
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

        private static byte[] _reserved = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 };
        private static byte[] _pstr = new byte[] { 66, 105, 116, 84, 111, 114, 114, 101, 110, 116, 32, 112, 114, 111, 116, 111, 99, 111, 108 };

        private bool _am_choking = true;// 本客户端正在choke远程peer。 
        private bool _am_interested = false;// 本客户端对远程peer感兴趣。 
        private bool _peer_choking = true;// 远程peer正choke本客户端。 
        private bool _peer_interested = false;// 远程peer对本客户端感兴趣。
        private List<int> _haveIndexArray = new List<int>();
        private object _lock;
        public Dictionary<int, bool> HaveState = new Dictionary<int, bool>();

        public bool IsConnect = false;


        public void Process(IPEndPoint ip, Info info, TorrentModel torrentModel)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //Console.WriteLine(ip + "， 开始连接");
            socket.BeginConnect(ip, ConnectCallback, null);
            //Console.WriteLine(ip + "， 等待连接回调");
            IsConnect = true;

            void ConnectCallback(IAsyncResult ar)
            {
                try
                {
                    socket.EndConnect(ar);
                    Console.WriteLine(ip + "， 连接成功");
                    //handshake:<pstrlen><pstr><reserved><info_hash><peer_id>
                    //"BitTorrent protocol"
                    byte[] handshakeByte = null;
                    using (var ms = new MemoryStream())
                    using (var bw = new BinaryWriter(ms))
                    {
                        bw.Write((byte)_pstr.Length);
                        bw.Write(_pstr);
                        bw.Write(_reserved);
                        bw.Write(info.Sha1Hash);
                        bw.Write(Http.PeerIdBytes);
                        handshakeByte = ms.ToArray();
                    }

                    socket.SendEnsure(handshakeByte, handshakeByte.Length, SocketFlags.None, $"{ip}, sendHandShake ");
                    Console.WriteLine(ip + ",发送handshake成功");

                    //发送Bitfield
                    SendBitfield();

                    //发送unchok
                    SendUnChoke();
                    //发送感兴趣
                    SendInterested();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ip + " , " + ex.Message);
                    IsConnect = false;
                    return;
                }

                Task.Factory.StartNew(obj =>
                {
                    try
                    {
                        var soc = obj as Socket;
                        int messageLen = 0;

                        //handshake:<pstrlen><pstr><reserved><info_hash><peer_id>
                        //接收handshake消息
                        ReceiveHandshake();

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
                                        break;
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
                                    Console.WriteLine(ip + ",处理：Piece");
                                    var buf = new byte[4];
                                    soc.ReceiveEnsure(buf, 4, SocketFlags.None, $"{ip}, Piece ");
                                    var index = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buf, 0));
                                    soc.ReceiveEnsure(buf, 4, SocketFlags.None, $"{ip}, Piece ");
                                    var begin = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buf, 0));

                                    buf = new byte[messageLen - 9];
                                    soc.ReceiveEnsure(buf, buf.Length, SocketFlags.None, $"{ip}, Piece ");
                                    lock (_lock)
                                    {
                                        var filename = torrentModel.Info.Sha1Hash.Aggregate("", (sum, item) => sum + item.ToString("x2"));
                                        using (var fs = new FileStream($"{filename}.db", FileMode.OpenOrCreate))
                                        {
                                            fs.Position = index * info.Piece_length + begin;
                                            fs.Write(buf, 0, buf.Length);
                                        }
                                    }
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

                                }

                                void Have()
                                {
                                    Console.WriteLine(ip + ",处理：Have");
                                    var buf = new byte[4];
                                    soc.ReceiveEnsure(buf, 4, SocketFlags.None, $"{ip}, Have ");
                                    var haveIndex = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buf, 0));
                                    _haveIndexArray.Add(haveIndex);
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
                                if (buf.Length != torrentModel.Bitfield.Length)
                                {
                                    Console.WriteLine($"{ip} Bitfield的长度错误");
                                    return;
                                }
                                int sum = 0;
                                for (int i = 0; i < buf.Length; i++)
                                {
                                    var item = buf[i];
                                    for (int j = 0; j < 8; j++)
                                    {
                                        sum++;
                                        if (sum > torrentModel.DownloadState.Count)
                                        {
                                            break;
                                        }
                                        HaveState[sum] = (item & (1 << (7 - i))) == 0 ? false : true;
                                    }
                                }
                            }
                        }

                        void ReceiveHandshake()
                        {
                            Console.WriteLine(ip + "开始接收handshake");
                            // pstrlen: < pstr > 的字符串长度，单个字节。 
                            var buf = new byte[1];
                            soc.ReceiveEnsure(buf, 1, SocketFlags.None, $"{ip}, ReceiveHandshake(pstrlen) ");
                            // pstr: 协议的标识符，字符串类型。 
                            buf = new byte[buf[0]];
                            soc.ReceiveEnsure(buf, buf.Length, SocketFlags.None, $"{ip}, ReceiveHandshake(pstr) ");
                            Console.WriteLine("协议的标识符:" + Encoding.UTF8.GetString(buf));
                            // reserved: 8个保留字节。当前的所有实现都使用全0.这些字节里面的每一个字节都可以用来改变协议的行为。来自Bram的邮件建议应该首先使用后面的位，以便可以使用前面的位来改变后面位的意义。 
                            buf = new byte[8];
                            soc.ReceiveEnsure(buf, 8, SocketFlags.None, $"{ip}, ReceiveHandshake(reserved-保留字节) ");
                            // info_hash: 元信息文件中info键(key)对应值的20字节SHA1哈希。这个info_hash和在tracker请求中info_hash是同一个。 
                            buf = new byte[20];
                            soc.ReceiveEnsure(buf, buf.Length, SocketFlags.None, $"{ip}, ReceiveHandshake(info_hash) ");
                            // 判断是否相同
                            var isequal = info.Sha1Hash.SequenceEqual(buf);
                            Console.WriteLine($"{ip}, Handshake接收到的info_hash和种子本身的：" + (isequal ? "相同" : "不相同"));
                            // peer_id: 用于唯一标识客户端的20字节字符串。这个peer_id通常跟在tracker请求中传送的peer_id相同(但也不尽然，例如在Azureus，就有一个匿名选项)。
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
                        if (Tcp.ErrNum == Tcp.TotalPeer)
                        {
                            Console.WriteLine("全部结束");
                        }
                    }

                }, socket, TaskCreationOptions.LongRunning);
            }

            void SendInterested()
            {
                var type = IPAddress.HostToNetworkOrder(1);
                byte id = 2;
                var data = BitConverter.GetBytes(type).ToList(); ;
                data.Add(id);
                socket.SendEnsure(data.ToArray(), data.Count, SocketFlags.None, $"{ip}, SendInterested ");
                _am_interested = true;
                Console.WriteLine($"{ip}, SendInterested 成功");
            }

            void SendBitfield()
            {
                //bitfield: <len=0001+X><id=5><bitfield>
                var buf = torrentModel.Bitfield;
                var len = 1 + buf.Length;

                socket.SendEnsure(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(len)), 4, SocketFlags.None, $"{ip} 发送Bitfield");
                socket.SendEnsure(new byte[] { 5 }, 1, SocketFlags.None, $"{ip} 发送Bitfield");
                socket.SendEnsure(buf, buf.Length, SocketFlags.None, $"{ip} 发送Bitfield");

                Console.WriteLine($"{ip} 发送Bitfield成功");
            }

            void SendRequest(int requestIndex)
            {
                Console.WriteLine(ip + ",发送：Request 请求序号：" + requestIndex);
                //request: < len = 0013 >< id = 6 >< index >< begin >< length > 
                //request报文长度固定，用于请求一个块(block)。payload包含如下信息： 
                //index: 整数，指定从零开始的piece索引。 
                //begin: 整数，指定piece中从零开始的字节偏移。 
                //length: 整数，指定请求的长度。
                var buf = new List<byte>(17);
                buf.AddRange(BitConverter.GetBytes(IPAddress.NetworkToHostOrder(13)));
                buf.Add(6);
                buf.AddRange(BitConverter.GetBytes(IPAddress.NetworkToHostOrder(requestIndex)));
                buf.AddRange(BitConverter.GetBytes(IPAddress.NetworkToHostOrder(0)));
                buf.AddRange(BitConverter.GetBytes(IPAddress.NetworkToHostOrder(info.Piece_length)));

                socket.SendEnsure(buf.ToArray(), buf.Count, SocketFlags.None, $"{ip}, 发送request, 序号：" + requestIndex);
            }
            void SendUnChoke()
            {
                //<len=0001><id=1>
                var type = IPAddress.HostToNetworkOrder(1);
                byte id = 1;
                var data = BitConverter.GetBytes(type).ToList(); ;
                data.Add(id);
                socket.SendEnsure(data.ToArray(), data.Count, SocketFlags.None, $"{ip}, SendUnChoke ");
                _am_choking = false;
                Console.WriteLine($"{ip}, SendUnChoke 成功");
            }
        }

    }
}
