using System;
using System.IO;

namespace Bencoding.Helper
{
    public static class StreamExtension
    {
        public static void EnsureRead(this Stream stream, byte[] buf, int offset, int length)
        {
            int sum = 0;
            while (true)
            {
                var readLen = stream.Read(buf, offset + sum, length - sum);
                sum += readLen;
                if (sum == length)
                {
                    break;
                }
                else if (readLen == 0)
                {
                    throw new Exception("流已经读到末尾");
                }
            }

        }
    }
}
