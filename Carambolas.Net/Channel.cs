using System;
using System.Runtime.InteropServices;

namespace Carambolas.Net
{
    [StructLayout(LayoutKind.Auto)]
    internal partial struct Channel
    {        
        public Inbound RX;
        public Outbound TX;

        public byte Index;

        // Each channel keeps track of the next and previous channel ready to send forming a loose linked list 
        // based  on integer indexes in the array of Peer.channels.

        #region Send list

        /// <summary>
        /// Index of the next channel to send.
        /// </summary>
        public byte NextToSend { get; private set; }

        /// <summary>
        /// Index of the previous channel to send.
        /// </summary>
        public byte PreviousToSend { get; private set; }

        public void AddToSendListBefore(ref Channel other)
        {
            NextToSend = other.Index;
            PreviousToSend = other.PreviousToSend;
            other.PreviousToSend = Index;
        }

        public void RemoveFromSendList(ref Channel prev, ref Channel next)
        {
            prev.NextToSend = NextToSend;
            next.PreviousToSend = PreviousToSend;

            NextToSend = PreviousToSend = 0;
        }

        #endregion

        public void Initialize(byte index, Protocol.Time time)
        {
            Index = index;

            // All next source times must be initialized with the connection time.
            RX.NextRemoteTimes = new Protocol.Ordinal.Window.Times(time);

            // TX Ack count must be initialized to 1 as if a previous ack had been received with 
            // ack.Next = 0. This ensures a fast retransmit can be triggered under the proper 
            // threshold for the first packet as well.
            TX.Ack = (default, default, 1);
        }

        public void Adjust(Protocol.Time time) => RX.NextRemoteTimes.Adjust(time);

        public void Dispose()
        {
            RX.Dispose();
            TX.Dispose();
        }        
    }   
}
