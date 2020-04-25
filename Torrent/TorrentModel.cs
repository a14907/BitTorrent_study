using Bencoding.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Tracker.Models;

namespace Torrent
{
    public delegate void DownloadComplete();
    public partial class TorrentModel
    {
        private readonly Logger _logger;
        private readonly DictionaryField _dictionaryField;
        private readonly FileStreamPool _fileStreamPool;
        private readonly string _baseDir = "Download";

        public void Download(TrackerResponse trackerResponse)
        {
            trackerResponse.Peers = trackerResponse.Peers.Except(Peers.ToArray().Where(m => m != null).Select(m => m.ip)).ToArray();
            _ = Task.Factory.StartNew(() =>
            {
                new Tcp().Download(this, trackerResponse);
            }, TaskCreationOptions.LongRunning);
        }

        private readonly Guid _guid = Guid.NewGuid();
        public Dictionary<int, DownloadState> DownloadState;
        public List<Peer> Peers = new List<Peer>();
        public SemaphoreSlim ConsumerHold = new SemaphoreSlim(1024, 1024);
        public bool IsFinish { get; set; }
        private readonly object _lock = new object();
        private readonly BlockingCollection<(byte[] buf, int index, int begin, Peer peer)> _writeToFileDB = new BlockingCollection<(byte[], int, int, Peer)>();

        public event DownloadComplete DownloadComplete;
        private UdpServer _udpServer = new UdpServer(UdpServer.Port);

        public void SetPeerNull(Peer peer)
        {
            foreach (var item in DownloadState)
            {
                if (item.Value.Peer == peer)
                {
                    item.Value.Peer = null;
                }
            }
        }

        //public SemaphoreSlim DownloadSemaphore = new SemaphoreSlim(1, 1);

        public byte[] Bitfield
        {
            get
            {
                var buf = new byte[(int)Math.Ceiling(Info.PiecesHashArray.Count / (1.0 * 8))];
                for (int i = 0; i < Info.PiecesHashArray.Count; i++)
                {
                    var index = i / 8;
                    var bit = 7 - (i - index * 8);
                    var val = DownloadState[i].IsDownloded ? 1 : 0;
                    buf[index] = (byte)(buf[index] | (val << bit));
                }
                return buf;
            }
        }

        public TorrentModel(DictionaryField dictionaryField)
        {
            _fileStreamPool = new FileStreamPool(_lock);
            _logger = new Logger();
            _dictionaryField = dictionaryField;
            Info = new Info(dictionaryField["info"] as DictionaryField);
            _downloadStateFile = _baseDir + "/" + Info.Name + ".downloadState";

            if (File.Exists(_downloadStateFile))
            {
                var s = LoadDownloadProcess();
                DownloadState = new Dictionary<int, DownloadState>(Info.PiecesHashArray.Count);
                for (int i = 0; i < Info.PiecesHashArray.Count; i++)
                {
                    DownloadState[i] = new DownloadState(s[i]);
                }
            }
            else
            {
                DownloadState = new Dictionary<int, DownloadState>(Info.PiecesHashArray.Count);
                for (int i = 0; i < Info.PiecesHashArray.Count; i++)
                {
                    DownloadState[i] = new DownloadState(false);
                }
            }

            //如果是多文件，预先创建文件夹
            if (Info.Files != null)
            {
                if (!Directory.Exists(_baseDir + "/" + Info.Name))
                {
                    Directory.CreateDirectory(_baseDir + "/" + Info.Name);
                }
                foreach (var f in Info.Files)
                {
                    if (f.Path.Count > 1)
                    {
                        var p = _baseDir + "/" + Info.Name;
                        for (int i = 0; i < f.Path.Count - 1; i++)
                        {
                            p += "/" + f.Path[i];
                            if (!Directory.Exists(p))
                            {
                                Directory.CreateDirectory(p);
                            }
                        }
                    }
                }
            }

            //配置数据写入进程
            ConsumeData();

            _udpServer.Start();
        }

        private void ConsumeData()
        {
            for (int i = 0; i < 1; i++)
            {
                int index = i;
                _ = Task.Factory.StartNew(() =>
                {
                    foreach (var item in _writeToFileDB.GetConsumingEnumerable())
                    {
                        int num = index;
                        _logger.LogError($"index:{index}    total:{_writeToFileDB.Count}");
                        try
                        {
                            WriteToFile(item.buf, item.index, item.begin, item.peer);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex.Message);
                            throw ex;
                        }
                        finally
                        {
                            ConsumerHold.Release();
                        }
                    }
                    _writeToFileDB.Dispose();
                }, TaskCreationOptions.LongRunning);
            }
        }

        public Guid Id { get { return _guid; } }

