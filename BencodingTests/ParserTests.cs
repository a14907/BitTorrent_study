using Microsoft.VisualStudio.TestTools.UnitTesting;
using Bencoding;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Bencoding.Model;
using System.Security.Cryptography;

namespace Bencoding.Tests
{
    [TestClass()]
    public class ParserTests
    {
        [TestMethod()]
        public void Test_Seek()
        {
            var ms = new MemoryStream();
            for (byte i = 0; i < 100; i++)
            {
                ms.WriteByte(i);
            }
            ms.Position = 20;
            ms.Seek(10, SeekOrigin.Begin);

            Assert.AreEqual(10, ms.Position);
        }

        [TestMethod()]
        public void Test_ParseString()
        {
            var temp = "4:wdqa";
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(temp));
            var val = Parser.DecodingString(ms);
            Assert.AreEqual("wdqa", val.Value);
            Assert.AreEqual(ms.Position, temp.Length);
        }

        [TestMethod()]
        public void Test_ParseInt()
        {
            var temp = "i12345e";
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(temp));
            var val = Parser.DecodingLong(ms);
            Assert.AreEqual(12345, val.Value);
            Assert.AreEqual(ms.Position, temp.Length);
        }

        [TestMethod()]
        public void Test_Parse_mulfile()
        {
            using (var fs = new FileStream("a.torrent", FileMode.Open))
            {
                var val = Parser.Decode(fs) as DictionaryField;
                Assert.AreEqual(Model.BType.Dictionary, val.Type);
                Assert.AreEqual(fs.Position, fs.Length);
            }

        }
        [TestMethod()]
        public void Test_Parse_singlefile()
        {
            //Encoding.UTF8.GetString(WebUtility.UrlEncodeToBytes(buf, 0, buf.Length));
            using (var fs = new FileStream("b.torrent", FileMode.Open))
            {
                var val = Parser.Decode(fs);
                Assert.AreEqual(Model.BType.Dictionary, val.Type);
                Assert.AreEqual(fs.Position, fs.Length);
            }

        }
    }
}