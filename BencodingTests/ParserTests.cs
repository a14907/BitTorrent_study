using Microsoft.VisualStudio.TestTools.UnitTesting;
using Bencoding;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

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
            var val = Parser.ParseString(ms);
            Assert.AreEqual("wdqa", val.Value);
            Assert.AreEqual(ms.Position, temp.Length);
        }

        [TestMethod()]
        public void Test_ParseInt()
        {
            var temp = "i12345e";
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(temp));
            var val = Parser.ParseInt(ms);
            Assert.AreEqual(12345, val.Value);
            Assert.AreEqual(ms.Position, temp.Length);
        }

        [TestMethod()]
        public void Test_Parse()
        {
            using (var fs = new FileStream("a.torrent", FileMode.Open))
            {
                var val = Parser.Parse(fs);
                Assert.AreEqual(Model.BType.Dictionary, val.Type);
                Assert.AreEqual(fs.Position, fs.Length);
            }

        }
    }
}