        private AnnounceItem _announce;
        public AnnounceItem Announce
        {
            get
            {
                if (_announce == null)
                {
                    _announce = new AnnounceItem((_dictionaryField["announce"] as StringField)?.Value);
                }
                return _announce;
            }
        }
        private List<List<AnnounceItem>> _announce_list;
        public List<List<AnnounceItem>> Announce_list
        {
            get
            {
                if (_announce_list == null)
                {
                    _announce_list = (_dictionaryField["announce-list"] as ListField)?.Value
                    .Select(m => (m as ListField)?.Value.Select(n => new AnnounceItem((n as StringField)?.Value)).ToList()).ToList();
                    if (_announce_list == null)
                    {
                        _announce_list = new List<List<AnnounceItem>>();
                    }
                }
                return _announce_list;
            }
        }
        public string Comment
        {
            get
            {
                return (_dictionaryField["comment"] as StringField)?.Value;
            }
        }
        public string Create_by
        {
            get
            {
                return (_dictionaryField["create by"] as StringField)?.Value;
            }
        }
        public long Creation_date
        {
            get
            {
                return (_dictionaryField["creation date"] as NumberField)?.Value ?? 0;
            }
        }
        public string Encoding
        {
            get
            {
                return (_dictionaryField["encoding"] as StringField)?.Value;
            }
        }
        public Info Info { get; set; }

        private readonly string _downloadStateFile;

        public override bool Equals(object obj)
        {
            if (obj == null || obj.GetType() != typeof(TorrentModel))
            {
                return false;
            }
            var m = obj as TorrentModel;
            if (Info.Sha1Hash == null || m.Info.Sha1Hash == null)
            {
                return false;
            }
            return this.Info.Sha1Hash.SequenceEqual(m.Info.Sha1Hash);
        }

        public override int GetHashCode()
        {
            return Info.Sha1Hash.Aggregate(13, (s, item) => s + 23 * item);
        }

        public void AddWriteToFile(byte[] buf, int index, int begin, Peer peer)
        {
            _writeToFileDB.Add((buf, index, begin, peer));
        }

        private SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(0);
        private ManualResetEventSlim _manualResetEventSlim = new ManualResetEventSlim(false);
        public void AddStart()
        {
            _semaphoreSlim.Release();
        }
        public void AddFinish()
        {
            _semaphoreSlim.Wait();
        }
        private void WriteToFile(byte[] buf, int index, int begin, Peer peer)
        {

            if (Info.Files != null)
            {
                //多文件
                var start = Info.Piece_length * index + begin;
                var end = start + buf.Length;
                long sum = 0;
                long writeLen = 0;
                foreach (var item in Info.Files)
                {
                    if (start >= sum && start < (sum + item.Length))
                    {
                        if ((item.Length + sum - start) >= buf.Length - writeLen)
                        {
                            //最后一节
                            long count = end - start;
                            WriteFile(start - sum, writeLen, count, buf, item);
                            writeLen += count;
                        }
                        else
                        {
                            long count = (item.Length + sum - start);

                            WriteFile(start - sum, writeLen, count, buf, item);

                            writeLen += count;
                            start += count;
                        }
                        if (writeLen == buf.Length)
                        {
                            break;
                        }
                    }
                    sum += item.Length;
                }
            }
            else
            {
                lock (_lock)
                {
                    //单文件
                    var filename = this.Info.Name;
                    var fs = _fileStreamPool.GetStream($"{_baseDir}/{filename}");
                    fs.Position = index * Info.Piece_length + begin;
                    fs.Write(buf, 0, buf.Length);
                }
            }
            var ditem = this.DownloadState[index];
            ditem.DownloadCount += buf.Length;
            if (ditem.DownloadCount >= Info.Piece_length
            || (Info.Files == null && index == (this.DownloadState.Count - 1) && ditem.DownloadCount >= (Info.Length - Info.Piece_length * (this.DownloadState.Count - 1)))
            || (Info.Files != null && index == (this.DownloadState.Count - 1) && ditem.DownloadCount >= (Info.Files.Sum(m => m.Length) - Info.Piece_length * (this.DownloadState.Count - 1)))
                )
            {
                using (SHA1 _sha1 = SHA1.Create())
                {
                    var shares = _sha1.ComputeHash(GetPeicePart(index));
                    if (Info.PiecesHashArray[index].SequenceEqual(shares))
                    {
                        ditem.IsDownloded = true;
                        ditem.Peer = null;
                        peer.SendHave(index);
                        //保存下载进度
                        SaveDownloadProcess();
                    }
                }

            }
            if (!this.DownloadState.Any(m => m.Value.IsDownloded == false))
            {
                _logger.LogWarnning("下载完毕");
                IsFinish = true;
                _writeToFileDB.CompleteAdding();
                _fileStreamPool.Dispose();
                DownloadComplete();
                _manualResetEventSlim.Set();
            }
        }

        private void WriteFile(long fileoffset, long bufOffset, long len, byte[] buf, FileInfo item)
        {
            lock (_lock)
            {
                var filename = _baseDir + "/" + Info.Name + "/" + item.FileName;
                var fs = _fileStreamPool.GetStream($"{filename}");
                try
                {
                    fs.Position = fileoffset;
                    fs.Write(buf, (int)bufOffset, (int)len);
                }
                catch (Exception ex)
                {

                    throw;
                }
            }

        }

