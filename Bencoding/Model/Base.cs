namespace Bencoding.Model
{
    public abstract class Base
    {
        public Base(BType bType)
        {
            Type = bType;
        }
        public BType Type { get; }
    }
}
