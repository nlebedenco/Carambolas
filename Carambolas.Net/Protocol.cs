using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Carambolas.Security.Cryptography;

using Pointer = System.ArraySegment<byte>;

namespace Carambolas.Net
{
    public static class Protocol
    {
        [StructLayout(LayoutKind.Auto)]
        public readonly struct QoS
        {
            public static readonly QoS Unreliable = new QoS(Delivery.Unreliable);
            public static readonly QoS Semireliable = new QoS(Delivery.Semireliable);
            public static readonly QoS Reliable = new QoS(Delivery.Reliable);

            public static QoS Volatile(ushort milliseconds) => new QoS(Delivery.Unreliable, milliseconds);

            internal readonly Delivery Delivery;
            internal readonly uint Timelimit;

            internal QoS(Delivery delivery, uint milliseconds = 0) => (Delivery, Timelimit) = (delivery, milliseconds);
        }

        public enum Delivery
        {
            /// <summary>
            /// Data is sequenced but not retransmitted. 
            /// <para/>
            /// The receiver will discard it if it arrives after further data (out of order).
            /// </summary>
            Unreliable = 0,

            /// <summary>
            /// Data is sequenced and may be retransmitted.
            /// <para/>
            /// The receiver will discard it if it arrives after further data (out of order).
            /// </summary>
            Semireliable = 1,

            /// <summary>
            /// Data is sequenced and may be retransmitted.
            /// <para/>
            /// The receiver must stall delivery of further data until it arrives.
            /// A disconnection will be produced if the receiver fails to acknowledge this data
            /// within a certain time or number of retransmissions.
            /// </summary>
            Reliable = 2
        }

        public static class Datagram
        {
            public static class Size
            {
                public const ushort MinValue = 1;
                public const ushort MaxValue = 65535;

                public static ushort Clamp(ushort value) => Math.Max(MinValue, Math.Min(value, MaxValue));
            }
        }

        public static class Memory
        {
            public static class Block
            {
                public static class Size
                {
                    public const int Default = 64;
                }
            }
        }

        public static class Update
        {
            public static class Rate
            {
                public const ushort Default = 50;

                public const ushort MinValue = 1;
                public const ushort MaxValue = 1000;

                public static ushort Clamp(ushort value) => Math.Max(MinValue, Math.Min(value, MaxValue));
            }
        }

        /// <![CDATA[
        /// The combination of connection timeout and ack timeout with backoff and ack fail limit
        /// may sometimes produce an unexpected resulting behaviour. This is because with a 
        /// multiplicative backoff factor the time interval between consecutive ack timeouts (and
        /// eventual retransmissions) grows exponentially while connection timeout and ack fail
        /// limit are constants. The consequence is that depending on where the initial ack timeout 
        /// (derived from the RTT) stands relative to a threshold the number of retransmissions will
        /// be limited by the ack fail limit and the total timeout to disconnect is going to be less
        /// than the connection timeout. As the initial ack timeout moves beyond this threshold, the
        /// reponse timeout becomes the limiting factor so the actual number of retransmissions 
        /// amount to less than the ack fail limit.
        ///
        /// The threshold in case can be calculated taking into account the backoff factor, the 
        /// connection timeout and the ack fail limit.
        /// 
        /// The ack timeout (ATO) of the i-th transmission (i >= 0) is given by: ATO(i) = ATO(0) * K^i
        /// where K >= 1 is the ack backoff factor and ATO(0) is the initial ATO derived from the RTT.
        /// The equivalent recursive formulation is: ATO(i) = ATO(i-1) * K, i > 0, K >= 1
        ///
        /// The partial sum for N transmissions is then given by: ATO(0) * (1 + K((K^(N-1))-1)/(K-1)), N > 0
        /// 
        /// Note that the protocol defaults will produce a pretty aggresive retransmission behaviour
        /// with retransmission taking up only 25% more time than the previous attempt.
        /// 
        /// The closer K gets to 0, the more aggressive retransmissions are - i.e. closer in time.
        /// 
        /// Assuming a peer that never replies, the dynamic behaviour produced by the protocol defaults 
        /// should be aproximately as follows:
        ///
        /// Connection timeout (CTO) = 30s
        /// Ack Backoff Factor (K) = 1.25s
        /// Ack Fail Limit (AFL) = 10
        ///
        /// | Initial Ack | Number of transmissions | Total time(s) |
        /// | Timeout(s)  |  (counting the first)   | to disconnect |
        /// |-------------|-------------------------|---------------|
        /// |        0.2  |          10             |       6.651   |
        /// |        0.5  |          10             |      16.626   |                      
        /// |        1.0  |          10             |      30.000   | 
        /// |        2.0  |           7             |      30.000   |
        /// |        4.0  |           5             |      30.000   |
        /// |        8.0  |           3             |      30.000   |
        /// |       16.0  |           2             |      30.000   |
        ///
        ///
        /// Note that for the given parameters when ATO(0) < 1s the limiting factor is AFL and the 
        /// disconnection is going to happen before CTO. After ATO(0) >= 1s the limitation becomes 
        /// the CTO with the number of transmissions that we can fit inside that time window decreasing. 
        /// Once ATO(0) >= CTO/2, only one retransmission is ever possible so the connection becomes 
        /// extremely sensitive to packet loss.
        /// 
        /// ]]>
        public static class Limits
        {
            public static class Connection
            {
                public static class Timeout
                {
                    public const uint Default = 30000;
                }
            }

