using System;

namespace Carambolas.Net
{
    public enum PeerState
    {
        /// <summary>
        /// No events may be received. No data can be sent.
        /// </summary>
        Disconnected = 0,

        /// <summary>
        /// Connection is in progress. Only <see cref="EventType.Connection"/> and <see cref="EventType.Disconnection"/> may be received. 
        /// No data can be sent. 
        /// </summary>
        Connecting = 1,

        /// <summary>
        /// <see cref="EventType.Data"/> may be received. Data can be sent.
        /// </summary>
        Connected = 2,

        /// <summary>
        /// Disconnection is in progress. <see cref="EventType.Data"/> may still be received. 
        /// No data can be sent.
        /// </summary>
        Disconnecting = 3,
    }
}
