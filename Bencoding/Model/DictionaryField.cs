using System.Collections.Generic;

namespace Bencoding.Model
{
    public class DictionaryField : Base
    {
        private DictionaryField(Dictionary<string, Base> val) : base(BType.Dictionary)
        {
            Value = val;
        }
        public Dictionary<string, Base> Value { get; set; }

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
