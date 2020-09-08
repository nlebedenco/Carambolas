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
                private Message() { }

                public Message Prev { get; private set; }
                public Message Next { get; private set; }

                public Protocol.Delivery Delivery;
                public Protocol.Ordinal SequenceNumber;

                /// <summary>
                /// After the message is transmitted at least once, this property contains the source time of the first transmission.
                /// <para/>
                /// Before the first transmission, this is used to sore a time limit beyond which the message is considered expired and must be dropped
                /// (only applicable to <see cref="Protocol.Delivery.Unreliable"/>).
                /// </summary>
                public Protocol.Time FirstSendTime;

                public Protocol.Time LatestSendTime;

                /// <summary>
                /// Payload data size. Value must be >= 1 when the message is acquired; 0 when disposed.
                /// </summary>
                public ushort Payload;

                /// <summary>
                /// Encoded message used for retransmissions.
                /// </summary>
                public Memory Encoded;

                [DebuggerBrowsable(DebuggerBrowsableState.Never)]
                private Action<Message> OnReleased;

                public void AddAfter(Message other)
                {
                    if (other != null)
                    {
                        Next = other.Next;
                        Prev = other;

                        if (Next != null)
                            Next.Prev = this;

                        Prev.Next = this;
                    }
                }

                public void Dispose()
                {
                    Remove();
                    OnReleased(this);
                }

                private void Remove()
                {
                    if (Prev != null)
                    {
                        Prev.Next = Next;
                        if (Next != null)
                        {
                            Next.Prev = Prev;
                            Next = null;
                        }

                        Prev = null;
                    }
                    else if (Next != null)
                    {
                        Next.Prev = null;
                        Next = null;
                    }
                }
            }
        }
    }
}