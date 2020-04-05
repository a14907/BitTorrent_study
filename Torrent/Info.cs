using Bencoding.Model;
using System.Collections.Generic;
using System.Linq;

namespace Torrent
{
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
}
