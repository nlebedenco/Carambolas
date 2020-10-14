using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Carambolas.Net
{
    internal partial struct Channel
    {
        [StructLayout(LayoutKind.Auto)]
        public partial struct Outbound
        {
            /// <summary>
            /// Thread-safe output mediator to every channel's send buffer. This helps to reduce lock contention by
            /// allowing the worker thread to lock channels relative to the user thread only once per update frame
            /// and provides a single point of entry for new messages in the worker thread.
            /// </summary>
            [StructLayout(LayoutKind.Auto)]
            public struct Mediator
            {
                private readonly byte[] index;

                private int length;
                private SpinLock indexLock;

                public Mediator(int capacity) => (indexLock, index, length) = (new SpinLock(false), new byte[capacity], 0);

                /// <summary>
                /// Send message from the user thread.
                /// </summary>
                public void Send(ref Channel channel, Message message)
                {
                    var locked = false;
                    try
                    {
                        indexLock.Enter(ref locked);
                        if (channel.TX.buffer.IsEmpty)
                        {
                            index[length] = channel.Index;
                            length++;
                        }

                        channel.TX.Send(message);
                    }
                    finally
                    {
                        if (locked)
                            indexLock.Exit(false);
                    }
                }

                /// <summary>
                /// Send a chain of messages from the user thread.
                /// </summary>
                public void Send(ref Channel channel, Message from, Message to)
                {
                    var locked = false;
                    try
                    {
                        indexLock.Enter(ref locked);
                        if (channel.TX.buffer.IsEmpty)
                        {
                            index[length] = channel.Index;
                            length++;
                        }

                        channel.TX.Send(from, to);
                    }
                    finally
                    {
                        if (locked)
                            indexLock.Exit(false);
                    }
                }

                /// <summary>
                /// Flush every channel's send buffer into its corresponding transmit buffer and updates the list of channels to send if neeed.
                /// </summary>
                /// <param name="channels">All channels supported by the peer</param>
                /// <param name="current">Current channel that is ready to send (or channel 0 if none is ready)</param>
                public void Flush(Channel[] channels, ref Channel current)
                {
                    var locked = false;
                    try
                    {
                        indexLock.Enter(ref locked);
                        for (int i = 0; i < length; ++i)
                        {
                            ref var channel = ref channels[index[i]];                            
                            channel.TX.Flush();
                            // If channel is now ready to send and was not before, add it to the end of linked list of channels ready to send (right before the current)
                            if (((channel.NextToSend | channel.PreviousToSend) == 0) && channel.TX.Transmit != null)
                                channel.AddToSendListBefore(ref current);
                        }

                        length = 0;                        
                    }
                    finally
                    {
                        if (locked)
                            indexLock.Exit(false);
                    }
                }
            }

            /// <summary>
            /// List of messages to transmit and/or retransmit. This is the output queue.
            /// </summary>
            public Message.List Messages;

            /// <summary>
            /// Pointer to the next message to transmit. 
            /// Does not apply to retransmissions. 
            /// Null if there's no message to transmit next.
            /// </summary>
            public Message Transmit;

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

            #region Used by the Mediator

            /// <summary>
            /// Buffered messages from the user thread.
            /// </summary>
            private Message.List buffer;

            private void Send(Message message) => buffer.AddLast(message);

            private void Send(Message from, Message to) => buffer.AddLast(from, to);

            private void Flush()
            {
                if (!buffer.IsEmpty)
                {
                    Messages.AddLast(in buffer);
                    if (Transmit == null)
                        Transmit = buffer.First;

                    buffer.Clear();
                }
            }

            #endregion

            public void Dispose()
            {
                buffer.Dispose();
                Messages.Dispose();

                Transmit = default;
                Retransmit = default;
            }
        }
    }
}