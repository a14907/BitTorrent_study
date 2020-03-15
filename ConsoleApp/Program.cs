﻿using Bencoding;
using Bencoding.Model;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Linq;
using Torrent;

namespace ConsoleApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var aaa = typeof(BitConverter).Assembly.Location;
            //var server = new UdpServer(Http.Port);
            var tcp = new Tcp();
            //server.Start();
            using (var fs = new FileStream("b.torrent", FileMode.Open))
            {
                var data = Parser.Decode(fs);
                TorrentModel torrentModel = new TorrentModel(data as DictionaryField);

                var res = await torrentModel.TrackAsync();
                //var res2 = await torrentModel.ScrapeAsync();
                //server.Connecting(torrentModel);
                tcp.Download(torrentModel);
            }
            Console.WriteLine("OK");
            Console.ReadKey();
            //server.Stop();
        }
    }
}
