using System;
using System.Threading;

namespace Torrent
{
    public class FakeSemaphoreSlim
    {
        public FakeSemaphoreSlim(int initialCount, int maxCount)
        {
        }

        public void Wait()
        {
        }

        public void Release()
        {
        }
    }
}
