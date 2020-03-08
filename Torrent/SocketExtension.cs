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
                Console.WriteLine("socket接受长度：" + readlen);
                if (readlen == 0)
                {
                    throw new Exception("断开连接了");
                }
                sum += readlen;
                Console.WriteLine($"需要接收长度：{size}，现在接收：{sum}");
                if (sum == size)
                {
                    break;
                }
            }
        }
        public static void SendEnsure(this Socket socket, byte[] buffer, int size, SocketFlags socketFlags)
        {
            int sum = 0, sendlen = 0;
            while (true)
            {
                sendlen = socket.Send(buffer, size - sum, socketFlags);
                Console.WriteLine("发送长度：" + sendlen);
                if (sendlen == 0)
                {
                    throw new Exception("断开连接了");
                }
                sum += sendlen;
                Console.WriteLine($"需要发送长度：{size}，现在发送：{sum}");
                if (sum == size)
                {
                    break;
                }
            }
        }
    }
}
