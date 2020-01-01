using System;
using System.Linq;

namespace Bencoding.Model
{
    public class StringField : Base
    {
        private StringField(string val, int length, byte[] buf) : base(BType.String)
        {
            Value = val;
            Length = length;
            Buffer = buf;
        }
        public string Value { get; }
        public long Length { get; }
        public byte[] Buffer { get; }

        public static StringField Create(string val, int length, byte[] buf)
        {
            return new StringField(val, length, buf);
        }

        public override string ToString()
        {
            if (Value?.Length > 100)
            {
                return base.ToString();
            }
            return Value;
        }
    }
}