            public static class Ack
            {
                public static class Timeout
                {
                    public const uint Default = 500;

                    public const uint MinValue = 200;
                    public const uint MaxValue = 60000;

                    public static uint Clamp(uint value) => Math.Max(MinValue, Math.Min(value, MaxValue));

                    public static class Backoff
                    {
                        public const float Default = 1.25f;
                    }
                }

                public static class Fail
                {
                    public const byte Default = 10;
                }
            }

            public static class Idle
            {
                public static class Timeout
                {
                    public const uint Default = 1000;
                }
            }
        }
    
        public static class FastRetransmit
        {
            public const ushort Threshold = 3;
        }

        public static class Bandwidth
        {
            public const uint Default = MaxValue;

            public const uint MinValue = 0;
            public const uint MaxValue = Data.Window.Size * 8000;

            public static uint Clamp(uint value) => Math.Max(MinValue, Math.Min(value, MaxValue));

        }

        /// <summary>
        /// Time-to-Live
        /// </summary>
        public static class TTL
        {
            public const byte Default = 64;

            public const byte MinValue = 1;
            public const byte MaxValue = byte.MaxValue;

            public static byte Clamp(byte value) => Math.Max(MinValue, Math.Min(value, MaxValue));
        }

        /// <summary>
        /// Maximum Transmission Unit
        /// </summary>
        public static class MTU
        {
            public const ushort IPv4 = 576;
            public const ushort IPv6 = 1280;

            /// <summary>
            /// The recommended MTU value.
            /// </summary>
            public const ushort Default = 1280;

            public const ushort MinValue = Protocol.IP.Header.Size + Protocol.UDP.Header.Size 
                                         + Protocol.Packet.Header.Size + sizeof(ushort) 
                                         + sizeof(Protocol.MessageFlags) + Protocol.Message.Fragment.MinSize + Protocol.Fragment.Size.MinValue
                                         + Protocol.Packet.Secure.N64.Size + Protocol.Packet.Secure.Mac.Size;

            public const ushort MaxValue = 65535;

            public static ushort Clamp(ushort value) => Math.Max(MinValue, Math.Min(value, MaxValue));
        }

        /// <summary>
        /// Maximum Transimssion Channel
        /// </summary>
        public static class MTC
        {
            public const byte Default = 0;

            public const byte MinValue = 0;
            public const byte MaxValue = 255;

            public static byte Clamp(byte value) => Math.Max(MinValue, Math.Min(value, MaxValue));
        }

        public static class IP
        {
            public static class Header
            {
                public const ushort Size = 40; // actual IPv4 header size is less than this in practice but this is safe assumption that works for both IPv4 and IPv6 
            }
        }

        public static class UDP
        {
            public static class Header
            {
                public const ushort Size = 8;
            }
        }

        public static class Packet
        {
            /// <summary>
            /// Maximum amount of time in milliseconds a packet is considered relevant.
            /// </summary>
            public const ushort LifeTime = 60000;

            public static class Header
            {
                public const ushort Size = 5; // STM(4) + PFLAGS(1)
            }

            public static class Secure
            {
                public static class Key
                {
                    public const int Size = Carambolas.Security.Cryptography.Key.Size;
                }

                public static class N64
                {
                    public const int Size = sizeof(ulong);
                }

                public static class Mac
                {
                    public const int Size = Carambolas.Security.Cryptography.Mac.Size;
                }
            }

            public static class Insecure
            {
                public static class Checksum
                {
                    public const int Size = Crc32C.Size;

                    internal static Crc32C Compute(byte[] buffer, int offset, int length) => Crc32C.Compute(buffer, offset, length);

                    internal static bool Verify(byte[] buffer, int offset, int length) => Crc32C.Verify(buffer, offset, length);
                }
            }
        }

        internal static class Countdown
        {
            public const int Infinite = -1;
        }

        internal readonly struct Time: IEquatable<Time>, IComparable<Time>
        {
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private readonly uint value;

            public Time(uint value) => this.value = value;

            public bool Equals(Time other) => Equals(this, other);

            public override bool Equals(object obj) => obj is Time other && Equals(this, other);

            public override int GetHashCode() => (int)value;

            public override string ToString() => value.ToString();

            public int CompareTo(Time other) => Compare(this, other);

            public static explicit operator uint(Time a) => a.value;
            public static implicit operator Time(uint value) => new Time(value);

            public static bool operator ==(Time a, Time b) => Equals(a, b);
            public static bool operator !=(Time a, Time b) => !Equals(a, b);

            public static bool operator <(Time a, Time b) => Lower(a, b);
            public static bool operator >(Time a, Time b) => !LowerOrEqual(a, b);

            public static bool operator <=(Time a, Time b) => LowerOrEqual(a, b);
            public static bool operator >=(Time a, Time b) => !Lower(a, b);

            public static Time operator +(Time a, Time b) => new Time(unchecked(a.value + b.value));
            public static Time operator +(Time a, uint value) => new Time(unchecked(a.value + value));
            public static Time operator +(uint value, Time b) => new Time(unchecked(value + b.value));
            public static Time operator -(Time a, Time b) => new Time(unchecked(a.value - b.value));
            public static Time operator -(Time a, uint value) => new Time(unchecked(a.value - value));
            public static Time operator -(uint value, Time b) => new Time(unchecked(value - b.value));

            private static int Compare(Time a, Time b) => a.value == b.value ? 0 : unchecked(b.value - a.value) < (1u << 31) ? -1 : 1;

            private static bool Equals(Time a, Time b) => a.value == b.value;

            private static bool Lower(Time a, Time b) => a.value != b.value && unchecked(b.value - a.value) < (1u << 31);

            private static bool LowerOrEqual(Time a, Time b) => unchecked(b.value - a.value) < (1u << 31);
        }

