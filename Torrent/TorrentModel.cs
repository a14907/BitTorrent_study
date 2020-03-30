using Bencoding.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Tracker.Models;

namespace Torrent
{
    public class AnnounceItem
    {
        public AnnounceItem(string url)
        {
            Url = url;
        }

        public string Url { get; set; }
    }

    public class DownloadState
    {

        public DownloadState(bool isDownloded)
        {
            IsDownloded = isDownloded;
        }
        public DownloadState()
        {

        }

        public bool IsDownloded { get; set; }
        public int DownloadCount { get; set; }
        public Peer Peer { get; set; }
    }
    public delegate void DownloadComplete();
    public partial class TorrentModel
    {
        private readonly DictionaryField _dictionaryField;
        private readonly SHA1 _sha1 = SHA1.Create();
        private readonly Guid _guid = Guid.NewGuid();
        public Dictionary<int, DownloadState> DownloadState;
        public List<Peer> Peers = new List<Peer>();
        private readonly object _lock = new object();
        private readonly BlockingCollection<(byte[] buf, int index, int begin, Peer peer)> _writeToFileDB = new BlockingCollection<(byte[], int, int, Peer)>();

        public event DownloadComplete DownloadComplete;

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
            _dictionaryField = dictionaryField;
            Info = new Info(dictionaryField["info"] as DictionaryField);
            DownloadState = new Dictionary<int, DownloadState>(Info.PiecesHashArray.Count);
            for (int i = 0; i < Info.PiecesHashArray.Count; i++)
            {
                DownloadState[i] = new DownloadState(false);
            }

            //如果是多文件，预先创建文件夹
            if (Info.Files != null)
            {
                if (!Directory.Exists(Info.Name))
                {
                    Directory.CreateDirectory(Info.Name);
                }
                foreach (var f in Info.Files)
                {
                    if (f.Path.Count > 1)
                    {
                        var p = Info.Name;
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
            _ = Task.Factory.StartNew(() =>
            {
                foreach (var item in _writeToFileDB.GetConsumingEnumerable())
                {
                    WriteToFile(item.buf, item.index, item.begin, item.peer);
                }
            }, TaskCreationOptions.LongRunning);

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
        public List<TrackerResponse> TrackerResponse { get; } = new List<TrackerResponse>();

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
                        if ((item.Length + sum - start) >= buf.Length)
                        {
                            //最后一节
                            long count = end - start;
                            WriteFile(start - sum, writeLen, count);
                            writeLen += count;
                        }
                        else
                        {
                            long count = (item.Length + sum - start);

                            WriteFile(start - sum, writeLen, count);

                            writeLen += count;
                            start += count;
                        }
                        if (writeLen == buf.Length)
                        {
                            break;
                        }
                    }
                    sum += item.Length;

                    void WriteFile(long fileoffset, long bufOffset, long len)
                    {
                        var filename = Info.Name + "/" + item.FileName;
                        using (var fs = new FileStream($"{filename}", FileMode.OpenOrCreate))
                        {
                            fs.Position = fileoffset;
                            fs.Write(buf, (int)bufOffset, (int)len);
                        }
                    }
                }
            }
            else
            {
                //单文件
                var filename = this.Info.Name;
                using (var fs = new FileStream($"{filename}", FileMode.OpenOrCreate))
                {
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
                var shares = _sha1.ComputeHash(GetPart(index));
                if (Info.PiecesHashArray[index].SequenceEqual(shares))
                {
                    ditem.IsDownloded = true;
                    ditem.Peer = null;
                    peer.SendHave(index);
                }
            }
            if (!this.DownloadState.Any(m => m.Value.IsDownloded == false))
            {
                Console.WriteLine("下载完毕");
                _writeToFileDB.CompleteAdding();
                DownloadComplete();
            }
        }

        private byte[] GetPart(int index)
        {
            if (Info.Files == null)
            {
                //单文件
                var start = Info.Piece_length * index;
                var len = Math.Min(Info.Piece_length, Info.Length - start);
                var buf = new byte[len];

                var filename = this.Info.Name;
                using (var fs = new FileStream($"{filename}", FileMode.OpenOrCreate))
                {
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
                        if ((item.Length + sum - start) >= buf.Length)
                        {
                            //最后一节
                            long count = end - start;
                            ReadFile(start - sum, writeLen, count);
                            writeLen += count;
                        }
                        else
                        {
                            long count = (item.Length + sum - start);

                            ReadFile(start - sum, writeLen, count);

                            writeLen += count;
                            start += count;
                        }
                        if (writeLen == buf.Length)
                        {
                            return buf;
                        }
                    }
                    sum += item.Length;

                    void ReadFile(long fileoffset, long bufoffset, long rlen)
                    {
                        var filename = Info.Name + "/" + item.FileName;
                        using (var fs = new FileStream($"{filename}", FileMode.OpenOrCreate))
                        {
                            fs.Position = fileoffset;
                            fs.Read(buf, (int)bufoffset, (int)rlen);
                        }
                    }
                }
            }
            return null;
        }
    }

    public class Info
    {
        private DictionaryField _dictionaryField;

        public Info(DictionaryField dictionaryField)
        {
            _dictionaryField = dictionaryField;
        }

        public byte[] Sha1Hash
        {
            get
            {
                return _dictionaryField.Sha1Val;
            }
        }
        public long Piece_length { get { return (_dictionaryField["piece length"] as NumberField)?.Value ?? 0; } }
        public string Pieces { get { return (_dictionaryField["pieces"] as StringField)?.Value; } }
        public List<byte[]> PiecesHashArray
        {
            get
            {
                var arr = (_dictionaryField["pieces"] as StringField)?.Buffer;
                var c = arr.Length / 20;
                var ls = new List<byte[]>();
                for (int i = 0; i < c; i++)
                {
                    ls.Add(arr.Skip(20 * i).Take(20).ToArray());
                }
                return ls;
            }
        }
        public long Private { get { return (_dictionaryField["private"] as NumberField)?.Value ?? 0; } }
        public string Name { get { return (_dictionaryField["name"] as StringField)?.Value; } }

        #region 单文件
        public long Length { get { return (_dictionaryField["length"] as NumberField)?.Value ?? 0; } }
        public string Md5sum { get { return (_dictionaryField["md5sum"] as StringField)?.Value; } }
        #endregion

        #region 多文件
        public List<FileInfo> Files
        {
            get
            {
                var ls = _dictionaryField["files"] as ListField;
                if (ls == null)
                {
                    return null;
                }
                return ls.Value.Select(m => new FileInfo(m as DictionaryField)).ToList();
            }
        }
        #endregion
    }

    public class FileInfo
    {
        private DictionaryField _m;

        public FileInfo(DictionaryField m)
        {
            this._m = m;
        }

        public long Length { get { return (_m["length"] as NumberField)?.Value ?? 0; } }
        public string Md5sum { get { return (_m["md5sum"] as StringField)?.Value; } }
        public List<string> Path
        {
            get
            {
                return (_m["path"] as ListField)?.Value.Select(m => (m as StringField)?.Value).ToList();
            }
        }

        public string FileName
        {
            get
            {
                return string.Join("/", Path);
            }
        }
    }
}
