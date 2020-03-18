using System.Collections.Generic;
using System.Net;

namespace Torrent
{
    public class IPEndPointCompare : IEqualityComparer<IPEndPoint>
    {
        public bool Equals(IPEndPoint x, IPEndPoint y)
        {
            return x.ToString() == y.ToString();
        }

        public int GetHashCode(IPEndPoint obj)
        {
            return obj.ToString().GetHashCode();
        }
    }
}
