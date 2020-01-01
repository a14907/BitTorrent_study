using System;

namespace Bencoding.Model
{
    public class StringField : Base
    {
        private StringField(string val) : base(BType.String)
        {
            Value = val;
        }
        public string Value { get; }

        public static StringField Create(string val)
        {
            return new StringField(val);
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
