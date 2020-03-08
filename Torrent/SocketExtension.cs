using System;
using System.Net.Sockets;

namespace Torrent
{
    public static class SocketExtension
    {
        public static void ReceiveEnsure(this Socket socket, byte[] buffer, int size, SocketFlags socketFlags)
        {
            int sum = 0, readlen = 0;
            while (true)
            {
                readlen = socket.Receive(buffer, size - sum, socketFlags);
                if (readlen == 0)
                {
                    throw new Exception("断开连接了");
                }
                sum += readlen;
                if (sum == size)
                {
                    break;
                }
            }
        }
        public static void SendEnsure(this Socket socket, byte[] buffer, int size, SocketFlags socketFlags)
        {
            int sum = 0, readlen = 0;
            while (true)
            {
                readlen = socket.Send(buffer, size - sum, socketFlags);
                if (readlen == 0)
                {
                    throw new Exception("断开连接了");
                }
                sum += readlen;
                if (sum == size)
                {
                    break;
                }
            }
        }
    }
}
