using Bencoding.Helper;
using Bencoding.Model;
using System;
using System.Collections.Generic;
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


        public static Base Decode(Stream stream)
        {
            var type = stream.ReadByte();
            stream.Position -= 1;
            switch ((char)type)
            {
                case _startInt:
                    return DecodingLong(stream);
                case _startDictionary:
                    return DecodingDictionary(stream);
                case _startLists:
                    return DecodingList(stream);
                default:
                    return DecodingString(stream);
            }
        }
        public static StringField DecodingString(Stream stream)
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
            var val = Encoding.UTF8.GetString(buf);
            return StringField.Create(val, length, buf);
        }
        public static NumberField DecodingLong(Stream stream)
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
        public static ListField DecodingList(Stream stream)
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
                result.Value.Add(Decode(stream));
            }
        }
        public static DictionaryField DecodingDictionary(Stream stream)
        {
            var begin = stream.ReadByte();
            if (begin != _startDictionary)
            {
                throw new Exception("dictionary开始字节错误");
            }
            var offsetStart = stream.Position - 1;
            var result = DictionaryField.Create();
            while (true)
            {
                var t = stream.ReadByte();
                if (t == _end)
                {
                    result.OffsetStart = offsetStart;
                    result.OffsetEnd = stream.Position - 1;
                    result.ComputeSha1(stream);
                    return result;
                }
                else
                {
                    stream.Position -= 1;
                }
                var key = Decode(stream);
                if (key.Type != BType.String)
                {
                    throw new Exception("dictionary的键类型错误");
                }
                var val = Decode(stream);
                result.Value.Add((key as StringField).Value, val);
            }
        }

        public static void Encode(Stream stream, object obj)
        {
            var type = obj.GetType();
            if (type == typeof(long))
            {
                EncodeLong(stream, (long)obj);
            }
            else if (type == typeof(Dictionary<string, object>))
            {
                EncodeDictionary(stream, (Dictionary<string, object>)obj);
            }
            else if (type == typeof(List<object>))
            {
                EncodeList(stream, (List<object>)obj);
            }
            else if (type == typeof(string))
            {
                EncodeString(stream, (string)obj);
            }
            else
            {
                throw new Exception("不支持的序列化格式");
            }
        }
        public static void EncodeString(Stream stream, string val)
        {
            var valbuf = Encoding.UTF8.GetBytes(val);

            var len = valbuf.Length.ToString();
            var lenbuf = Encoding.UTF8.GetBytes(len);
            stream.Write(lenbuf, 0, lenbuf.Length);

            stream.WriteByte((byte)_stringSpliter);

            stream.Write(valbuf, 0, valbuf.Length);
        }
        public static void EncodeLong(Stream stream, long val)
        {
            stream.WriteByte((byte)_startInt);

            var lenbuf = Encoding.UTF8.GetBytes(val.ToString());
            stream.Write(lenbuf, 0, lenbuf.Length);

            stream.WriteByte((byte)_end);
        }
        public static void EncodeList(Stream stream, List<object> val)
        {
            stream.WriteByte((byte)_startLists);
            foreach (var item in val)
            {
                Encode(stream, item);
            }
            stream.WriteByte((byte)_end);
        }
        public static void EncodeDictionary(Stream stream, Dictionary<string, object> val)
        {
            stream.WriteByte((byte)_startDictionary);
            foreach (var item in val)
            {
                Encode(stream, item.Key);
                Encode(stream, item.Value);
            }
            stream.WriteByte((byte)_end);
        }
    }
}
