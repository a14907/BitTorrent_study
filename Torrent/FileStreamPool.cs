using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Torrent
{
    public class FileStreamPool : IDisposable
    {
        private readonly object _lockobj;
        private Dictionary<string, FileStream> _cache = new Dictionary<string, FileStream>();
        private Timer _timer;
        private Logger _logger = new Logger();

        public FileStreamPool(object lockobj)
        {
            _timer = new Timer((obj) =>
            {
                FlushData();
            }, null, 10000, 10000);
            _lockobj = lockobj;
        }

        public void FlushData()
        {
            lock (_lockobj)
            {
                _logger.LogError("将数据刷入文件999999999999999999999999999999999999999999999999999999999999999999999999999999");
                foreach (var item in _cache)
                {
                    item.Value.Flush();
                }
            }
        }

        public FileStream GetStream(string path)
        {
            if (_cache.ContainsKey(path))
            {
                return _cache[path];
            }
            var fs = new FileStream(path, FileMode.OpenOrCreate);
            _cache.Add(path, fs);
            return fs;
        }

        #region IDisposable Support
        private bool disposedValue = false; // 要检测冗余调用

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _logger.LogError("关闭文件");
                    _timer.Dispose();
                    foreach (var item in _cache)
                    {
                        item.Value.Dispose();
                    }
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion

    }
}
