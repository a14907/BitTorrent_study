using Bencoding;
using Bencoding.Model;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Threading;
using Torrent;
using System.Collections.Generic;
using System.Net;

namespace ConsoleApp
{
    class Program
    {
        static Logger _logger = new Logger(Logger.LogLevel.Warnning);
        static async Task Main(string[] args)
        {

            using (var fs = new FileStream("b.torrent", FileMode.Open))
            {
                var data = Parser.Decode(fs);
                TorrentModel torrentModel = new TorrentModel(data as DictionaryField);
                _logger.LogWarnning("下载：" + torrentModel.Info.Name);

                _ = torrentModel.TrackAsync();

                //var res2 = await torrentModel.ScrapeAsync();

                torrentModel.Connecting();

                //调试
                //torrentModel.Download(new Tracker.Models.TrackerResponse(IPEndPoint.Parse("192.168.1.102:29512"))
                //{
                //    Peers = new IPEndPoint[] { IPEndPoint.Parse("192.168.1.239:39114") }
                //});

                torrentModel.WaitFinish();
            }
            _logger.LogWarnning("OK");
        }
    }
}