        /// <summary>
        /// 16-bit ordinal sequence number.
        /// </summary>
        /// <![CDATA[
        /// With a 16-bit sequence number the size of the sliding window must be at most 32768. The sliding window size also determines the maximum number of 
        /// messages that may be in flight which must be equal to Ordinal.Window.Size - 1. The reason for the -1 is that if a full window of 32768 consecutive 
        /// unreliable messages are lost in a row, the sender is forced to move its sliding window forward by the same amount (because there's no hope of any 
        /// acks arriving anymore) while the receiver's slindig window will remain unchanged. This will cause the receiver to misinterpret all further 32768 
        /// messages as old/late and acknowledge them all without delivering any data. The sender will believe these new 32768 messages are being delivered when 
        /// in fact they're being acknowledged and dropped by the receiver. An easy way to address this problem without increasing the sequence number space is 
        /// to rely on the fact that if the sender considers all messages in flight to be lost (they were all unreliable) a ping is injected to find if the 
        /// receiver is still alive. In this case if we reserve the last sequence number of the sliding window for an eventual reliable ping message, there will 
        /// be no crossing over window bounds. The receiver will be able to naturally adjust its sliding window once the ping is received and the sender, on its 
        /// side, can now safely wait for an ack to move its own sliding window instead of having to artifically adjust when messages are lost.
        ///
        /// A problem with sliding windows in selective repeat protocols is how to determine whether an incoming message is an old duplicate from a previous
        /// cycle of the window without having to augment the space of sequence numbers. 
        /// 
        /// Given a receiver that stores the lowest sequence number that can be acknowledged (LSEQ) the next expected sequence number (ESEQ) and the next 
        /// expected source time (ESTM), any incoming message m is:
        ///
        ///      - acceptable if m.STM >= ESTM
        ///      - may be acknowledged if m.SEQ >= LSEQ 
        ///      - may be enqueued for delivery if m.SEQ >= ESEQ
        /// 
        /// Initial states are:
        ///
        ///      ESEQ = 0
        ///      LSEQ = 0
        ///      ESTM = connection time
        /// 
        /// On message m delivered:
        ///
        ///      ESEQ = m.SEQ + 1
        ///      if (ESEQ - Protocol.Ordinal.Window.Size > LSEQ) 
        ///          LSEQ = ESEQ - Protocol.Ordinal.Window.Size;
        /// 
        /// Without loss of generality, imagine a sender that always sends a full window of messages. In this case, after a few seconds the receiver will observe
        /// something like the following pattern:
        ///
        ///         [0]          [1]          [2]          [3]          [4]
        ///      0..32767 | 32768..65535 | 0..32767 | 32768..65535 | 0..32767 | ...
        /// 
        /// It's easy to verify that STM is not monotonically increasing inside each window due to retransmissions. For instance m[0][0] may arrive with an 
        /// STM = t0, m[0][3] and m[0][4] with an STM = t10 and yet m[0][1] may arrive late after being retransmitted with an STM = t50.
        ///
        /// In fact this means that not all STMs from a window[k] are greater than or equal to those of window[k-1] (k > 0). For instance messages m[0][32766]
        /// to m[1][32779] may arrive with STM = t100, m[1][32780] with STM = 110 and m[0][32767] may arrive late after being retransmitted with STM = t150.
        /// In this situation some messages in window[1] have lower STMs than others in window[0].
        /// 
        /// Nevertheless, messages from window[2] are guaranteed to only be transmitted after all messages from window[0] (by a well-behaving sender) and that 
        /// includes retransmissions. In the worst case, assuming m[0][32767] is reliable and continously retransmitted without reaching the receiver, even if 
        /// all further messages in the sliding window are transmitted, the last one transmitted before a sender must stall is going to be m[1][65533] (because 
        /// m[1][65534] would be a ping but it's not transmitted since m[0][32767] is already acting like one - i.e. waiting for an ack). 
        /// 
        /// Therefore:
        /// 
        ///      min { m[k][i].STM } > max { m[k-2][i].STM }, k >= 2
        ///
        /// This means that a message m belongs to window[2] if and only if m.STM is greater than the maximum STM received for window[0]. This is great because
        /// the only requirement now is to compute the max STM of every static window as we go in a rolling buffer of 4 ESTMs like this:
        /// 
        ///      ESTM[-2] = connection time
        ///      ESTM[-1] = connection time
        ///
        ///                                          / m[0][i].STM >= ESTM[-2]
        ///                                          | ESTM[0] = max { m[0][i].STM }
        ///      while ESEQ-1 < 32768               <                                    --> no message from window[1] has been delivered yet
        ///                                          | m[1][i].STM >= ESTM[-1]
        ///                                          \ ESTM[1] = max { m[1][i].STM }
        ///                                         
        ///                                          / m[1][i].STM >= ESTM[-1]
        ///                                          | ESTM[1] = max { m[1][i].STM }
        ///      while ESEQ-1 < 0                   <                                    --> no message from window[2] has been delivered yet 
        ///                                          | m[2][i].STM >= ESTM[0]
        ///                                          \ ESTM[2] = max { m[2][i].STM }
        ///                                         
        ///                                          / m[2][i].STM >= ESTM[0]
        ///                                          | ESTM[2] = max { m[2][i].STM }
        ///      while ESEQ-1 < 32768               <                                    --> no message from window[3] has been delivered yet
        ///                                          | m[3][i].STM >= ESTM[1]
        ///                                          \ ESTM[3] = max { m[3][i].STM }
        ///                                         
        ///                                          / m[3][i].STM >= ESTM[1]
        ///                                          | ESTM[3] = max { m[3][i].STM }
        ///      while ESEQ-1 < 0                   <                                    --> no message from window[4] has been delivered yet 
        ///                                          | m[4][i].STM >= ESTM[2]
        ///                                          \ ESTM[4] = max { m[4][i].STM }
        ///          ...                            
        ///                                          / m[n-2][i].STM >= ESTM[n-4]
        ///                                          | ESTM[n-2] = max { m[n-2][i].STM }
        ///      while ESEQ-1 < 32768 * ((n-1) % 2) <                                    --> no message from window[n] has been delivered yet 
        ///                                          | m[n-1][i].STM >= ESTM[n-3]
        ///                                          \ ESTM[n-1] = max { m[n-1][i].STM }
        ///
        ///      where n > 0 is the number of windows (not to be confused with the index of the last window)
        ///
        /// Note that the actual implementation has to adjust ESTM indexes because we can't have negative array indexes in C# and even if we could the semantics 
        /// would probably be different than the one we imply here (e.g. like in python) So in practice we have to calculate window index j and offset the ESTM 
        /// index by 2 (simply assuming k = j). This means that for window 0 we test against ESTM[0] while updating ESTM[2], for window 1 we test agains ESTM[1] 
        /// while updating ESTM[3], for window 2 we test against ESTM[2] and update ESTM[0], etc...
        ///
        ///     ESTM[0] = ESTM[1] = ESTM[2] = ESTM[3] = latest source time on connected
        ///     XSEQ = 0
        ///     j = 0
        ///
        /// On ESEQ changed:
        ///     
        ///     XSEQ = (XSEQ + new ESEQ - old ESEQ) % (4 * Protocol.Ordinal.Window.Size)
        ///     j = XSEQ / 32768
        /// 
        /// 
        /// Finally, a misbehaving sender that transmits messages without observing the sequence window limits would still need two full windows - i.e. the 
        /// whole sequence number space - of messages to arrive later than some message m[k][i] for its SEQ to overlap with some window[k-2] message m[k-2][i] 
        /// and possibly replace it in error.
        ///
        /// A misbehaving sender that does not honour the monotonic increasing modulo 2^16 requirement for sequence numbers is out of scope.
        ///
        /// ]]>
        internal readonly struct Ordinal: IEquatable<Ordinal>, IComparable<Ordinal>
        {
            public static class Window
            {
                public const ushort Size = 32768;

                /// <summary>
                /// Fixed set of static window times. Refer to <see cref="Protocol.Ordinal"/> for more information.
                /// </summary>
                internal struct Times
                {
                    public const uint MaxAdjustmentTime = 24 * 24 * 3600 * 1000; // = 24 days in milliseconds

                    public const int Size = 4;
                    
                    private Time a0;
                    private Time a1;
                    private Time a2;
                    private Time a3;

                    public Time this[int index]
                    {
                        get
                        {
                            switch (index)
                            {
                                case 0:
                                    return a0;
                                case 1:
                                    return a1;
                                case 2:
                                    return a2;
                                case 3:
                                    return a3;
                                default:
                                    throw new ArgumentOutOfRangeException(nameof(index));
                            }
                        }

                        set
                        {
                            switch (index)
                            {
                                case 0:
                                    a0 = (uint)value;
                                    break;
                                case 1:
                                    a1 = (uint)value;
                                    break;
                                case 2:
                                    a2 = (uint)value;
                                    break;
                                case 3:
                                    a3 = (uint)value;
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException(nameof(index));
                            }
                        }
                    }

                    public Times(Protocol.Time time) => a0 = a1 = a2 = a3 = time;

                    /// <summary>
                    /// If an ordinal window time is less than <paramref name="time"/> set it to <paramref name="time"/>.
                    /// </summary>
                    public void Adjust(Protocol.Time time)
                    {
                        if (a0 < time)
                            a0 = time;
                        if (a1 < time)
                            a1 = time;
                        if (a2 < time)
                            a2 = time;
                        if (a3 < time)
                            a3 = time;
                    }
                }
            }

            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private readonly ushort value;

            public Ordinal(ushort value) => this.value = value;

            public bool Equals(Ordinal other) => Equals(this, other);

            public override bool Equals(object obj) => obj is Ordinal other && Equals(this, other);

            public override int GetHashCode() => (int)value;

            public override string ToString() => value.ToString();

            public int CompareTo(Ordinal other) => Compare(this, other);

            public static explicit operator ushort(Ordinal a) => a.value;

            public static bool operator ==(Ordinal a, Ordinal b) => Equals(a, b);
            public static bool operator !=(Ordinal a, Ordinal b) => !Equals(a, b);

            public static bool operator <(Ordinal a, Ordinal b) => Lower(a, b);
            public static bool operator >(Ordinal a, Ordinal b) => !LowerOrEqual(a, b);

            public static bool operator <=(Ordinal a, Ordinal b) => LowerOrEqual(a, b);
            public static bool operator >=(Ordinal a, Ordinal b) => !Lower(a, b);

            public static Ordinal operator +(Ordinal a, Ordinal b) => new Ordinal((ushort)unchecked(a.value + b.value));
            public static Ordinal operator +(Ordinal a, ushort value) => new Ordinal((ushort)unchecked(a.value + value));
            public static Ordinal operator +(ushort value, Ordinal b) => new Ordinal((ushort)unchecked(value + b.value));
            public static Ordinal operator -(Ordinal a, Ordinal b) => new Ordinal((ushort)unchecked(a.value - b.value));
            public static Ordinal operator -(Ordinal a, ushort value) => new Ordinal((ushort)unchecked(a.value - value));
            public static Ordinal operator -(ushort value, Ordinal b) => new Ordinal((ushort)unchecked(value - b.value));

            public static Ordinal operator ++(Ordinal a) => new Ordinal((ushort)unchecked(a.value + 1));

            private static int Compare(Ordinal a, Ordinal b) => a.value == b.value ? 0 : (ushort)unchecked(b.value - a.value) < Window.Size ? -1 : 1;

            private static bool Equals(Ordinal a, Ordinal b) => a.value == b.value;

            private static bool Lower(Ordinal a, Ordinal b) => a.value != b.value && (ushort)unchecked(b.value - a.value) < Window.Size;

            private static bool LowerOrEqual(Ordinal a, Ordinal b) => (ushort)unchecked(b.value - a.value) < Window.Size;
        }

