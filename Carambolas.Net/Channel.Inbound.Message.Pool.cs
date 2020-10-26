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
            public sealed partial class Message
            {
                [DebuggerDisplay("Count = {queue.Count}")]
                public sealed class Pool: Pool<Message>
                {
                    protected override Message Create() => new Message() { OnDisposed = Return };

                    protected override void OnReturn(Message instance)
                    {
                        instance.SequenceNumber = default;
                        instance.ReliableSequenceNumber = default;
                        instance.IsReliable = default;

                        instance.Data?.Dispose();
                        instance.Data = null;

                        // Differently than Data, a Reassembly is not owned 
                        // by a Message and must not be disposed here.
                        instance.Reassembly = null;
                    }
                }
            }
        }
    }
}
