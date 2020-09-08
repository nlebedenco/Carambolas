using System;

namespace Carambolas.Net
{
    internal sealed class Log: ILog
    {
        public static ILog Default = new Log();

        private Log() { }

        public void Info(string s) { }

        public void Warn(string s) { }

        public void Error(string s) { }

        public void Exception(Exception e) { }
    }
}