        internal static class Data
        {
            public static class Window
            {
                public const ushort Size = 65535;
            }
        }

        internal static class Segment
        {
            public static class Size
            {
                public const ushort MinValue = 1;

                public static ushort MaxValue(ushort mtu, bool secure)
                    => (ushort)(mtu - ((IP.Header.Size + UDP.Header.Size + Packet.Header.Size)
                        + (secure ? (Packet.Secure.N64.Size + sizeof(ushort) + sizeof(MessageFlags) + Message.Segment.MinSize + Packet.Secure.Mac.Size)     // RW(2) MFLAGS(1) {SEQ(2) RSN(2) LEN(2)} NONCE(8) MAC(16)
                                   : (sizeof(uint) + sizeof(ushort) + sizeof(MessageFlags) + Message.Segment.MinSize + Packet.Insecure.Checksum.Size))));   // SSN(4) RW(2) MFLAGS(1) SEQ(2) RSN(2) LEN(2) CRC(4)
            }
        }

        internal static class Fragment
        {
            public static class Size
            {
                public const ushort MinValue = 256;

                public static ushort MaxValue(ushort mtu, bool secure)
                    => (ushort)(mtu - ((IP.Header.Size + UDP.Header.Size + Packet.Header.Size)
                        + (secure ? (Packet.Secure.N64.Size + sizeof(ushort) + sizeof(MessageFlags) + Message.Fragment.MinSize + Packet.Secure.Mac.Size)    // RW(2) MFLAGS(1) {SEQ(2) RSN(2) SEGLEN(2) FRGIDX(1) LEN(2)} NONCE(8) MAC(16)
                                   : (sizeof(uint) + sizeof(ushort) + sizeof(MessageFlags) + Message.Fragment.MinSize + Packet.Insecure.Checksum.Size))));  // SSN(4) RW(2) MFLAGS(1) SEQ(2) RSN(2) SEGLEN(2) FRGIDX(1) LEN(2) CRC(4)
            }
        }
       
