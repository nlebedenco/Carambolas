using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Carambolas.Net
{
    internal partial struct Channel
    {
        public partial struct Outbound
        {
            public sealed partial class Message
            {
                [DebuggerDisplay("Count = {queue.Count}")]
                public sealed class Pool: IDisposable
                {
                    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
                    private SpinLock queueLock = new SpinLock(false);

                    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
                    private Queue<Message> queue = new Queue<Message>(256);

                    public Message Get()
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

                        return new Message() { OnReleased = Return };
                    }

                    private void Return(Message instance)
                    {
                        instance.Delivery = default;
                        instance.SequenceNumber = default;
                        instance.FirstSendTime = default;
                        instance.LatestSendTime = default;
                        instance.Payload = default;
                        instance.Encoded?.Dispose();
                        instance.Encoded = default;

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