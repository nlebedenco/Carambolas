using System;
using System.Threading;

namespace Carambolas
{
    public static class ThreadExtensions
    {
        public static void Wait(this Thread thread)
        {
            if (!thread.ThreadState.HasFlag(ThreadState.Stopped | ThreadState.Unstarted))
                thread.Join();
        }
    }
}
