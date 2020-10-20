using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Carambolas
{
    public static class ThreadExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Wait(this Thread thread)
        {
            if (!thread.ThreadState.HasFlag(ThreadState.Stopped | ThreadState.Unstarted))
                thread.Join();
        }
    }
}
