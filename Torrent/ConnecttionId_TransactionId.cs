using System;
using System.Collections.Generic;
using System.Net;

namespace Torrent
{
    public class ConnecttionId_TransactionId
    {
        private ConnecttionId_TransactionId()
        {

        }
        /// <summary>
        /// 4字节
        /// </summary>
        public int Transaction_ID { get; private set; }
        /// <summary>
        /// 8字节
        /// </summary>
        public long Connecttion_ID { get; private set; } = 0x41727101980;


        public static ConnecttionId_TransactionId Create(int transaction_ID, long connecttion_ID)
        {
            return new ConnecttionId_TransactionId { Transaction_ID = transaction_ID, Connecttion_ID = connecttion_ID };
        }
        public static ConnecttionId_TransactionId Create(int transaction_ID)
        {
            return new ConnecttionId_TransactionId { Transaction_ID = transaction_ID };
        }

        public byte[] ToArray()
        {
            var buf = new List<byte>(16);
            buf.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(Connecttion_ID)));
            buf.AddRange(BitConverter.GetBytes(0));
            buf.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(Transaction_ID)));

            return buf.ToArray();
        }

        private static IEnumerator<ConnecttionId_TransactionId> _arr;

        public static ConnecttionId_TransactionId CreateNext()
        {
            if (_arr == null)
            {
                _arr = CreateArray().GetEnumerator();
            }
            if (_arr.MoveNext())
            {
                return _arr.Current;
            }
            else
            {
                _arr = CreateArray().GetEnumerator();
                _arr.MoveNext();
                return _arr.Current;
            }
        }

        static IEnumerable<ConnecttionId_TransactionId> CreateArray()
        {
            for (int j = 0; j < int.MaxValue; j++)
            {
                yield return new ConnecttionId_TransactionId() { Transaction_ID = j };
            }
        }

        public override bool Equals(object obj)
        {
            if (obj == null || obj.GetType() != typeof(ConnecttionId_TransactionId))
            {
                return false;
            }
            var other = obj as ConnecttionId_TransactionId;
            return other.Transaction_ID == this.Transaction_ID;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + Transaction_ID.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return $"Transaction_ID:{Transaction_ID},Connecttion_ID:{Connecttion_ID}";
        }
    }
}
