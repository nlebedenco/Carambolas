using System;
using System.Collections.Generic;
using System.Text;

namespace Carambolas.Net
{
    public interface ILog
    {
        void Info(string s);
        void Warn(string s);
        void Error(string s);
        void Exception(Exception e);
    }
}
