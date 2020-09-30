using System;
using System.Runtime.InteropServices;
using System.Threading;

using Carambolas.Security.Cryptography;
using Carambolas.Net.Sockets;

namespace Carambolas.Net
{
    public sealed partial class Host
    {
        [StructLayout(LayoutKind.Auto)]
        public readonly struct Settings
        {
            public static readonly Settings Default = new Settings(0, Protocol.MTC.Default, Protocol.MTU.Default, Protocol.Bandwidth.MaxValue, int.MaxValue, in Host.Stream.Settings.Default, in Host.Stream.Settings.Default, Protocol.TTL.Default, Protocol.Memory.Block.Size.Default);

            /// <summary>
            /// Maximum number of passive (incoming) connections supported by the host. 
            /// Does not affect the number of active connections a host may initiate.
            /// </summary>
            public readonly ushort Capacity;

            public readonly byte MaxChannel;

            public readonly ushort MaxTransmissionUnit;

            /// <summary>
            /// Maximum amount of user data that may be buffered for transmission in bytes intended for each peer. 
            /// </summary>
            public readonly int MaxTransmissionBacklog;

            /// <summary>
            /// Maximum receive bandwidth in bits per second intended for each peer. 
            /// This is the bandwidth advertised to the remote peer upon connection.
            /// Valid values are between <see cref="Protocol.Bandwidth.MinValue"/> and <see cref="Protocol.Bandwidth.MaxValue"/>.
            /// </summary>
            public readonly uint MaxBandwidth;

            public readonly Stream.Settings Upstream;
            public readonly Stream.Settings Downstream;

            /// <summary>
            /// Packet Time-to-Live (HopLimit on IPv6). 
            /// </summary>
            public readonly byte TTL;

            /// <summary>
            /// Packet Type of service. Only applicable to IPv4.
            /// Ignored on some platforms (e.g. Windows).
            /// </summary>
            public readonly TOS TOS;

            /// <summary>
            /// Allocation unit in bytes used for message buffers.
            /// </summary>
            public readonly int BlockSize;

            public Settings(ushort capacity, byte maxChannel = Protocol.MTC.Default, ushort maxTranmissionUnit = Protocol.MTU.Default, uint maxBandwidth = Protocol.Bandwidth.MaxValue, int maxTransmissionBacklog = int.MaxValue, byte ttl = Protocol.TTL.Default, int blockSize = Protocol.Memory.Block.Size.Default, TOS tos = TOS.LowDelay)
                : this(capacity, maxChannel, maxTranmissionUnit, maxBandwidth, maxTransmissionBacklog, in Host.Stream.Settings.Default, in Host.Stream.Settings.Default, ttl, blockSize) { }

            public Settings(ushort capacity, byte maxChannel, ushort maxTransmissionUnit, uint maxBandwidth, int maxTransmissionBacklog, in Host.Stream.Settings upstream, in Host.Stream.Settings downstream, byte ttl = Protocol.TTL.Default, int blockSize = Protocol.Memory.Block.Size.Default, TOS tos = TOS.LowDelay)
            {
                Capacity = capacity;
                MaxTransmissionUnit = maxTransmissionUnit;
                MaxChannel = maxChannel;
                MaxBandwidth = maxBandwidth;
                MaxTransmissionBacklog = maxTransmissionBacklog;
                Upstream = upstream;
                Downstream = downstream;
                TTL = ttl;
                TOS = tos;
                BlockSize = blockSize;
            }

            internal void CreateSocketSettings(out Socket.Settings settings) => settings = new Socket.Settings(Upstream.BufferSize, Downstream.BufferSize, Timeout.Infinite, Timeout.Infinite, TTL, Carambolas.Net.Sockets.SocketMode.NonBlocking, TOS);
        }
    }
}