        private void SaveDownloadProcess()
        {
            lock (_lock)
            {
                using (var fs = new FileStream(_downloadStateFile, FileMode.Create))
                {
                    var dicdata = DownloadState.ToDictionary(m => m.Key, m => m.Value.IsDownloded);
                    var json = new System.Runtime.Serialization.Json.DataContractJsonSerializer(dicdata.GetType());
                    json.WriteObject(fs, dicdata);
                }
            }
        }
        private Dictionary<int, bool> LoadDownloadProcess()
        {
            using (var fs = new FileStream(_downloadStateFile, FileMode.Open))
            {
                var json = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(Dictionary<int, bool>));
                return json.ReadObject(fs) as Dictionary<int, bool>;
            }
        }

        /// <summary>
        /// 获取peice的数据进行sha1计算
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        private byte[] GetPeicePart(int index)
        {
            if (Info.Files == null)
            {
                //单文件
                var start = Info.Piece_length * index;
                var len = Math.Min(Info.Piece_length, Info.Length - start);
                var buf = new byte[len];

                var filename = this.Info.Name;
                lock (_lock)
                {
                    var fs = _fileStreamPool.GetStream($"{_baseDir}/{filename}");
                    fs.Position = start;
                    fs.Read(buf, 0, buf.Length);
                    return buf;
                }
            }
            else
            {
                //多文件
                var start = Info.Piece_length * index;
                var len = Math.Min(Info.Piece_length, Info.Files.Sum(m => m.Length) - start);
                var buf = new byte[len];

                var end = start + len;
                long sum = 0;
                long writeLen = 0;
                foreach (var item in Info.Files)
                {
                    if (start >= sum && start < (sum + item.Length))
                    {
                        if ((item.Length + sum - start) >= buf.Length - writeLen)
                        {
                            //最后一节
                            long count = end - start;
                            ReadFile(start - sum, writeLen, count, buf, item);
                            writeLen += count;
                        }
                        else
                        {
                            long count = (item.Length + sum - start);

                            ReadFile(start - sum, writeLen, count, buf, item);

                            writeLen += count;
                            start += count;
                        }
                        if (writeLen == buf.Length)
                        {
                            return buf;
                        }
                    }
                    sum += item.Length;
                }
            }
            return null;
        }

        private void ReadFile(long fileoffset, long bufoffset, long rlen, byte[] buf, FileInfo item)
        {
            lock (_lock)
            {
                var filename = _baseDir + "/" + Info.Name + "/" + item.FileName;
                var fs = _fileStreamPool.GetStream($"{filename}");
                try
                {
                    fs.Position = fileoffset;
                    if (bufoffset + rlen > buf.Length)
                    {
                        return;
                    }
                    fs.Read(buf, (int)bufoffset, (int)rlen);
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        public void Connecting()
        {
            Task.Factory.StartNew(() =>
            {
                _udpServer.Connecting(this);
            }, TaskCreationOptions.LongRunning);
        }

        public void WaitFinish()
        {
            _manualResetEventSlim.Wait();
        }

        public byte[] GetPeicePart(int index, int begin, int length)
        {
            if (Info.Files == null)
            {
                //单文件
                var start = Info.Piece_length * index + begin;
                var len = length;
                var buf = new byte[len];

                var filename = this.Info.Name;
                lock (_lock)
                {
                    var fs = _fileStreamPool.GetStream($"{_baseDir}/{filename}");
                    fs.Position = start;
                    fs.Read(buf, 0, buf.Length);
                    return buf;
                }
            }
            else
            {
                //多文件
                var start = Info.Piece_length * index + begin;
                var len = length;
                var buf = new byte[len];

                var end = start + len;
                long sum = 0;
                long writeLen = 0;
                foreach (var item in Info.Files)
                {
                    if (start >= sum && start < (sum + item.Length))
                    {
                        if ((item.Length + sum - start) >= buf.Length)
                        {
                            //最后一节
                            long count = end - start;
                            ReadFile1(start - sum, writeLen, count, buf, item);
                            writeLen += count;
                        }
                        else
                        {
                            long count = (item.Length + sum - start);
                            ReadFile1(start - sum, writeLen, count, buf, item);

                            writeLen += count;
                            start += count;
                        }
                        if (writeLen == buf.Length)
                        {
                            return buf;
                        }
                    }
                    sum += item.Length;
                }
            }
            return null;

        }

        private void ReadFile1(long fileoffset, long bufoffset, long rlen, byte[] buf, FileInfo item)
        {
            lock (_lock)
            {
                var filename = _baseDir + "/" + Info.Name + "/" + item.FileName;
                var fs = _fileStreamPool.GetStream($"{filename}");
                fs.Position = fileoffset;
                fs.Read(buf, (int)bufoffset, (int)rlen);
            }
        }
    }
}
