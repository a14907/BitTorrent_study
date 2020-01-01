using System;

namespace Bencoding.Model
{
    public class NumberField : Base
    {
        private NumberField(long intVal) : base(BType.Int)
        {
            Value = intVal;
        }
        public long Value { get; set; }

        public static NumberField Create(long intVal)
        {
            return new NumberField(intVal);
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