        internal enum State
        {
            /// <summary>
            /// No packets can be sent or received.
            /// </summary>
            Disconnected = 0,

            /// <summary>
            /// Actively requesting a connection to a remote host. No data packets can be sent or received yet.
            /// </summary>
            Connecting,

            /// <summary>
            /// Waiting for a confirmation from the remote host that a connection has been established. No data packets can be sent or received yet.
            /// </summary>
            Accepting,

            /// <summary>
            /// Data packets can be sent and received.
            /// </summary>
            Connected
        }

        [Flags]
        internal enum PacketFlags: byte
        {
            None = 0x00,
            Accept = 0x0A,
            Connect = 0x0C,
            Data = 0x0D,
            Reset = 0x0F,

            Secure = 0x10
        }

        [Flags]
        internal enum MessageFlags: byte
        {
            None = 0x00,
            // Control flags (i.e {Accept} => accept message, {Ack | Accept} => accept ack, etc...)
            Accept = 0x0A,

            // Ack flags (i.e. {Data | Ack} => data ack, {Data | Ack | Gap} => data ack with gap info, {Data | Ack | Dup} => data ack with duplicate info
            Dup = 0x10,
            Gap = 0x40,
            Ack = 0x80,

            // Data flags ( i.e {Data | Segment} => unreliable segment, {Reliable | Data | Segment} => reliable segment, {Data | Fragment} => unreliable fragment etc...)
            Segment = 0x00,
            Fragment = 0x10,
            Data = 0x20,
            Reliable = 0x40
        }

