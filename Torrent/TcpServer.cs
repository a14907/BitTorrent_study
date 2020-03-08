﻿using System;
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
    public class Tcp
    {
        public Tcp()
        {
        }

        public void Download(TorrentModel torrentModel)
        {
            var peer = new Peer(new object());
            peer.Process(torrentModel.TrackerResponse.First(), torrentModel.Info);
        }
    }

    internal class Peer
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


        public void Process(TrackerResponse trackerResponse, Info info)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(trackerResponse.IPEndPoint);

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

            socket.Send(handshakeByte);

            //发送感兴趣
            SendInterested();
            //request请求
            //SendRequest();

            Task.Factory.StartNew(obj =>
            {
                var soc = obj as Socket;
                int messageLen = 0;
                while (true)
                {
                    var bufBegin = new byte[4];
                    soc.ReceiveEnsure(bufBegin, 4, SocketFlags.None);
                    messageLen = BitConverter.ToInt32(bufBegin, 0);
                    messageLen = IPAddress.NetworkToHostOrder(messageLen);
                    if (messageLen == 0)
                    {
                        KeepAlive();
                    }
                    else
                    {
                        var bufType = new byte[1];
                        soc.ReceiveEnsure(bufType, 1, SocketFlags.None);
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
                            soc.ReceiveEnsure(buf, 2, SocketFlags.None);
                            Console.WriteLine("处理：port");
                        }

                        void Cancel()
                        {
                            var buf = new byte[4];
                            soc.ReceiveEnsure(buf, 4, SocketFlags.None);
                            var index = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buf, 0));
                            soc.ReceiveEnsure(buf, 4, SocketFlags.None);
                            var begin = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buf, 0));
                            soc.ReceiveEnsure(buf, 4, SocketFlags.None);
                            var length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buf, 0));
                            Console.WriteLine("处理：Cancel");
                        }

                        void Piece()
                        {
                            Console.WriteLine("处理：Piece");
                            var buf = new byte[messageLen - 9];
                            soc.ReceiveEnsure(buf, 4, SocketFlags.None);
                            var index = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buf, 0));
                            soc.ReceiveEnsure(buf, 4, SocketFlags.None);
                            var begin = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buf, 0));
                            soc.ReceiveEnsure(buf, messageLen - 9, SocketFlags.None);
                            lock (_lock)
                            {
                                using (var fs = new FileStream("a.db", FileMode.OpenOrCreate))
                                {
                                    fs.Position = index * info.Piece_length + begin;
                                    fs.Write(buf, 0, buf.Length);
                                }
                            }
                        }

                        void Request()
                        {
                            Console.WriteLine("处理：Request");
                            var buf = new byte[4];
                            soc.ReceiveEnsure(buf, 4, SocketFlags.None);
                            var index = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buf, 0));
                            soc.ReceiveEnsure(buf, 4, SocketFlags.None);
                            var begin = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buf, 0));
                            soc.ReceiveEnsure(buf, 4, SocketFlags.None);
                            var length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buf, 0));
                        }

                        void Bitfield()
                        {
                            Console.WriteLine("处理：Bitfield");
                            var len = messageLen - 1;
                            var buf = new byte[len];
                            soc.ReceiveEnsure(buf, len, SocketFlags.None);
                        }

                        void Have()
                        {
                            Console.WriteLine("处理：Have");
                            var buf = new byte[4];
                            soc.ReceiveEnsure(buf, 4, SocketFlags.None);
                            var haveIndex = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buf, 0));
                            _haveIndexArray.Add(haveIndex);
                        }

                        void NotInterested()
                        {
                            Console.WriteLine("处理：NotInterested");
                            _peer_interested = false;
                        }

                        void Interested()
                        {
                            Console.WriteLine("处理：Interested");
                            _peer_interested = true;
                        }

                        void UnChoke()
                        {
                            Console.WriteLine("处理：UnChoke");
                            _peer_choking = false;
                            SendRequest();
                        }

                        void Choke()
                        {
                            Console.WriteLine("处理：Choke");
                            _peer_choking = true;
                        }
                    }

                    void KeepAlive()
                    {
                        Console.WriteLine("处理：KeepAlive");
                    }
                }

            }, socket, TaskCreationOptions.LongRunning);


            void SendInterested()
            {
                var type = IPAddress.HostToNetworkOrder(1);
                byte id = 2;
                var data = BitConverter.GetBytes(type).ToList(); ;
                data.Add(id);
                socket.SendEnsure(data.ToArray(), data.Count, SocketFlags.None);
                _am_interested = true;
            }

            void SendRequest()
            {
                Console.WriteLine("发生：request");
                //请求第一个块
                int index = IPAddress.HostToNetworkOrder(0);
                int begin = IPAddress.HostToNetworkOrder(0);
                int length = IPAddress.HostToNetworkOrder((int)info.Piece_length);
                //request: <len=0013><id=6><index><begin><length>
                var arr = new List<byte>(13);
                arr.AddRange(BitConverter.GetBytes(13));
                arr.Add(6);
                arr.AddRange(BitConverter.GetBytes(index));
                arr.AddRange(BitConverter.GetBytes(begin));
                arr.AddRange(BitConverter.GetBytes(length));
                socket.SendEnsure(arr.ToArray(), 14, SocketFlags.None);
            }
        }

    }
}
