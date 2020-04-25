using System;

namespace Torrent
{
    public class Logger
    {
        private readonly LogLevel _logLevel;

        public Logger(LogLevel logLevel = LogLevel.Error)
        {
            _logLevel = logLevel;
        }
        public void LogInformation(string log)
        {
            if (_logLevel > LogLevel.Information)
            {
                return;
            }
            Console.WriteLine(log);
        }
        public void LogWarnning(string log)
        {
            if (_logLevel > LogLevel.Warnning)
            {
                return;
            }
            Console.WriteLine(log);
        }
        public void LogError(string log)
        {
            if (_logLevel > LogLevel.Error)
            {
                return;
            }
            Console.WriteLine(log + "********************************************************************");
        }

        public enum LogLevel
        {
            Information = 1,
            Warnning = 2,
            Error = 3
        }
    }
}
