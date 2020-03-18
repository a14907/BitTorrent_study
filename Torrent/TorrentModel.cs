using Bencoding.Model;
using System;
using System.Collections.Generic;
using System.Linq;
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
    public partial class TorrentModel
    {
        private readonly DictionaryField _dictionaryField;
        private readonly Guid _guid = Guid.NewGuid();

        public Dictionary<int, bool> DownloadState;

        public List<Peer> Peers = new List<Peer>();

        public byte[] Bitfield
        {
            get
            {
                var buf = new byte[(int)Math.Ceiling(Info.PiecesHashArray.Count / (1.0 * 8))];
                for (int i = 0; i < Info.PiecesHashArray.Count; i++)
                {
                    var index = i / 8;
                    var bit = 7 - (i - index * 8);
                    var val = DownloadState[i] ? 1 : 0;
                    buf[index] = (byte)(buf[index] | (val << bit));
                }
                return buf;
            }
        }

        public TorrentModel(DictionaryField dictionaryField)
        {
            _dictionaryField = dictionaryField;
            Info = new Info(dictionaryField["info"] as DictionaryField);
            DownloadState = new Dictionary<int, bool>(Info.PiecesHashArray.Count);
            for (int i = 0; i < Info.PiecesHashArray.Count; i++)
            {
                DownloadState[i] = false;
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
        public string Name { get { return (_dictionaryField["Name"] as StringField)?.Value; } }

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

    }
}
