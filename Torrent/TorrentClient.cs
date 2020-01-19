using Bencoding;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Torrent
{
    public class TorrentClient
    {
        private UdpServer _server;

        public TorrentClient()
        {
            _server = new UdpServer(Http.Port);
        }

        public void Start()
        {
            _server.Start();
        }
        public void Stop()
        {
            _server.Stop();
        }

        public async Task AddTorrent(string fileName)
        {
            using (var fs = new FileStream(fileName, FileMode.Open))
            {
                var data = Parser.DecodingDictionary(fs);
                TorrentModel torrentModel = new TorrentModel(data);

                var res = await torrentModel.TrackAsync();
                _server.Connecting(torrentModel);

            }
        }
    }

    public class DataItem
    {
        public DataItem(TorrentModel torrentModel)
        {
            TorrentModel = torrentModel;
        }
        public TorrentModel TorrentModel { get; set; }
        public DateTime AddTime { get; set; } = DateTime.Now;
    }
}
