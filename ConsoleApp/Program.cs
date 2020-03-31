using Bencoding;
using Bencoding.Model;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Torrent;

namespace ConsoleApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var server = new UdpServer(UdpServer.Port);
            var tcp = new Tcp();
            server.Start();
            using (var fs = new FileStream("b.torrent", FileMode.Open))
            {
                var data = Parser.Decode(fs);
                TorrentModel torrentModel = new TorrentModel(data as DictionaryField);

                var res = await torrentModel.TrackAsync();

                //var res2 = await torrentModel.ScrapeAsync();

                server.Connecting(torrentModel);
                while (!UdpServer.IsOk)
                {
                    await Task.Delay(1000);
                }
                Console.WriteLine("收到udp");
                tcp.Download(torrentModel);
                Console.ReadKey();
            }
            Console.WriteLine("OK");
            server.Stop();
        }
    }
}
