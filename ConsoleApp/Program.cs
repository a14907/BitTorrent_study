﻿using Bencoding;
using Bencoding.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Torrent;
using Tracker;

namespace ConsoleApp
{
    class Program
    {
        static async Task Main(string[] args)
        {

            var server = new UdpServer(Http.Port);
            server.Start();
            using (var fs = new FileStream("a.torrent", FileMode.Open))
            {
                var data = Parser.Decode(fs);
                TorrentModel torrentModel = new TorrentModel(data as DictionaryField);

                //var res = await torrentModel.TrackAsync();
                //var res = await torrentModel.ScrapeAsync();
                server.Connecting(torrentModel);

            }
            Console.WriteLine("OK");
            Console.ReadKey();
            server.Stop();
        }
    }
}
