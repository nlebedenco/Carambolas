using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Carambolas.Net
{
    internal partial struct Channel
    {
        public partial struct Inbound
        {
            public sealed partial class Reassembly
            {
                [DebuggerDisplay("Count = {queue.Count}")]
                public sealed class Pool: IDisposable
                {
                    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
                    private SpinLock queueLock = new SpinLock(false);

                    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
                    private Queue<Reassembly> queue = new Queue<Reassembly>(256);

                    public Reassembly Get()
                    {
                        var locked = false;
                        try
                        {
                            queueLock.Enter(ref locked);
                            if ((queue ?? throw new ObjectDisposedException(GetType().FullName)).Count > 0)
                                return queue.Dequeue();
                        }
                        finally
                        {
                            if (locked)
                                queueLock.Exit(false);
                        }

                        return new Reassembly() { OnDisposed = Return };
                    }

                    private void Return(Reassembly instance)
                    {
                        instance.SequenceNumber = default;

                        instance.Data?.Dispose();
                        instance.Data = null;

                        if (queue != null)
                        {
                            var locked = false;
                            try
                            {
                                queueLock.Enter(ref locked);
                                queue.Enqueue(instance);
                            }
                            finally
                            {
                                if (locked)
                                    queueLock.Exit(false);
                            }
                        }
                    }

                    public void Dispose() => queue = null;
                }
            }
        }
    }
}
