using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bencoding.Model
{
    public class TorrentModel
    {
        private readonly DictionaryField _dictionaryField;

        public TorrentModel(DictionaryField dictionaryField)
        {
            _dictionaryField = dictionaryField;
            Info = new Info(dictionaryField["info"] as DictionaryField);
        }

        public string Announce
        {
            get
            {
                return (_dictionaryField["announce"] as StringField)?.Value;
            }
        }
        public List<List<string>> Announce_list
        {
            get
            {
                return (_dictionaryField["announce-list"] as ListField)?.Value
                    .Select(m => (m as ListField)?.Value.Select(n => (n as StringField)?.Value).ToList()).ToList();
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
