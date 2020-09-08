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
            public sealed partial class Message: Node
            {
                private Message() { }

                public Protocol.Ordinal ReliableSequenceNumber;
                public bool IsReliable;

                /// <summary>
                /// Complete datagram if the message does not represent a fragment, otherwise null.
                /// </summary>
                public Memory Data;

                /// <summary>
                /// Reassembled datagram if the message represents the last fragment (of its datagram), otherwise null.
                /// <para/>
                /// After the delivery pipeline is stalled due to a missing reliable datagram, any fragmented
                /// datagrams that may follow must wait in the buffer. When the pipeline is released again the last
                /// fragment of a fragmented datagram must deliver the complete reassembly (if complete). A stored 
                /// reference avoids the need for a search in the collection of reassemblies.
                /// </summary>
                public Reassembly Reassembly;

                public override void Dispose() => OnDisposed(this);

                [DebuggerBrowsable(DebuggerBrowsableState.Never)]
                private Action<Message> OnDisposed;
            }
        }
    }
}
