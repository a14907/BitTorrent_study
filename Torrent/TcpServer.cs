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
    public class Tcp
    {
        private static byte[] _reserved = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 };
        private static byte[] _pstr = new byte[] { 66, 105, 116, 84, 111, 114, 114, 101, 110, 116, 32, 112, 114, 111, 116, 111, 99, 111, 108 };
        public Tcp()
        {
        }

        public void Connect(TorrentModel torrentModel)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            socket.Connect(torrentModel.TrackerResponse.First().IPEndPoint);

            //handshake:<pstrlen><pstr><reserved><info_hash><peer_id>
            //"BitTorrent protocol"
            byte[] handshakeByte = null;
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write((byte)_pstr.Length);
                bw.Write(_pstr);
                bw.Write(_reserved);
                bw.Write(torrentModel.Info.Sha1Hash);
                bw.Write(Http.PeerIdBytes);
                handshakeByte = ms.ToArray();
            }

            socket.Send(handshakeByte);

            Task.Factory.StartNew(obj =>
            {
                var soc = obj as Socket;
                var buf = new byte[1024 * 1024];
                int messageLen = 0;
                while (true)
                {
                    soc.ReceiveEnsure(buf, 4, SocketFlags.None);
                    messageLen = BitConverter.ToInt32(buf, 0);
                    messageLen = IPAddress.NetworkToHostOrder(messageLen);
                    if (messageLen == 0)
                    {
                        KeepAlive();
                    }
                    else
                    {
                        soc.ReceiveEnsure(buf, 1, SocketFlags.None);
                        var id = buf[0];
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
                    }
                }

            }, socket, TaskCreationOptions.LongRunning);

            void Port()
            {
                
            }

            void Cancel()
            {
                
            }

            void Piece()
            {
                
            }

            void Request()
            {
                
            }

            void Bitfield()
            {
                
            }

            void Have()
            {
                
            }

            void NotInterested()
            {
                
            }

            void Interested()
            {
                
            }

            void UnChoke()
            {
                
            }

            void Choke()
            {
                
            }

            void KeepAlive()
            {
                
            }
        }
    }
}
