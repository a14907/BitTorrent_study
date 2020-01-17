using Bencoding.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace Tracker.Models
{
    public class TrackerResponse
    {
        private readonly DictionaryField _dictionaryField;
        private readonly string _url;

        public TrackerResponse(DictionaryField dictionaryField, string url)
        {
            _dictionaryField = dictionaryField;
            _url = url;
        }
        public string Url { get { return _url; } }
        public long Complete { get { return (_dictionaryField["complete"] as NumberField)?.Value ?? 0; } }
        public long Downloaded { get { return (_dictionaryField["downloaded"] as NumberField)?.Value ?? 0; } }
        public long Incomplete { get { return (_dictionaryField["incomplete"] as NumberField)?.Value ?? 0; } }
        public long Interval { get { return (_dictionaryField["interval"] as NumberField)?.Value ?? 0; } }
        public long MinInterval { get { return (_dictionaryField["min interval"] as NumberField)?.Value ?? 0; } }

        private IPEndPoint[] _peers;
        public IPEndPoint[] Peers
        {
            get
            {
                if (_peers == null)
                {
                    var buf = (_dictionaryField["peers"] as StringField)?.Buffer;
                    int count = buf.Length / 6;
                    var ls = new IPEndPoint[count];
                    for (int i = 0; i < count; i++)
                    {
                        var section = buf.Skip(i * 6).Take(6);
                        IPAddress iPAddress = new IPAddress(buf.Take(4).ToArray());
                        var port = BitConverter.ToUInt16(section.ToArray(), 4);
                        ls[i] = new IPEndPoint(iPAddress, port);
                    }
                    _peers = ls.Distinct().ToArray();
                }
                return _peers;
            }
        }
    }
}
