using System;
using System.Collections.Generic;
using System.Text;

namespace Carambolas.Net
{
    public enum PeerReason: byte
    {
        None = 0,

        /// <summary>
        /// The connection was closed by the local host.
        /// </summary>
        Closed = 1,

        /// <summary>
        /// The connection was reset by the remote peer.
        /// </summary>
        Reset = 2,

        /// <summary>
        /// The remote peer has failed to respond.
        /// </summary>
        TimedOut = 3,

        /// <summary>
        /// Unrecoverable error.
        /// </summary>
        Error = 4,

        /// <summary>
        /// The remote host actively refused a connection.
        /// </summary>
        Refused = 5,

        /// <summary>
        /// The connection attempt was not answered.
        /// </summary>
        Unreachable = 6,

    }
}
