using Bencoding;
using Bencoding.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Tracker;
using Tracker.Models;

namespace Torrent
{
    public static class Http
    {
        private static readonly HttpClient _httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(1) };
        public static readonly byte[] PeerIdBytes;
        public static readonly int Port = 8089;
        private static Logger _logger;

        static Http()
        {
            _logger = new Logger();
            var str = "-WQ0001-";
            var data = Encoding.ASCII.GetBytes(str).ToList();
            var r = new Random();
            for (int i = 0; i < 12; i++)
            {
                data.Add((byte)r.Next(0, 255));
            }
            PeerIdBytes = data.ToArray();
        }
        public static async Task TrackAsync(this TorrentModel model)
        {
            var info_hash = Encoding.UTF8.GetString(WebUtility.UrlEncodeToBytes(model.Info.Sha1Hash, 0, 20));
            var peer_id = Encoding.UTF8.GetString(WebUtility.UrlEncodeToBytes(PeerIdBytes, 0, 20));
            var port = Port;
            var uploaded = 0;
            var downloaded = 0;
            var left = model.Info.Length;
            var eventStr = Event.Started.ToString().ToLower();
            var compact = 1;

            var us = model.Announce_list.SelectMany(m => m).ToList();
            if (!string.IsNullOrEmpty(model.Announce.Url))
            {
                us.Add(model.Announce);
            }

            var httpUrls = us.Where(m => m.Url.StartsWith("http")).ToList();
            if (!httpUrls.Any())
            {
                throw new Exception("不存在http或者https的announce");
            }

            _logger.LogInformation("http track数量：" + httpUrls.Count);
            //udp tracker:https://blog.csdn.net/wenxinfly/article/details/1504785 
            foreach (var b in httpUrls)
            {
                await ProcessRequest();

                async Task ProcessRequest()
                {
                    try
                    {
                        string url = $"{b.Url}?info_hash={info_hash}&peer_id={peer_id}&port={port}&uploased={uploaded}&downloaded={downloaded}&left={left}&event={eventStr}&compact={compact}";
                        if (b.Url.Contains('?'))
                        {
                            url = $"{b.Url}&info_hash={info_hash}&peer_id={peer_id}&port={port}&uploased={uploaded}&downloaded={downloaded}&left={left}&event={eventStr}&compact={compact}";
                        }
                        var responseBuf = await _httpClient.GetByteArrayAsync(url);
                        var res = Parser.DecodingDictionary(new MemoryStream(responseBuf));
                        if (res.Value.Count == 1)
                        {
                            return;
                        }
                        var m = new TrackerResponse(res, b.Url);
                        if (m.Complete == 0)
                        {
                            return;
                        }
                        _logger.LogInformation("请求" + b.Url + "成功：" + m.Peers.Length);
                        model.Download(m);


                        if (model.IsFinish)
                        {
                            return;
                        }
                        else
                        {
                            _ = Task.Delay(TimeSpan.FromSeconds(m.Interval))
                                            .ContinueWith(t =>
                                            {
                                                _ = ProcessRequest();
                                            });
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogInformation("请求" + b.Url + "发生错误：" + e.Message);
                    }
                }
            }
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
                    _logger.LogInformation("对" + announceUrl + "进行scrape查询成功");
                    ls.Add(dic);
                }
                catch (Exception ex)
                {
                    _logger.LogInformation("对" + announceUrl + "进行scrape查询失败：" + ex.Message);
                }
            }
            return ls;
        }
    }
}
