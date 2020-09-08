using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Carambolas.Net
{
    internal partial struct Channel
    {        
        public Inbound RX;
        public Outbound TX;

        public void Initialize(Protocol.Time time)
        {   
            // All next source times must be initialized with the connection time.
            RX.NextRemoteTimes = new Protocol.Ordinal.Window.Times((uint)time);

            // TX Ack count must be initialized to 1 as if a previous ack had been received with 
            // ack.Next = 0. This ensures a fast retransmit can be triggered under the proper 
            // threshold for the first packet as well.
            TX.Ack = (default, default, 1);
        }

        public void Dispose()
        {
            RX.Dispose();
            TX.Dispose();
        }        
    }   
}
