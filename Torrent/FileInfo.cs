using Bencoding.Model;
using System.Collections.Generic;
using System.Linq;

namespace Torrent
{
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
