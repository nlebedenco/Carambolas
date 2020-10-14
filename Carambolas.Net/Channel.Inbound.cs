using System;
using System.Runtime.InteropServices;

namespace Carambolas.Net
{
    internal partial struct Channel
    {
        [StructLayout(LayoutKind.Auto)]
        public partial struct Inbound
        {
            public Node.Tree<Reassembly> Reassemblies;
            public Node.Tree<Message> Messages;

            /// <summary>
            /// Next sequence number expected.
            /// </summary>
            public Protocol.Ordinal NextSequenceNumber;

            /// <summary>
            /// Last sequence number expected if there is a gap. Otherwise the same as <see cref="NextSequenceNumber"/>
            /// </summary>
            public Protocol.Ordinal LastSequenceNumber { get; private set; }

            /// <summary>
            /// Next reliable sequence number expected.
            /// </summary>
            public Protocol.Ordinal NextReliableSequenceNumber;

            /// <summary>
            /// A message must have a sequence number greater than or equal to this one to be acknowledgeable.
            /// </summary>
            public Protocol.Ordinal LowestSequenceNumber { get; private set; }

            private ushort crossSequenceNumber;

            /// <summary>
            /// Value in the range [0 .. (2 * <see cref="Protocol.Ordinal.Window.Size"/>) -1] associated with <see cref="NextSequenceNumber"/> and used to determine the current static window.
            /// <para/>
            /// Refer to <see cref="Protocol.Ordinal"/> for more information.
            /// </summary>
            public ushort CrossSequenceNumber
            {
                get => crossSequenceNumber;
                set => crossSequenceNumber = (ushort)(value % (2 * Protocol.Ordinal.Window.Size));
            }

            /// <summary>
            /// A message must have a source time that is greater than or equal to the one corresponding 
            /// to its static window to be acceptable. Refer to <seealso cref="Protocol.Ordinal"/> for 
            /// more information about static windows.
            /// </summary>
            public Protocol.Ordinal.Window.Times NextRemoteTimes;

            /// <summary>
            /// Sequence number to acknowledge, number of times to acknowledge and
            /// time to acknowledge (latest source time received since transmission of last ack).
            /// An ack must be transmitted when Count &gt; 0.
            /// </summary>
            public (Protocol.Ordinal SequenceNumber, ushort Count, Protocol.Time LatestRemoteTime) Ack;

            public void UpdateLastSequenceNumber() => LastSequenceNumber = Messages.First != null ? (Messages.First.SequenceNumber - 1) : NextSequenceNumber;

            public void UpdateLowestSequenceNumber()
            {
                var value = NextSequenceNumber - Protocol.Ordinal.Window.Size;
                if (value > LowestSequenceNumber)
                    LowestSequenceNumber = value;
            }

            /// <summary>
            /// Update max source time for the next incarnation of this static window [= (<paramref name="window"/> + 2) % 4].
            /// </summary>
            public void UpdateNextRemoteTime(int window, Protocol.Time remoteTime)
            {
                window = (window + 2) & 0x03;
                var max = NextRemoteTimes[window];
                if (max < remoteTime)
                    NextRemoteTimes[window] = remoteTime;
            }          

            public void Dispose()
            {
                Reassemblies.RemoveAndDisposeAll();
                Messages.RemoveAndDisposeAll();
            }
        }
    }
}
