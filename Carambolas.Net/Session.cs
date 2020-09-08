using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

using Carambolas.Security.Cryptography;

namespace Carambolas.Net
{
    [Flags]
    internal enum SessionOptions
    {
        None = 0,
        Secure = 1,
        ValidateRemoteKey = 2
    }

    internal static class SessionOptionsExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains(this SessionOptions e, SessionOptions flags) => (e & flags) == flags;
    }

    [StructLayout(LayoutKind.Auto)]
    internal struct Session
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int state;
        internal Protocol.State State
        {
            get => (Protocol.State)state;
            set => state = (int)value;
        }

        /// <summary>
        /// Session number used to send packets.
        /// </summary>
        internal uint Local;

        /// <summary>
        /// Session number used to receive packets.
        /// </summary>
        internal uint Remote;

        internal SessionOptions Options;

        internal Key RemoteKey;

        /// <summary>
        /// Crypto box used for the secure session. Always null if the session is not secure.
        /// </summary>
        internal ICipher Cipher;
       
        internal ulong Nonce;

        /// <summary>
        /// Returns true if the connection was effectively disconnected by this call or false if it had been disconnected previously.
        /// </summary>
        /// <returns></returns>
        internal bool TryDisconnect(out Protocol.State previous) => (previous = (Protocol.State)Interlocked.Exchange(ref state, (int)Protocol.State.Disconnected)) != (int)Protocol.State.Disconnected;

        public override string ToString() => Remote.ToString("X8");
    }
}
