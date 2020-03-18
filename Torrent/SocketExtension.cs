using System;
using System.Net.Sockets;

namespace Torrent
{
    public static class SocketExtension
    {
        public static void ReceiveEnsure(this Socket socket, byte[] buffer, int size, SocketFlags socketFlags, string logscope = "")
        {
            int sum = 0, readlen = 0;
            while (true)
            {
                readlen = socket.Receive(buffer, size - sum, socketFlags);
                if (readlen == 0)
                {
                    throw new Exception(logscope + "断开连接了  socket.Connected:" + socket.Connected + "  socket.Available:" + socket.Available);
                }
                sum += readlen;
                if (sum == size)
                {
                    break;
                }
            }
        }
        public static void SendEnsure(this Socket socket, byte[] buffer, int size, SocketFlags socketFlags, string logscope = "")
        {
            int sum = 0, sendlen = 0;
            while (true)
            {
                sendlen = socket.Send(buffer, size - sum, socketFlags);
                if (sendlen == 0)
                {
                    throw new Exception(logscope + "断开连接了 socket.Connected:" + socket.Connected + "  socket.Available:" + socket.Available);
                }
                sum += sendlen;
                if (sum == size)
                {
                    break;
                }
            }
        }
    }
}
