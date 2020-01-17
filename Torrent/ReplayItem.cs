using System.Net;

namespace Torrent
{
    public class ReplayItem
    {
        public ConnecttionId_TransactionId Ids { get; set; }
        public EndPoint EndPoint { get; set; }

        public override bool Equals(object obj)
        {
            if (obj == null || obj.GetType() != typeof(ReplayItem))
            {
                return false;
            }
            var m = obj as ReplayItem;
            return Ids.Equals(m.Ids);
        }

        public override int GetHashCode()
        {
            return Ids.GetHashCode();
        }
    }
}
