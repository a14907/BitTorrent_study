using Bencoding.Helper;
using Bencoding.Model;
using System;
using System.IO;
using System.Text;

namespace Bencoding
{
    public static class Parser
    {
        private const char _stringSpliter = ':';
        private const char _end = 'e';
        private const char _startInt = 'i';
        private const char _startLists = 'l';
        private const char _startDictionary = 'd';


        public static Base Parse(Stream stream)
        {
            var type = stream.ReadByte();
            stream.Position -= 1;
            switch ((char)type)
            {
                case _startInt:
                    return ParseInt(stream);
                case _startDictionary:
                    return ParseDictionary(stream);
                case _startLists:
                    return ParseList(stream);
                default:
                    return ParseString(stream);
            }
        }

        public static StringField ParseString(Stream stream)
        {
            int lengthNeedRead = 0;
            while (true)
            {
                var b = stream.ReadByte();
                if (b == _stringSpliter)
                {
                    break;
                }
                lengthNeedRead++;
            }
            stream.Position = stream.Position - lengthNeedRead - 1;
            byte[] lengthBuf = new byte[lengthNeedRead];
            stream.EnsureRead(lengthBuf, 0, lengthNeedRead);

            var lengthStr = Encoding.UTF8.GetString(lengthBuf);
            if (!int.TryParse(lengthStr, out int length))
            {
                throw new Exception("字符串的长度格式不对");
            }
            stream.ReadByte();

            var buf = new byte[length];
            stream.EnsureRead(buf, 0, length);
            return StringField.Create(Encoding.UTF8.GetString(buf));
        }

        public static NumberField ParseInt(Stream stream)
        {
            var begin = stream.ReadByte();
            if (begin != _startInt)
            {
                throw new Exception("整数开始字节错误");
            }
            int lengthNeedRead = 0;
            while (true)
            {
                var b = stream.ReadByte();
                if (b == _end)
                {
                    break;
                }
                lengthNeedRead++;
            }
            stream.Position = stream.Position - lengthNeedRead - 1;
            byte[] intBuf = new byte[lengthNeedRead];
            stream.EnsureRead(intBuf, 0, lengthNeedRead);
            stream.ReadByte();
            var longStr = Encoding.UTF8.GetString(intBuf);
            if (!long.TryParse(longStr, out long intVal))
            {
                throw new Exception("整数内容的格式错误");
            }
            return NumberField.Create(intVal);
        }

        public static ListField ParseList(Stream stream)
        {
            var begin = stream.ReadByte();
            if (begin != _startLists)
            {
                throw new Exception("list开始字节错误");
            }
            var result = ListField.Create();
            while (true)
            {
                var t = stream.ReadByte();
                if (t == _end)
                {
                    return result;
                }
                else
                {
                    stream.Position -= 1;
                }
                result.Value.Add(Parse(stream));
            }
        }
        public static DictionaryField ParseDictionary(Stream stream)
        {
            var begin = stream.ReadByte();
            if (begin != _startDictionary)
            {
                throw new Exception("dictionary开始字节错误");
            }
            var result = DictionaryField.Create();
            while (true)
            {
                var t = stream.ReadByte();
                if (t == _end)
                {
                    return result;
                }
                else
                {
                    stream.Position -= 1;
                }
                var key = Parse(stream);
                if (key.Type != BType.String)
                {
                    throw new Exception("dictionary的键类型错误");
                }
                var val = Parse(stream);
                result.Value.Add((key as StringField).Value, val);
            }
        }
    }
}
