using Bencoding.Helper;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace Bencoding.Model
{
    [DebuggerDisplay("Count = {Value.Count}")]
    public class DictionaryField : Base
    {
        private DictionaryField(Dictionary<string, Base> val) : base(BType.Dictionary)
        {
            Value = val;
        }
        public Base this[string index]
        {
            get
            {
                if (!Value.ContainsKey(index))
                {
                    return null;
                }
                return Value[index];
            }
        }
        public Dictionary<string, Base> Value { get; set; }
        public long OffsetStart { get; set; }
        public long OffsetEnd { get; set; }
        public byte[] Sha1Val { get; private set; }

        public void ComputeSha1(Stream stream)
        {
            var oldPos = stream.Position;
            var sha1 = SHA1.Create();
            stream.Position = OffsetStart;
            var buf = new byte[OffsetEnd - OffsetStart + 1];
            stream.EnsureRead(buf, 0, buf.Length);
            Sha1Val = sha1.ComputeHash(buf);

            sha1.Dispose();
            stream.Position = oldPos;

        }

        public static DictionaryField Create(Dictionary<string, Base> val)
        {
            return new DictionaryField(val);
        }
        public static DictionaryField Create()
        {
            return new DictionaryField(new Dictionary<string, Base>());
        }
    }
}
