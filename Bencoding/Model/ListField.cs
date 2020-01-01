using System.Collections.Generic;
using System.Diagnostics;

namespace Bencoding.Model
{
    [DebuggerDisplay("Count = {Value.Count}")]
    public class ListField : Base
    {
        public ListField(List<Base> val) : base(BType.List)
        {
            Value = val;
        }
        public List<Base> Value { get; set; }


        public static ListField Create(List<Base> val)
        {
            return new ListField(val);
        }
        public static ListField Create()
        {
            return new ListField(new List<Base>());
        }

        public override string ToString()
        {
            if (Value.Count == 1)
            {
                return Value[0].ToString();
            }
            return base.ToString();
        }
    }
}
