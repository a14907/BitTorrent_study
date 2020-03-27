using Bencoding.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Tracker.Models
{
    public class TrackerResponse
    {
        private readonly DictionaryField _dictionaryField;
        private readonly IPEndPoint _ipEndPoint;

        public TrackerResponse(DictionaryField dictionaryField, string httpurl)
        {
            _dictionaryField = dictionaryField;
            _ipEndPoint = GetIpEndpoint(httpurl);
        }

        private IPEndPoint GetIpEndpoint(string url)
        {
            var u = new Uri(url);
            return new IPEndPoint(Dns.GetHostAddresses(u.Host).FirstOrDefault(m => m.AddressFamily == AddressFamily.InterNetwork), u.Port);
        }

        public TrackerResponse(IPEndPoint ipEndPoint)
        {
            _ipEndPoint = ipEndPoint;
        }
        public IPEndPoint IPEndPoint { get { return _ipEndPoint; } }

        private long _complete;
        public long Complete
        {
            get
            {
                if (_dictionaryField == null)
                {
                    return _complete;
                }
                return (_dictionaryField["complete"] as NumberField)?.Value ?? 0;
            }
            set
            {
                _complete = value;
            }
        }

        private long _downloaded;
        public long Downloaded
        {
            get
            {
                if (_dictionaryField == null)
                {
                    return _downloaded;
                }
                return (_dictionaryField["downloaded"] as NumberField)?.Value ?? 0;
            }
            set
            {
                _downloaded = value;
            }
        }

        private long _incomplete;
        public long Incomplete
        {
            get
            {
                if (_dictionaryField == null)
                {
                    return _incomplete;
                }
                return (_dictionaryField["incomplete"] as NumberField)?.Value ?? 0;
            }
            set
            {
                _incomplete = value;
            }
        }

        private long _interval;
        public long Interval
        {
            get
            {
                if (_dictionaryField == null)
                {
                    return _interval;
                }
                return (_dictionaryField["interval"] as NumberField)?.Value ?? 0;
            }
            set
            {
                _interval = value;
            }
        }

        private long _minInterval;
        public long MinInterval
        {
            get
            {
                if (_dictionaryField == null)
                {
                    return _minInterval;
                }
                return (_dictionaryField["min interval"] as NumberField)?.Value ?? 0;
            }
            set
            {
                _minInterval = value;
            }
        }

        private IPEndPoint[] _peers;
        public IPEndPoint[] Peers
        {
            get
            {
                if (_peers == null)
                {
                    if (_dictionaryField == null)
                    {
                        return _peers;
                    }
                    var buf = (_dictionaryField["peers"] as StringField)?.Buffer;
                    if (buf == null)
                    {
                        return new IPEndPoint[0];
                    }
                    int count = buf.Length / 6;
                    var ls = new IPEndPoint[count];
                    for (int i = 0; i < count; i++)
                    {
                        var section = buf.Skip(i * 6).Take(6);
                        IPAddress iPAddress = new IPAddress(section.Take(4).ToArray());
                        var port = (int)FromBytes(section.Skip(4).Take(2).ToArray(), 0, 2);


                        //IPAddress iPAddress = new IPAddress(IPAddress.NetworkToHostOrder(BitConverter.ToInt32(section.Take(4).ToArray(), 0)));
                        //var port = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(section.ToArray(), 4));

                        ls[i] = new IPEndPoint(iPAddress, port);
                    }
                    _peers = ls.Distinct().ToArray();
                }
                return _peers;
            }
            set
            {
                _peers = value;
            }
        }

        long FromBytes(byte[] buffer, int startIndex, int bytesToConvert)
        {
            long ret = 0L;
            for (int i = 0; i < bytesToConvert; i++)
            {
                ret = (ret << 8 | buffer[startIndex + i]);
            }
            return ret;
        }
    }
}
