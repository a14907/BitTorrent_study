using Bencoding;
using Bencoding.Model;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Torrent;

namespace ConsoleApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            //var server = new UdpServer(Http.Port);
            var tcp = new Tcp();
            //server.Start();
            using (var fs = new FileStream("b.torrent", FileMode.Open))
            {
                var data = Parser.Decode(fs);
                TorrentModel torrentModel = new TorrentModel(data as DictionaryField);

                var res = await torrentModel.TrackAsync();

                _ = Task.Factory.StartNew(async () =>
                {
                    Console.WriteLine("开始下载任务");
                    for (int i = 0; i < torrentModel.DownloadState.Count; i++)
                    {
                        await Task.Delay(3000);
                        Console.WriteLine("===========判断序号是否存在：" + i);
                        var item = torrentModel.DownloadState[i];
                        if (!item)
                        {
                            if (torrentModel.Peers.Any(m => m.IsConnect && m.HaveState.ContainsKey(i) && m.HaveState[i]))
                            {
                                var p = torrentModel.Peers.First(m => m.IsConnect && m.HaveState[i]);
                                Console.WriteLine("======" + p.ip + "======请求" + i + "开始");
                                //torrentModel.DownloadSemaphore.Wait();
                                p.SendRequest(i);
                                Console.WriteLine("======" + p.ip + "======请求" + i + "完毕");
                            }
                        }
                    }

                }, TaskCreationOptions.LongRunning);
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
