using Bencoding;
using Bencoding.Model;
using System;
using System.IO;
using System.Threading.Tasks;
using Tracker;

namespace ConsoleApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using (var fs = new FileStream("a.torrent", FileMode.Open))
            {
                var data = Parser.Decode(fs);
                TorrentModel torrentModel = new TorrentModel(data as DictionaryField);

                //var res = await torrentModel.TrackAsync();
                var res = await torrentModel.ScrapeAsync();
            }

        }
    }
}
