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
                public sealed class Pool: Pool<Message>
                {
                    protected override Message Create() => new Message() { OnReleased = Return };

                    protected override void OnReturn(Message instance)
                    {
                        instance.Delivery = default;
                        instance.SequenceNumber = default;
                        instance.FirstSendTime = default;
                        instance.LatestSendTime = default;
                        instance.Payload = default;
                        instance.Encoded?.Dispose();
                        instance.Encoded = default;
                    }
                }
            }
        }
    }
}