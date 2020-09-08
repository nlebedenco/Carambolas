using System;
using System.Runtime.InteropServices;

namespace Carambolas.Net
{
    public enum EventType: byte
    {
        None = 0,
        Connection = 1,
        Disconnection = 2,
        Data = 3,
    }

    [StructLayout(LayoutKind.Auto)]
    public readonly struct Event
    {
        public readonly Peer Peer;
        public readonly EventType EventType;
        public readonly PeerReason Reason;
        public readonly Data Data;

        internal Event(Peer peer)
        {
            Peer = peer;
            EventType = EventType.Connection;
            Reason = default;
            Data = default;
        }

        internal Event(Peer peer, PeerReason reason = default)
        {
            Peer = peer;
            EventType = EventType.Disconnection;
            Reason = reason;
            Data = default;
        }

        internal Event(Peer peer, in Data d)
        {
            Peer = peer;
            EventType = EventType.Data;
            Reason = default;
            Data = d;
        }

        internal void Dispose() => Data.Dispose();
    }
}
