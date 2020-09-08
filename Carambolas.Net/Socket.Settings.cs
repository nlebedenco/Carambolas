using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Carambolas.Net.Sockets
{
    public enum TOS: byte
    {
        None = 0x00,
        LowDelay = 0x10,
        Throughput = 0x08,
        Reliability = 0x04,
        LowCost = 0x02
    }

    public sealed partial class Socket: IDisposable
    {
        [StructLayout(LayoutKind.Auto)]
        public readonly struct Settings
        {
            public static readonly Settings Default = new Settings(8192, 4096, Timeout.Infinite, Timeout.Infinite, Protocol.TTL.Default, SocketMode.NonBlocking);

            public readonly SocketMode Mode;

            public readonly int SendBufferSize;
            public readonly int ReceiveBufferSize;

            public readonly int SendTimeout;
            public readonly int ReceiveTimeout;

            public readonly byte TTL;
            public readonly TOS TOS;

            public Settings(int sendBufferSize, int receiveBufferSize, int sendTimeout, int receiveTimeout, byte ttl = Protocol.TTL.Default, SocketMode mode = default, TOS tos = TOS.LowDelay)
            {
                Mode = mode;

                SendBufferSize = sendBufferSize;
                ReceiveBufferSize = receiveBufferSize;

                SendTimeout = sendTimeout;
                ReceiveTimeout = receiveTimeout;

                TTL = ttl;
                TOS = tos;
            }
        }
    }
}
