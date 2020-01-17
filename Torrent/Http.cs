using Bencoding;
using Bencoding.Model;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using Tracker.Models;
using System.Collections.Generic;
using Tracker;

namespace Torrent
{
    public static class Http
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly byte[] _peerIdBytes;
        private static readonly int _port = 8089;

        static Http()
        {
            _peerIdBytes = new byte[20];
            var r = new Random();
            for (int i = 0; i < 20; i++)
            {
                _peerIdBytes[i] = (byte)r.Next(0, 255);
            }
        }
        public static async Task<List<TrackerResponse>> TrackAsync(this TorrentModel model)
        {
            var info_hash = Encoding.UTF8.GetString(WebUtility.UrlEncodeToBytes(model.Info.Sha1Hash, 0, 20));
            var peer_id = Encoding.UTF8.GetString(WebUtility.UrlEncodeToBytes(_peerIdBytes, 0, 20));
            var port = _port;
            var uploaded = 0;
            var downloaded = 0;
            var left = model.Info.Length;
            var eventStr = Event.Started.ToString().ToLower();
            var compact = 1;

            var us = model.Announce_list.SelectMany(m => m).ToList();
            us.Add(model.Announce);
            var httpUrls = us.Where(m => m.Url.StartsWith("http")).ToList();
            if (!httpUrls.Any())
            {
                throw new Exception("不存在http或者https的announce");
            }

            //udp tracker:https://blog.csdn.net/wenxinfly/article/details/1504785

            var ls = new List<TrackerResponse>();
            foreach (var b in httpUrls)
            {
                try
                {
                    string url = $"{b}?info_hash={info_hash}&peer_id={peer_id}&port={port}&uploased={uploaded}&downloaded={downloaded}&left={left}&event={eventStr}&compact={compact}";
                    if (b.Url.Contains('?'))
                    {
                        url = $"{b}&info_hash={info_hash}&peer_id={peer_id}&port={port}&uploased={uploaded}&downloaded={downloaded}&left={left}&event={eventStr}&compact={compact}";
                    }
                    var responseBuf = await _httpClient.GetByteArrayAsync(url);
                    var res = Parser.DecodingDictionary(new MemoryStream(responseBuf));
                    var m = new TrackerResponse(res, b.Url);
                    ls.Add(m);
                }
                catch (Exception e)
                {
                    Console.WriteLine("请求" + b + "发生错误：" + e.Message);
                }
            }
            model.TrackerResponse = ls;
            return ls;
        }

        public static async Task<object> ScrapeAsync(this TorrentModel model)
        {
            var us = model.Announce_list.SelectMany(m => m).ToList();
            us.Add(model.Announce);
            var httpUrls = us.Where(m => m.Url.StartsWith("http")).ToList();
            if (!httpUrls.Any())
            {
                throw new Exception("不存在http或者https的announce");
            }

            var ls = new List<DictionaryField>();
            var info_hash = Encoding.UTF8.GetString(WebUtility.UrlEncodeToBytes(model.Info.Sha1Hash, 0, 20));
            foreach (var announceUrl in httpUrls)
            {
                try
                {
                    if (!announceUrl.Url.Contains("announce"))
                    {
                        throw new Exception("announceUrl不符合规范，无法进行scrape查询");
                    }
                    var baseUrl = announceUrl.Url.Replace("announce", "scrape");
                    var url = $"{baseUrl}?info_hash=" + info_hash;
                    var buf = await _httpClient.GetByteArrayAsync(url);
                    var dic = Parser.DecodingDictionary(new MemoryStream(buf));
                    ls.Add(dic);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("对" + announceUrl + "进行scrape查询失败：" + ex.Message);
                }
            }
            return ls;
        }
    }
}
