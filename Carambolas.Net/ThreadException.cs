using System;

namespace Carambolas.Net
{
    public class ThreadException: ApplicationException
    {
        public ThreadException() { }
        public ThreadException(string message) : base(message) { }
        public ThreadException(string message, Exception innerException) : base(message, innerException) { }
    }
}
