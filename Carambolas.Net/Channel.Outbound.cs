using System;
using System.Threading;

namespace Carambolas.Net
{
    internal partial struct Channel
    {
        public partial struct Outbound
        {
            private SpinLock sendLock;
            private Message.List sendBuffer;

            public Message.List Messages;

            /// <summary>
            /// Pointer to the next message to transmit. 
            /// Does not apply to retransmissions. 
            /// Null if there's no message to transmit next.
            /// </summary>
            public Message Next;

            /// <summary>
            /// Pointer to the next message to retransmit. 
            /// Null if there's no message to retransmit next.
            /// </summary>
            public Message Retransmit;

            /// <summary>
            /// Next SEQ to transmit.
            /// </summary>
            public Protocol.Ordinal NextSequenceNumber;

            /// <summary>
            /// Next RSN to transmit.
            /// </summary>
            public Protocol.Ordinal NextReliableSequenceNumber;

            /// <summary>
            /// Latest received ack source time.
            /// </summary>
            public Protocol.Time LatestAckRemoteTime;

            /// <summary>
            /// Interval of sequence numbers expected by the remote host according to the latest ack
            /// and number of times the same sequence number has been acknowledged by the remote host.
            /// </summary>
            public (Protocol.Ordinal Next, Protocol.Ordinal Last, ushort Count) Ack;

            /// <summary>
            /// Send message from the user thread.
            /// </summary>
            public void Send(Message message)
            {
                var locked = false;
                try
                {
                    sendLock.Enter(ref locked);
                    sendBuffer.AddLast(message);
                }
                finally
                {
                    if (locked)
                        sendLock.Exit(false);
                }
            }

            /// <summary>
            /// Send chain of messages from the user thread.
            /// </summary>
            public void Send(Message from, Message to)
            {
                var locked = false;
                try
                {
                    sendLock.Enter(ref locked);
                    sendBuffer.AddLast(from, to);
                }
                finally
                {
                    if (locked)
                        sendLock.Exit(false);
                }
            }

            /// <summary>
            /// Flush the send buffer into the transmit buffer.
            /// Not to be used from the user thread.
            /// </summary>
            public void Flush()
            {
                var locked = false;
                try
                {
                    sendLock.Enter(ref locked);
                    if (!sendBuffer.IsEmpty)
                    {
                        Messages.AddLast(in sendBuffer);
                        if (Next == null)
                            Next = sendBuffer.First;

                        sendBuffer.Clear();
                    }
                }
                finally
                {
                    if (locked)
                        sendLock.Exit(false);
                }
            }

            /// <summary>
            /// Setup retransmission if needed.
            /// Not to be used from the user thread.
            /// </summary>
            public void Timeout(ref byte retransmittingChannelsCount)
            {
                if (Messages.First != Next)
                {
                    if (Retransmit == null)
                    {
                        retransmittingChannelsCount++;
                        Ack.Last = NextSequenceNumber - 1;
                        Ack.Count = 1;
                    }

                    Retransmit = Messages.First;
                }
            }

            public void Dispose()
            {
                sendBuffer.Dispose();
                Messages.Dispose();

                Next = default;
                Retransmit = default;
            }
        }
    }
}