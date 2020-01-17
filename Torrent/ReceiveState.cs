using System.Net;

namespace Torrent
{
    public class ReceiveState
    {
        public byte[] Buffer { get; set; }
        public int ReceiveLen { get; set; }
        public EndPoint RemoteEndPoint { get; set; }
    }
}
