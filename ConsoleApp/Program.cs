using Bencoding;
using Bencoding.Model;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Threading;
using Torrent;

namespace ConsoleApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using (var fs = new FileStream("b.torrent", FileMode.Open))
            {
                var data = Parser.Decode(fs);
                TorrentModel torrentModel = new TorrentModel(data as DictionaryField);

                _ = torrentModel.TrackAsync();

                //var res2 = await torrentModel.ScrapeAsync();

                torrentModel.Connecting();

                torrentModel.WaitFinish();
            }
            Console.WriteLine("OK");
        }
    }
}