        internal static class Message
        {
            [StructLayout(LayoutKind.Auto)]
            internal readonly ref struct Connect
            {
                public const int Size = 7; // MTU(2), MTC(1), MTB(4)

                public readonly ushort MaximumTransmissionUnit;

                public readonly byte MaximumTransmissionChannel;

                public readonly uint MaximumBandwidth;

                public Connect(ushort mtu, byte mtc, uint mbw)
                {
                    MaximumTransmissionUnit = mtu;
                    MaximumTransmissionChannel = mtc;
                    MaximumBandwidth = mbw;
                }
            }

            [StructLayout(LayoutKind.Auto)]
            internal readonly ref struct Accept
            {
                public const int Size = 11; // MTU(2), MTC(1), MBW(4), ATM(4)

                public static class Ack
                {
                    public const int Size = 4; // ATM
                }

                public readonly ushort MaximumTransmissionUnit;

                public readonly byte MaximumTransmissionChannel;

                public readonly uint MaximumBandwidth;

                public readonly uint AcknowledgedTime;

                public Accept(ushort mtu, byte mtc, uint mbw, uint atm)
                {
                    MaximumTransmissionUnit = mtu;
                    MaximumTransmissionChannel = mtc;
                    MaximumBandwidth = mbw;
                    AcknowledgedTime = atm;
                }
            }

            [StructLayout(LayoutKind.Auto)]
            internal readonly ref struct Ack
            {
                /// <summary>
                /// Size of the message parameters not counting flags and channel.
                /// </summary>
                public const int Size = 7; // CH(1), NEXT(2), ATM(4)

                internal static class Dup
                {
                    /// <summary>
                    /// Size of the message parameters not counting flags and channel.
                    /// </summary>
                    public const int Size = 9;  // CH(1), CNT(2), NEXT(2), ATM(4)
                }

                internal static class Gap
                {
                    /// <summary>
                    /// Size of the message parameters not counting flags and channel.
                    /// </summary>
                    public const int Size = 9; // CH(1), NEXT(2), LAST(2), ATM(4)

                    internal static class Dup
                    {
                        /// <summary>
                        /// Size of the message parameters not counting flags and channel.
                        /// </summary>
                        public const int Size = 11;  // CH(1), CNT(2), NEXT(2), LAST(2), ATM(4)
                    }

                }

                public readonly byte Channel;

                /// <summary>
                /// Number of acknowledgements this ack is worth. Value must be > 0.
                /// This is equivalent to the number of packets received so far that contained one or more messages arriving ahead of the next expected.
                /// It's a cheaper alternative than sending repeated acks in order to trigger fast retransmissions. 
                /// </summary>
                public readonly ushort Count;

                /// <summary>
                /// Next expected sequence number.
                /// </summary>
                public readonly Ordinal Next;

                /// <summary>
                /// First sequence number known after <see cref="Next"/> so that (<see cref="Last"/> - <see cref="Next"/>) 
                /// is the size of the gap in the receive buffer.
                /// </summary>
                public readonly Ordinal Last;

                /// <summary>
                /// Latest source time of all messages received on the channel since last ack/gap.
                /// Doesn't necessariyl correspond to the source time of <see cref="Next"/> when <see cref="Next"/> != <see cref="Last"/> 
                /// but this doesn't affect the roundtrip time estimation.
                /// </summary>
                public readonly Time AcknowledgedTime;

                public Ack(byte channel, Ordinal next, Time atm) : this(channel, 1, next, next, atm) { }
                public Ack(byte channel, Ordinal next, Ordinal last, Time atm) : this(channel, 1, next, last, atm) { }

                public Ack(byte channel, ushort count, Ordinal next, Time atm) : this(channel, count, next, next, atm) { }                
                public Ack(byte channel, ushort count, Ordinal next, Ordinal last, Time atm) => (Channel, Count, Next, Last, AcknowledgedTime) = (channel, count, next, last, atm);
            }
          
            /// <summary>
            /// Data message containing a complete datagram.
            /// </summary>
            [StructLayout(LayoutKind.Auto)]
            internal readonly ref struct Segment
            {
                /// <summary>
                /// Size of the message parameters not counting flags and channel and with no data.
                /// </summary>
                public const int MinSize = 7; // CH(1), SEQ(2), RSN(2), SEGLEN(2)

                public readonly byte Channel;

                public readonly Ordinal SequenceNumber;

                public readonly Ordinal ReliableSequenceNumber;

                public readonly Pointer Data;

                public Segment(byte channel, Ordinal seq, Ordinal rsn, in Pointer data)
                {
                    Channel = channel;

                    SequenceNumber = seq;
                    ReliableSequenceNumber = rsn;
                    Data = data;
                }
            }

            /// <summary>
            /// Data message contaning a piece of a datagram.
            /// </summary>
            [StructLayout(LayoutKind.Auto)]
            internal readonly ref struct Fragment
            {
                /// <summary>
                /// Size of the message parameters not counting flags and channel and with no data.
                /// </summary>
                public const int MinSize = 10; // CH(1), SEQ(2), RSN(2), SEGLEN(2), FRAGINDEX(1) FRAGLEN(2)

                public readonly byte Channel;

                public readonly Ordinal SequenceNumber;

                /// <summary>
                /// Reliable sequence number
                /// </summary>
                public readonly Ordinal ReliableSequenceNumber;

                /// <summary>
                /// 0-based index of the fragment in the datagram.
                /// </summary>
                public readonly byte Index;

                /// <summary>
                /// Last fragment index. Calculated after <see cref="Index"/> and <see cref="DatagramLength"/>
                /// </summary>
                public readonly byte Last;

                /// <summary>
                /// Complete datagram length.
                /// </summary>
                public readonly ushort DatagramLength;

                public readonly Pointer Data;

                public Fragment(byte channel, Ordinal seq, Ordinal rsn, byte index, byte last, ushort datagramLength, in Pointer data)
                {
                    Channel = channel;

                    SequenceNumber = seq;
                    ReliableSequenceNumber = rsn;
                    Index = index;
                    Last = last;
                    DatagramLength = datagramLength;
                    Data = data;
                }
            }
        }    
    }

    internal static class PacketFlagsExtensions { public static bool Contains(this Protocol.PacketFlags e, Protocol.PacketFlags flags) => (e & flags) == flags; }

    internal static class MessageFlagsExtensions { public static bool Contains(this Protocol.MessageFlags e, Protocol.MessageFlags flags) => (e & flags) == flags; }

    internal static class BinaryWriterExtensions
    {
        public static bool TryWrite(this BinaryWriter writer, in Protocol.Message.Ack ack)
        {
            if (ack.Next == ack.Last)
            {
                if (ack.Count > 1)
                {
                    if (writer.Available < Protocol.Message.Ack.Dup.Size + 1)
                        return false;

                    writer.UncheckedWrite(Protocol.MessageFlags.Ack | Protocol.MessageFlags.Data | Protocol.MessageFlags.Dup);
                    writer.UncheckedWrite(ack.Channel);
                    writer.UncheckedWrite(ack.Count);
                }
                else
                {
                    if (writer.Available < Protocol.Message.Ack.Size + 1)
                        return false;

                    writer.UncheckedWrite(Protocol.MessageFlags.Ack | Protocol.MessageFlags.Data);
                    writer.UncheckedWrite(ack.Channel);
                }

                writer.UncheckedWrite(ack.Next);
                writer.UncheckedWrite(ack.AcknowledgedTime);
            }
            else
            {
                if (ack.Count > 1)
                {
                    if (writer.Available < Protocol.Message.Ack.Gap.Dup.Size + 1)
                        return false;

                    writer.UncheckedWrite(Protocol.MessageFlags.Ack | Protocol.MessageFlags.Gap | Protocol.MessageFlags.Data | Protocol.MessageFlags.Dup);
                    writer.UncheckedWrite(ack.Channel);
                    writer.UncheckedWrite(ack.Count);
                }
                else
                {
                    if (writer.Available < Protocol.Message.Ack.Gap.Size + 1)
                        return false;

                    writer.UncheckedWrite(Protocol.MessageFlags.Ack | Protocol.MessageFlags.Gap | Protocol.MessageFlags.Data);
                    writer.UncheckedWrite(ack.Channel);
                }

                writer.UncheckedWrite(ack.Next);
                writer.UncheckedWrite(ack.Last);
                writer.UncheckedWrite(ack.AcknowledgedTime);
            }

            return true;
        }

        public static void Write(this BinaryWriter writer, Protocol.PacketFlags value) => writer.Write((byte)value);

        public static void Write(this BinaryWriter writer, Protocol.MessageFlags value) => writer.Write((byte)value);

        public static void Write(this BinaryWriter writer, Protocol.Time value) => writer.Write((uint)value);

        public static void Write(this BinaryWriter writer, Protocol.Ordinal value) => writer.Write((ushort)value);

        public static void Write(this BinaryWriter writer, in Key value)
        {
            writer.Ensure(Key.Size);
            writer.UncheckedWrite(in value);
        }

        public static void Write(this BinaryWriter writer, in Mac value)
        {
            writer.Ensure(Mac.Size);
            writer.UncheckedWrite(in value);
        }

        public static void Write(this BinaryWriter writer, Crc32C value)
        {
            writer.Ensure(Crc32C.Size);
            writer.UncheckedWrite(value);
        }


        public static void UncheckedWrite(this BinaryWriter writer, Protocol.PacketFlags value) => writer.UncheckedWrite((byte)value);

        public static void UncheckedWrite(this BinaryWriter writer, Protocol.MessageFlags value) => writer.UncheckedWrite((byte)value);

        public static void UncheckedWrite(this BinaryWriter writer, Protocol.Time value) => writer.UncheckedWrite((uint)value);

        public static void UncheckedWrite(this BinaryWriter writer, Protocol.Ordinal value) => writer.UncheckedWrite((ushort)value);

        public static void UncheckedWrite(this BinaryWriter writer, in Key value)
        {
            value.CopyTo(writer.Buffer, writer.Position);
            writer.UncheckedSkip(Key.Size);
        }

        public static void UncheckedWrite(this BinaryWriter writer, in Mac value)
        {
            value.CopyTo(writer.Buffer, writer.Position);
            writer.UncheckedSkip(Mac.Size);
        }

        public static void UncheckedWrite(this BinaryWriter writer, Crc32C value)
        {
            value.CopyTo(writer.Buffer, writer.Position);
            writer.UncheckedSkip(Crc32C.Size);
        }

        public static void UncheckedOverwrite(this BinaryWriter writer, Protocol.Ordinal value, int index) => writer.UncheckedOverwrite((ushort)value, index);
    }

    internal static class BinaryReaderExtensions
    {
        public static void Read(this BinaryReader reader, out Protocol.PacketFlags value)
        {
            reader.Read(out byte opcode);
            value = (Protocol.PacketFlags)opcode;
        }

        public static void Read(this BinaryReader reader, out Protocol.MessageFlags value)
        {
            reader.Read(out byte opcode);
            value = (Protocol.MessageFlags)opcode;
        }

        public static void Read(this BinaryReader reader, out Protocol.Time value)
        {
            reader.Read(out uint time);
            value = new Protocol.Time(time);
        }

        public static void Read(this BinaryReader reader, out Protocol.Ordinal value)
        {
            reader.Read(out ushort ordinal);
            value = new Protocol.Ordinal(ordinal);
        }

        public static void Read(this BinaryReader reader, out Key value)
        {
            reader.Ensure(Key.Size);            
            reader.UncheckedRead(out value);
        }

        public static void Read(this BinaryReader reader, out Mac value)
        {
            reader.Ensure(Mac.Size);
            reader.UncheckedRead(out value);
        }


        public static void UncheckedRead(this BinaryReader reader, out Protocol.PacketFlags value)
        {
            reader.UncheckedRead(out byte opcode);
            value = (Protocol.PacketFlags)opcode;
        }

        public static void UncheckedRead(this BinaryReader reader, out Protocol.MessageFlags value)
        {
            reader.UncheckedRead(out byte opcode);
            value = (Protocol.MessageFlags)opcode;
        }

        public static void UncheckedRead(this BinaryReader reader, out Protocol.Time value)
        {
            reader.UncheckedRead(out uint time);
            value = new Protocol.Time(time);
        }

        public static void UncheckedRead(this BinaryReader reader, out Protocol.Ordinal value)
        {
            reader.UncheckedRead(out ushort ordinal);
            value = new Protocol.Ordinal(ordinal);
        }

        public static void UncheckedRead(this BinaryReader reader, out Key value)
        {
            value = new Key(reader.Buffer, reader.Position);
            reader.UncheckedSkip(Key.Size);
        }

        public static void UncheckedRead(this BinaryReader reader, out Mac value)
        {
            value = new Mac(reader.Buffer, reader.Position);
            reader.UncheckedSkip(Mac.Size);
        }


        public static bool TryRead(this BinaryReader reader, out Protocol.PacketFlags value)
        {
            if (!reader.TryRead(out byte opcode))
            {
                value = default;
                return false;
            }

            value = (Protocol.PacketFlags)opcode;
            return true;
        }

        public static bool TryRead(this BinaryReader reader, out Protocol.MessageFlags value)
        {
            if (!reader.TryRead(out byte opcode))
            {
                value = default;
                return false;
            }

            value = (Protocol.MessageFlags)opcode;
            return true;
        }

        public static bool TryRead(this BinaryReader reader, out Protocol.Time value)
        {
            if (!reader.TryRead(out uint time))
            {
                value = default;
                return false;
            }

            value = new Protocol.Time(time);
            return true;
        }

        public static bool TryRead(this BinaryReader reader, out Protocol.Ordinal value)
        {
            if (!reader.TryRead(out ushort ordinal))
            {
                value = default;
                return false;
            }

            value = new Protocol.Ordinal(ordinal);
            return true;
        }

        public static bool TryRead(this BinaryReader reader, out Key value)
        {
            if (reader.Available < Key.Size)
            {
                value = default;
                return false;
            }

            reader.UncheckedRead(out value);
            return true;
        }

        public static bool TryRead(this BinaryReader reader, out Mac value)
        {
            if (reader.Available < Mac.Size)
            {
                value = default;
                return false;
            }

            reader.UncheckedRead(out value);
            return true;
        }        
    }

    internal static class MemoryExtensions
    {
        public static void UncheckedOverwrite(this Memory memory, Protocol.Ordinal value, int index) => memory.UncheckedOverwrite((ushort)value, index);
    }
}
