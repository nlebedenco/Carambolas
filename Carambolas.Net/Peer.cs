using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

using Carambolas.Security.Cryptography;

namespace Carambolas.Net
{
    [Flags]
    internal enum Command
    {
        None = 0,
        Transmit = 1,
        Connect = 2,
        Accept = 4,
    }

    internal static class CommandExtensions { public static bool Contains(this Command e, Command flags) => (e & flags) == flags; }

    internal enum Acknowledgment
    {
        None = 0,
        Accept
    }

    public class Peer
    {
        internal Peer(Host host, in IPEndPoint endPoint, PeerMode mode)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
            EndPoint = endPoint;
            State = PeerState.Connecting;
            Mode = mode;
        }

        internal Peer(Host host, in IPEndPoint endPoint, PeerMode mode, SessionOptions options, in Key remoteKey)
            : this(host, in endPoint, mode)
        {            
            Session.Options = options;
            if (options.Contains(SessionOptions.Secure))
            {
                Secure = true;
                Session.RemoteKey = remoteKey;
                Session.Cipher = Host.CipherFactory.Create();
                if (options.Contains(SessionOptions.ValidateRemoteKey))
                    Session.Cipher.Key = Host.Keychain.CreateSharedKey(in Host.Keys.Private, in remoteKey);
            }            
        }

        internal volatile Peer Next;
        internal Peer Prev;

        public readonly Host Host;
        public readonly IPEndPoint EndPoint;

        /// <summary>
        /// General purpose reference to a user object.
        /// </summary>
        public object Tag;

        internal Session Session;

        public PeerMode Mode { get; internal set; }
        public PeerState State { get; internal set; }
       
        public readonly bool Secure;
                
        internal bool Terminated { get; private set; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int maxBacklog;

        /// <summary>
        /// Maximum amount of user data in bytes that may be buffered for transmission across all channels.
        /// </summary>
        public int MaxTransmissionBacklog
        {
            get => maxBacklog;
            set => maxBacklog = Math.Max(0, value);
        }

        private ushort maxTransmissionUnit;
        public ushort MaxTransmissionUnit
        {
            get => maxTransmissionUnit;

            internal set
            {
                maxTransmissionUnit = value;
                MaxSegmentSize = Protocol.Segment.Size.MaxValue(value, Secure);
                MaxFragmentSize = Protocol.Fragment.Size.MaxValue(value, Secure);

                Debug.Assert(MaxFragmentSize >= Protocol.Fragment.Size.MinValue, $"Maximum fragment size must be greater than or equal to {Protocol.Fragment.Size.MinValue} bytes.");
                Debug.Assert(MaxFragmentSize < MaxSegmentSize, "Maximum fragment must be less than maximum segment size.");
            }
        }

        public ushort MaxSegmentSize { get; private set; }
        public ushort MaxFragmentSize { get; private set; }

        private Channel[] channels;

        public byte MaxChannel { get; private set; }

        public override string ToString() => $"{EndPoint}:{Session}";

        #region Events  

        private SpinLock eventsLock = new SpinLock(false);
        private Queue<Event> events = new Queue<Event>();

        /// <summary>
        /// Retrieve an event from the queue. 
        /// There's an implicit guarantee that for every occurance of a peer 
        /// in the host event queue there's going to be an event in that peer's event queue.
        /// Therefore, this method does not check if the queue is empty and it should only be called
        /// once on a peer that is retrieved from the host's event queue. 
        /// </summary>
        internal void Dequeue(out Event e)
        {
            var locked = false;
            try
            {
                eventsLock.Enter(ref locked);

                Debug.Assert(events.Count > 0, "Peer must have one or more events in the queue.");
                e = events.Dequeue();
            }
            finally
            {
                if (locked)
                    eventsLock.Exit(false);
            }
        }

        /// <summary>
        /// Add an event to the queue. Must only be used by the host to enforce the
        /// guarantee that for every occurance of a peer in the host event queue 
        /// there's going to be an event in that peer's event queue.
        /// </summary>
        internal void Enqueue(in Event e)
        {
            var locked = false;
            try
            {
                eventsLock.Enter(ref locked);
                events.Enqueue(e);
            }
            finally
            {
                if (locked)
                    eventsLock.Exit(false);
            }
        }

        #endregion

        #region Flow Control

        /// <summary>
        /// 2-way communication delay estimated in milliseconds. 
        /// <para/>
        /// Minimum valid value is 1. A value of 0 is used to represent an infinite time.
        /// </summary>
        public uint RoundTripTime { get; private set; }

        private uint roundTripTimeVariance;

        /// <summary>
        /// Latest remote time received
        /// </summary>
        internal Protocol.Time LatestRemoteTime;

        /// <summary>
        /// Maximum number of bytes that the remote host can buffer. 
        /// Updated in every packet. Note that this property may be adjusted by the 
        /// remote host after data has already been sent thus becoming temporarily 
        /// lower than <see cref="BytesInFlight"/>.
        /// </summary>
        public ushort RemoteWindow { get; internal set; }

        /// <summary>
        /// Maximum receive bandwidth of the remote host in bytes per second.
        /// <para/>
        /// This value is indicated by the remote host in the connection handshake.
        /// </summary>
        public uint RemoteBandwidth { get; internal set; }

        /// <summary>
        /// Conservative estimate of link capacity beyond which there is a higher chance of congestion.
        /// The <see cref="CongestionWindow"/> grows exponentially with each acknowledgement up to this 
        /// point (slow start) and then turns to a linear growth (congestion avoidance).
        /// </summary>
        public ushort LinkCapacity { get; private set; } = ushort.MaxValue;

        /// <summary>
        /// Maximum number of bytes that the bottleneck flow can handle. 
        /// <para/>
        /// Note that this property may be adjusted by a change in the link conditions after data has already 
        /// been sent thus becoming temporarily lower than <see cref="BytesInFlight"/>.
        /// <para/>
        /// Minimum value is equal to <see cref="MaxTransmissionUnit"/>.
        /// </summary>
        public ushort CongestionWindow { get; private set; }

        /// <summary>
        /// Initial value for the <see cref="CongestionWindow"/>. 
        /// This property shouldn't return a value lower than (<see cref="Protocol.FastRetransmit.Threshold"/> + 1) * <see cref="MaxSegmentSize"/>
        /// so 
        /// </summary>
        internal ushort InitialCongestionWindow => (ushort) (MaxSegmentSize << 2);

        /// <summary>
        /// Maximum number of bytes that can be transmitted to remain below the <see cref="RemoteBandwidth"/>
        /// </summary>
        public ushort BandwidthWindow { get; private set; }

        /// <summary>
        /// The maximum number of data bytes that can be transmitted in a single burst.
        /// <para/>
        /// (<see cref="SendWindow"/> - <see cref="BytesInFlight"/>) is the maximum number of data bytes that can be transmisted in a frame. 
        /// Note that in certain circunstances this property may become negative.
        /// </summary>
        public int SendWindow { get; private set; }

        /// <summary>
        /// Receive window advertised to the remote host.
        /// </summary>
        public ushort ReceiveWindow => (ushort)Math.Min(ushort.MaxValue, Host.Downstream.BufferShare);

        /// <summary>
        /// Number of bytes sent but not yet acknowledged.
        /// </summary>
        public uint BytesInFlight { get; private set; }  // must be a uint because an eventual ping must consume 1 "virtual" and the previous 32767 unreliable messages may have taken up 65535 bytes already.

        /// <summary>
        /// Number of bytes in the transmit queue (across all channels).
        /// </summary>
        public int TransmissionBacklog => transmissionBacklog;
        private int transmissionBacklog;

        /// <summary>
        /// Current number of consecutive ack timeouts.
        /// </summary>
        private byte ackFailCounter;

        private uint ackTimeout = Protocol.Limits.Ack.Timeout.Default;
        private uint connTimeout => Host.ConnectionTimeout;

        private Protocol.Time? ackDeadline;
        private Protocol.Time? connDeadline;
        private Protocol.Time? idleDeadline;

        /// <summary>
        /// Number of packets that may be sent to the remote host containing user data.
        /// This is used to limit the number of data packets allowed right after an ack timeout.
        /// </summary>
        private int capacity = Protocol.Countdown.Infinite;

        /// <summary>
        /// Number of channels actively retransmitting
        /// </summary>
        private byte retransmittingChannelsCount;

        #endregion

        #region Statistics

        public DateTime StartTime { get; internal set; }
        public DateTime StopTime { get; internal set; }

        internal long packetsSent;
        public long PacketsSent => Interlocked.Read(ref packetsSent);

        internal long packetsReceived;
        public long PacketsReceived => Interlocked.Read(ref packetsReceived);

        internal long packetsDropped;
        public long PacketsDropped => Interlocked.Read(ref packetsDropped);

        internal long bytesSent;
        public long BytesSent => Interlocked.Read(ref bytesSent);

        internal long bytesReceived;
        public long BytesReceived => Interlocked.Read(ref bytesReceived);

        internal long dataSent;
        public long DataSent => Interlocked.Read(ref dataSent);

        internal long dataReceived;
        public long DataReceived => Interlocked.Read(ref dataReceived);

        internal long fastRetransmissions;
        public long FastRetransmissions => Interlocked.Read(ref fastRetransmissions);

        internal long timeouts;
        public long Timeouts => Interlocked.Read(ref timeouts);

        /// <summary>
        /// Estimated rate of packet loss. This is an aproximation because the only way to determine 
        /// the exact packet loss is to inspect the network from both endpoints simulateneously.
        /// </summary>
        public double Loss => ((double)FastRetransmissions + (double)Timeouts) / PacketsSent;

        #endregion

        private (Command CMD, Protocol.Time AcceptanceTime, Acknowledgment ACK, Protocol.Time AcknowledgedTime) control;

        internal void Connect()
        {
            if (control.CMD != Command.Connect)
                control.CMD = Command.Transmit | Command.Connect;
        }

        internal void Accept(Protocol.Time time)
        {
            if (control.CMD != Command.Accept)
            {
                control.CMD = Command.Transmit | Command.Accept;
                control.AcceptanceTime = time;
            }
        }

        internal void Acknowledge(Acknowledgment value, Protocol.Time acknowledgedTime)
        {
            if (control.ACK != value)
            {
                control.ACK = value;
                control.AcknowledgedTime = acknowledgedTime;
            }
        }

        private void OnAckReceived(Protocol.Time time, Protocol.Time acknowledgedTime)
        {
            var rtt = (uint)(time - acknowledgedTime);

            // Update roundtrip time following Jacobson/Karels's algorithm
            if (RoundTripTime == 0)
            {
                RoundTripTime = Math.Max(1, rtt);
                roundTripTimeVariance = rtt >> 1;
            }
            else
            {
                RoundTripTime = Math.Max(1, (7 * RoundTripTime + rtt) >> 3);
                roundTripTimeVariance = (3 * roundTripTimeVariance + (uint)Math.Abs(RoundTripTime - (long)rtt)) >> 2;
            }

            BandwidthWindow = (ushort)Math.Min(ushort.MaxValue, (long)RemoteBandwidth * RoundTripTime / 1000);

            ackTimeout = Host.AckTimeoutClamp(RoundTripTime + (roundTripTimeVariance << 2));
        }

        private void OnAckMatched(Protocol.Time time, Protocol.Time acknowledgedTime)
        {
            OnAckReceived(time, acknowledgedTime);

            ackFailCounter = 0;
            ackDeadline = default;
            connDeadline = default;
            idleDeadline = time + Host.IdleTimeout;

            capacity = Protocol.Countdown.Infinite;
        }

        private void OnAckTimeout(Protocol.Time time)
        {
            if (ackFailCounter == 1) // First ack timeout
            {
                // If not retransmitting, reduce the estimated link capacity
                if (retransmittingChannelsCount == 0)
                    LinkCapacity = (ushort)Math.Max(CongestionWindow >> 1, MaxSegmentSize << 2);

                // Reset the congestion window for a new slow start, 
                // this value should never be lower than one MaxSegmentSize
                CongestionWindow = MaxSegmentSize;
            }
            else if (ackFailCounter == 2) // Second consecutive ack timeout
            {
                // Reset RTT estimate as it's probably too wrong now
                RoundTripTime = 0;
                BandwidthWindow = 0;
            }

            // If there's a control command waiting for an ack, flag it for retransmission
            // otherwise set every channel to retransmit. 
            if (control.CMD != default)
                control.CMD |= Command.Transmit;
            else 
            {
                // If a channel has data do retransmit it will increment the counter.
                for (int i = 0; i < channels.Length; ++i)
                    channels[i].TX.Timeout(ref retransmittingChannelsCount);

                // This could be a congestion so restrict sending to at most 1 data packet 
                // (retransmission or not) until either ack is received or another timeout occurs.
                capacity = 1;
            }            

            // Backoff the ack timeout 
            ackTimeout = Host.AckTimeoutClamp(Host.AckTimeoutBackoff(ackTimeout));
            // Stop the ack timer. It's going be restarted when a packet is (re)transmitted.
            ackDeadline = default;
            // Stop the idle timer. An empty reliable segment (ping) is going to be issued if there's nothing to retransmit.
            idleDeadline = default;
        }


        internal void OnConnecting(Protocol.Time time)
        {
            Session.State = Protocol.State.Connecting;            
            Session.Local = (uint)time;

            // Don't assign MaxChannel here yet as channels should only be 
            // allocated after the connection is established to avoid unecessary allocations.

            CongestionWindow = (ushort)(MaxSegmentSize << 2);
            RemoteWindow = ushort.MaxValue;

            StartTime = DateTime.Now;

            Connect();
        }

        internal void OnAccepting(Protocol.Time time, Protocol.Time remoteTime, uint remoteSession, in Protocol.Message.Connect connect)
        {
            Session.State = Protocol.State.Accepting;
            Session.Local = (uint)time;
            Session.Remote = remoteSession;

            var mtc = connect.MaximumTransmissionChannel;

            MaxChannel = mtc;
            channels = new Channel[mtc + 1];
            for (int i = 0; i <= mtc; ++i)
                channels[i].Initialize(remoteTime);

            RemoteBandwidth = connect.MaximumBandwidth >> 3;
            CongestionWindow = (ushort)(MaxSegmentSize << 2);

            StartTime = DateTime.Now;

            Accept(LatestRemoteTime);
        }


        internal void OnCrossConnecting(Protocol.Time time, Protocol.Time remoteTime, uint remoteSession, in Protocol.Message.Connect connect, in Key remoteKey)
        {
            Session.RemoteKey = remoteKey;
            Session.Cipher.Key = Host.Keychain.CreateSharedKey(in Host.Keys.Private, in remoteKey);

            OnCrossConnecting(time, remoteTime, remoteSession, in connect);
        }

        internal void OnCrossConnecting(Protocol.Time time, Protocol.Time remoteTime, uint remoteSession, in Protocol.Message.Connect connect)
        {
            Session.State = Protocol.State.Accepting;
            Session.Remote = remoteSession;
            MaxTransmissionUnit = connect.MaximumTransmissionUnit;

            var mtc = connect.MaximumTransmissionChannel;

            MaxChannel = mtc;
            channels = new Channel[mtc + 1];
            for (int i = 0; i <= mtc; ++i)
                channels[i].Initialize(remoteTime);

            RemoteBandwidth = connect.MaximumBandwidth >> 3;
            CongestionWindow = (ushort)(MaxSegmentSize << 2);

            Accept(LatestRemoteTime);
        }

        internal void OnConnected(Protocol.Time time, Protocol.Time remoteTime, uint remoteSession, in Protocol.Message.Accept accept)
        {
            Session.State = Protocol.State.Connected;
            Session.Remote = remoteSession;
            MaxTransmissionUnit = accept.MaximumTransmissionUnit;

            var mtc = accept.MaximumTransmissionChannel;

            MaxChannel = mtc;
            channels = new Channel[mtc + 1];
            for (int i = 0; i <= mtc; ++i)
                channels[i].Initialize(remoteTime);

            RemoteBandwidth = accept.MaximumBandwidth >> 3;
            CongestionWindow = (ushort)(MaxSegmentSize << 2);

            Acknowledge(Acknowledgment.Accept, LatestRemoteTime);

            control.CMD = default;
            OnAckMatched(time, accept.AcknowledgedTime);
        }

        internal void OnAccepted(Protocol.Time time, Protocol.Time acknowledgedTime)
        {
            Session.State = Protocol.State.Connected;

            control.CMD = default;
            OnAckMatched(time, acknowledgedTime);
        }

        internal void OnUpdate(Protocol.Time time)
        {
            if (connDeadline <= time)
            {
                Reset(Session.State == Protocol.State.Connecting ? PeerReason.Unreachable : PeerReason.TimedOut, PeerMode.Active);
                return;
            }

            if (ackDeadline <= time)
            {
                // Update statistics
                Interlocked.Increment(ref timeouts);

                ackFailCounter++;
                if (ackFailCounter >= Host.AckFailLimit)
                {
                    Reset(Session.State == Protocol.State.Connecting ? PeerReason.Unreachable : PeerReason.TimedOut, PeerMode.Active);
                    return;
                }

                OnAckTimeout(time);
            }

            if (idleDeadline <= time)
            {
                idleDeadline = null;
                ping = true;
            }

            SendWindow = Max(MaxSegmentSize, Min(Host.Upstream.BufferShare, CongestionWindow, RemoteWindow, BandwidthWindow));
        }

        #region Data Sending

        /// <summary>
        /// True if peer must be pinged.
        /// </summary>
        private bool ping;

        /// <summary>
        /// Next channel to process in <see cref="OnSend"/>
        /// </summary>
        private byte nextch;

        private void DecrementTransmissionBacklog(ushort value)
        {
            Debug.Assert(transmissionBacklog >= value, "Transmission backlog must be greater than or equal to the returned space");
            Interlocked.Add(ref transmissionBacklog, -value);
        }

        public void Send(byte[] data, Protocol.Delivery delivery = default, byte channel = 0) => Send(data, 0, data.Length, new Protocol.QoS(delivery), channel);
        public void Send(byte[] data, int offset, int length, Protocol.Delivery delivery = default, byte channel = 0) => Send(data, offset, length, new Protocol.QoS(delivery), channel);

        public void Send(byte[] data, in Protocol.QoS qos, byte channel = 0) => Send(data, 0, data.Length, qos, channel);
        public void Send(byte[] data, int offset, int length, in Protocol.QoS qos, byte channel = 0)
        {
            if (State != PeerState.Connected)
                throw new InvalidOperationException(SR.Peer.NotConnected);

            if (channel >= channels.Length)
                throw new ArgumentOutOfRangeException(nameof(channel));

            if (length == 0)
                return;

            if (length < Protocol.Datagram.Size.MinValue || length > Protocol.Datagram.Size.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(length));

            // No need to make transmissionBacklog volatile. It's only incremented by the user thread
            // so worst case scenario the worker thread may have just decremented it but the user 
            // thread (this one) hasn't loaded the updated value yet due to some instruction 
            // reordering. This won't affect the check below unless the application is running too 
            // close to max backlog in which case it should run with a larger max backlog that can 
            // handle variations in the throughput more nicely.
            if (MaxTransmissionBacklog - transmissionBacklog < length)
                throw new System.IO.InternalBufferOverflowException(SR.Peer.TransmissionBacklogLimitExceeded);

            Interlocked.Add(ref transmissionBacklog, length);

            if (length > MaxSegmentSize)
            {
                var expiration = unchecked(Host.Now() + ((qos.Timelimit - 1) & int.MaxValue));
                var seglen = (ushort)length;
                var fraglen = MaxFragmentSize;
                var fraglast = (byte)((length - 1) / fraglen);

                var first = CreateFragment(Host.UserEncoder, channel, qos.Delivery, expiration, 0, seglen, data, offset, fraglen);
                var last = first;

                byte fragindex = 1;
                do
                {
                    length -= fraglen;
                    offset += fraglen;
                    fraglen = Math.Min((ushort)length, MaxFragmentSize);
                    var message = CreateFragment(Host.UserEncoder, channel, qos.Delivery, expiration, fragindex, seglen, data, offset, fraglen);
                    message.AddAfter(last);
                    last = message;
                    fragindex++;
                }
                while (fragindex <= fraglast);

                channels[channel].TX.Send(first, last);
            }
            else
            {
                var message = CreateSegment(Host.UserEncoder, channel, qos.Delivery, unchecked(Host.Now() + ((qos.Timelimit - 1) & int.MaxValue)), data, offset, (ushort)length);
                channels[channel].TX.Send(message);
            }            
        }

        /// <summary>
        /// Returns true if a packet was written and must be transmitted; otherwise false (yielding to the next peer).
        /// </summary>
        internal bool OnSend(uint time, BinaryWriter packet)
        {
            var created = false;

            if (Session.State < Protocol.State.Connected) // Connecting / Accepting
            {
                // Send control commands
                switch (control.CMD)
                {
                    case Command.Transmit | Command.Connect:

                        control.CMD &= ~Command.Transmit;
                        created = true;

                        // There's no need to reserve space for the checksum because a CONNECT 
                        // either secure or insecure must be way below the minimum MTU.
                        packet.Reset(MaxTransmissionUnit);

                        if (Session.Options.Contains(SessionOptions.Secure))
                        {
                            packet.UnsafeWrite(time);
                            packet.UnsafeWrite(Protocol.PacketFlags.Secure | Protocol.PacketFlags.Connect);
                            packet.UnsafeWrite(Session.Local);
                            packet.UnsafeWrite(Host.MaxTransmissionUnit);
                            packet.UnsafeWrite(Host.MaxChannel);
                            packet.UnsafeWrite(Host.MaxBandwidth);
                            packet.UnsafeWrite(in Host.Keys.Public);
                        }
                        else
                        {
                            packet.UnsafeWrite(time);
                            packet.UnsafeWrite(Protocol.PacketFlags.Connect);
                            packet.UnsafeWrite(Session.Local);
                            packet.UnsafeWrite(Host.MaxTransmissionUnit);
                            packet.UnsafeWrite(Host.MaxChannel);
                            packet.UnsafeWrite(Host.MaxBandwidth);
                        }

                        packet.UnsafeWrite(Protocol.Packet.Insecure.Checksum.Compute(packet.Buffer, packet.Offset, packet.Count));

                        if (ackDeadline == null)
                            ackDeadline = time + ackTimeout;

                        if (connDeadline == null)
                            connDeadline = time + connTimeout;

                        break;
                    case Command.Transmit | Command.Accept:
                        control.CMD &= ~Command.Transmit;
                        created = true;

                        // There's no need to reserve space for the checksum (or nonce/mac) because an ACCEPT
                        // either secure or insecure must be way below the minimum MTU.
                        packet.Reset(MaxTransmissionUnit);

                        if (Session.Options.Contains(SessionOptions.Secure))
                        {
                            packet.Write(time);
                            packet.UnsafeWrite(Protocol.PacketFlags.Secure | Protocol.PacketFlags.Accept);
                            packet.UnsafeWrite(Session.Local);
                            packet.UnsafeWrite(Host.MaxTransmissionUnit);
                            packet.UnsafeWrite(Host.MaxChannel);
                            packet.UnsafeWrite(Host.MaxBandwidth);
                            packet.UnsafeWrite(control.AcceptanceTime);

                            var (buffer, offset, position, count) = (packet.Buffer, packet.Offset, packet.Position, sizeof(ushort));
                            packet.UnsafeWrite(ReceiveWindow);

                            var nonce64 = ++Session.Nonce;
                            var nonce = new Nonce(time, nonce64);

                            Session.Cipher.EncryptInPlace(buffer, position, count, in nonce);
                            Session.Cipher.Sign(buffer, offset, position - offset, count, in nonce, out Mac mac);

                            packet.UnsafeWrite(in Host.Keys.Public);
                            packet.UnsafeWrite(nonce64);
                            packet.UnsafeWrite(in mac);
                        }
                        else
                        {
                            packet.Write(time);
                            packet.UnsafeWrite(Protocol.PacketFlags.Accept);
                            packet.UnsafeWrite(Session.Local);
                            packet.UnsafeWrite(Host.MaxTransmissionUnit);
                            packet.UnsafeWrite(Host.MaxChannel);
                            packet.UnsafeWrite(Host.MaxBandwidth);
                            packet.UnsafeWrite(control.AcceptanceTime);
                            packet.UnsafeWrite(ReceiveWindow);
                            packet.UnsafeWrite(Session.Remote);
                            packet.UnsafeWrite(Protocol.Packet.Insecure.Checksum.Compute(packet.Buffer, packet.Offset, packet.Count));
                        }

                        // Start ack timer if not started yet
                        if (ackDeadline == null)
                            ackDeadline = time + ackTimeout;

                        // Start connection timer if not started yet
                        if (connDeadline == null)
                            connDeadline = time + connTimeout;
                        break;
                    default:
                        break;
                }
            }
            else // Connected
            {
                void EnsureDataPacketIsCreated()
                {
                    if (!created)
                    {
                        created = true;

                        if (Session.Options.Contains(SessionOptions.Secure))
                        {
                            packet.Reset(MaxTransmissionUnit - (Protocol.Packet.Secure.N64.Size + Protocol.Packet.Secure.Mac.Size));
                            packet.UnsafeWrite(time);
                            packet.UnsafeWrite(Protocol.PacketFlags.Secure | Protocol.PacketFlags.Data);
                            packet.UnsafeWrite(ReceiveWindow);
                        }
                        else
                        {
                            packet.Reset(MaxTransmissionUnit - Protocol.Packet.Insecure.Checksum.Size);
                            packet.UnsafeWrite(time);
                            packet.UnsafeWrite(Protocol.PacketFlags.Data);
                            packet.UnsafeWrite(Session.Local);
                            packet.UnsafeWrite(ReceiveWindow);
                        }
                    }
                }

                // Send control acks
                switch (control.ACK)
                {
                    case Acknowledgment.Accept:
                        control.ACK = default;

                        EnsureDataPacketIsCreated();
                        packet.UnsafeWrite(Protocol.MessageFlags.Ack | Protocol.MessageFlags.Accept);
                        packet.UnsafeWrite(control.AcknowledgedTime);

                        break;
                    default:
                        break;
                }

                if (capacity == 0) // No more packets with user data allowed but acks may still need to be transmitted.
                {
                    var endch = nextch;
                    do
                    {
                        ref var channel = ref channels[nextch];

                        if (channel.RX.Ack.Count > 0) // Channel must send ACK
                        {
                            EnsureDataPacketIsCreated();

                            if (!packet.TryWrite(new Protocol.Message.Ack(nextch, channel.RX.Ack.Count, channel.RX.NextSequenceNumber, channel.RX.LastSequenceNumber, channel.RX.Ack.LatestRemoteTime)))
                                break;

                            channel.RX.Ack.Count = 0;
                        }

                        nextch = (byte)((nextch + 1) % channels.Length);
                    }
                    while (nextch != endch);
                }
                else // Send both data and acks
                {
                    var transmitted = 0;
                    if (retransmittingChannelsCount > 0) // Peer has one or more channels that need to retransmit data
                    {
                        // At this point BytesInFlight must be greater than 0 and at least one channel must have the Retransmit pointer not null.
                        Debug.Assert(BytesInFlight > 0, $"{nameof(BytesInFlight)} must be greater than zero when there are restransmitting channels ({retransmittingChannelsCount})");

                        // Even if in the end all retransmissions are void (only unreliable messages were waiting) 
                        // an Enquire should still be transmitted so it's safe to ensure a packet header here. 
                        EnsureDataPacketIsCreated();

                        do
                        {
                            ref var channel = ref channels[nextch];
                            if (channel.RX.Ack.Count > 0) //  Channel must send an ack.      
                            {
                                // It's better to spread ACKs over multiple packets ahead of their own channel streams so that we don't 
                                // always end up with packets full of acks that may compromise several channels at once if lost.
                                if (!packet.TryWrite(new Protocol.Message.Ack(nextch, channel.RX.Ack.Count, channel.RX.NextSequenceNumber, channel.RX.LastSequenceNumber, channel.RX.Ack.LatestRemoteTime)))
                                    goto DoneSendingData;

                                channel.RX.Ack.Count = 0;
                            }

                            var retransmit = channel.TX.Retransmit;
                            if (retransmit != null) // Channel may have something to retransmit.  
                            {
                                var end = channel.TX.Next;
                                while (true)
                                {
                                    if (retransmit == end || retransmit.SequenceNumber > channel.TX.Ack.Last) // No more data to retransnmit in this channel (compare to end first because end can be null)
                                    {
                                        // Clear the retransmission pointer 
                                        channel.TX.Retransmit = null;
                                        // Decrement counter of channels retransmitting
                                        retransmittingChannelsCount--;

                                        // There's no need to adjust the sequence interval expected by the remote host.
                                        // Even if all messages were unreliable and are now presumed lost there's at least 
                                        // one sequence number left in the window for a ping that is going to be injected
                                        // after we break out of this loop.
                                        break;
                                    }

                                    if (retransmit.Encoded == null) // This is an unreliable message that cannot be retransmitted
                                    {
                                        // Message is now considered lost (not in flight anymore).
                                        BytesInFlight -= retransmit.Payload;
                                        // Return space to the transmit backlog.
                                        DecrementTransmissionBacklog(retransmit.Payload);

                                        var next = retransmit.Next;
                                        // Release message object.
                                        channel.TX.Messages.Dispose(retransmit);
                                        retransmit = next;
                                    }
                                    else
                                    {
                                        var position = packet.Count + 1;
                                        if (!packet.TryWrite(retransmit.Encoded)) // No more space left in the packet
                                        {
                                            // Update the retransmission pointer
                                            channel.TX.Retransmit = retransmit;
                                            goto DoneSendingData;
                                        }
                                        else // message was retransmitted (fix the packet)
                                        {
                                            // Update last send time
                                            retransmit.LatestSendTime = time;
                                            // Increment number of data bytes transmitted in this packet.
                                            transmitted += retransmit.Payload;
                                            // Move to next message to retransmit                                        
                                            retransmit = retransmit.Next;
                                        }
                                    }
                                }
                            }

                            // Select next channel
                            nextch = (byte)((nextch + 1) % channels.Length);
                        }
                        while (retransmittingChannelsCount > 0);// Repeat until no more channels need to retransmit

                        // If all messages have been dropped check that remote host is still alive
                        if (BytesInFlight == 0)
                            ping = true;
                    }

                    // Send new data after all retransmissions have been processed.        
                    var endch = nextch;
                    do
                    {
                        ref var channel = ref channels[nextch];

                        if (channel.RX.Ack.Count > 0) // channel must send ACK
                        {
                            EnsureDataPacketIsCreated();

                            // It's better to spread ACKs over multiple packets ahead of their own channel streams so that we don't 
                            // always end up with packets full of acks that may compromise several channels at once if lost.
                            if (!packet.TryWrite(new Protocol.Message.Ack(nextch, channel.RX.Ack.Count, channel.RX.NextSequenceNumber, channel.RX.LastSequenceNumber, channel.RX.Ack.LatestRemoteTime)))
                                goto DoneSendingData;

                            channel.RX.Ack.Count = 0;
                        }

                        // Flush the send buffer into the transmit buffer. 
                        // Using a separate send buffer to reduce lock contention. 
                        channel.TX.Flush();

                        var transmit = channel.TX.Next;
                        if (transmit != null) // There's a message to transmit                                                                       
                        {
                            do
                            {
                                // If either the send window limit or the sequence window limit has been reached then stall (break to next channel).
                                // Every data message in flight must be consuming at least 1 byte of the send window. In the worst case the number 
                                // of messages in flight is going to be equal to Ordinal.Window.Size (Ordianal.Window.Size-1 messages containing a 
                                // single byte of user data and 1 reliable ping message taking up 1 "virtual" byte). 
                                //
                                // Note that unreliable messages should not be dropped due to the lack of send window or sequence window space. 
                                // Otherwise all datagrams larger than the send window are going to be ultimately lost (last fragments dropped). 
                                // And if the user never sends a datagram smaller than the send window *several broken datagrams* will have to be
                                // partially acknowledged and buffered by the receiver until the send window has grown large enough to accommodate a
                                // complete datagram. Large datagrams will also be wasted if they go across the upper edge of the sequence window. 
                                // And even worst in some cases only the last fragment of a datagram (or a short succession of datagrams) would be 
                                // transmitted because their first fragments would not fit in the send window (SendWindow - BytesInFlight < MaxFragmentSize)
                                // but the last fragment would. Only after the first complete datagram (or ping) arrives is when the receiver is 
                                // able to discard all other partial datagrams previously buffered and deliver some data to the application.                                
                                if ((SendWindow - BytesInFlight < transmit.Payload)
                                 || (channel.TX.NextSequenceNumber == channel.TX.Ack.Next + (Protocol.Ordinal.Window.Size - 1)))
                                    break;

                                // Discard message if unreliable and expired. All fragments of a datagram set to expire have the same expirarion time. 
                                // Once a fragment is discarded so are all the remaining ones. Note that an unreliable fragmented datagram may be 
                                // dropped if it expires in the middle of the transmission process. That is, some of its fragments were transmitted in
                                // previous iterations but the remaining ones are now expired. In order to avoid this problem send large datagrams using 
                                // Protocol.Delivery.Semireliable.
                                if (transmit.Delivery == Protocol.Delivery.Unreliable && transmit.FirstSendTime < time)
                                {
                                    // Even though this message is going to be discarded, a sequence number must be allocated to avoid breaking continuity 
                                    // for fragmented datagrams. The presumption is that for every message A there must be N other messages prior to A where 
                                    // N = fragment.Index of A and they're all part of the same datagram (segments are considered a special case of fragment 
                                    // with Index = Last = 0).
                                    channel.TX.NextSequenceNumber++;

                                    // Return space to the transmit backlog.
                                    DecrementTransmissionBacklog(transmit.Payload);

                                    // Release unreliable message and goto next
                                    var next = transmit.Next;
                                    channel.TX.Messages.Dispose(transmit);
                                    transmit = next;
                                    continue;
                                }

                                EnsureDataPacketIsCreated();

                                var length = transmit.Encoded.Length;
                                if (packet.Available < length) // No more space left in the packet. 
                                {
                                    // Update transmit pointer
                                    channel.TX.Next = transmit;
                                    // Break out to try again with the next packet. 
                                    // It's cheaper than searching through the channels 
                                    // for a message that might fit in the space available.
                                    goto DoneSendingData;
                                }

                                // Generated SEQ and RSN, setup the message and transmit.
                                // Unreliable messages are not retransmistted so the encoded 
                                // message may be disposed immediately.                                
                                var seq = channel.TX.NextSequenceNumber++;
                                switch (transmit.Delivery)
                                {
                                    case Protocol.Delivery.Unreliable:
                                        var position = packet.Count + 1;
                                        packet.UnsafeWrite(transmit.Encoded, 0, length);
                                        packet.UnsafeOverwrite(seq, position);
                                        packet.UnsafeOverwrite(channel.TX.NextReliableSequenceNumber, position + 2);
                                        // Unreliable messages are not retransmitted so the encoded message may now be discarded.
                                        transmit.Encoded.Dispose();
                                        transmit.Encoded = null;
                                        break;
                                    case Protocol.Delivery.Semireliable:
                                        transmit.Encoded.UnsafeOverwrite(seq, 1);
                                        transmit.Encoded.UnsafeOverwrite(channel.TX.NextReliableSequenceNumber, 3);
                                        packet.UnsafeWrite(transmit.Encoded, 0, length);
                                        break;
                                    case Protocol.Delivery.Reliable:
                                        transmit.Encoded.UnsafeOverwrite(seq, 1);
                                        transmit.Encoded.UnsafeOverwrite(++channel.TX.NextReliableSequenceNumber, 3);
                                        packet.UnsafeWrite(transmit.Encoded, 0, length);
                                        break;
                                    default:
                                        throw new NotSupportedException();
                                }

                                transmit.SequenceNumber = seq;
                                transmit.FirstSendTime = time;
                                transmit.LatestSendTime = time;

                                Interlocked.Add(ref dataSent, transmit.Payload);

                                BytesInFlight += transmit.Payload;
                                transmitted += transmit.Payload;

                                transmit = transmit.Next;
                            }
                            while (transmit != null);

                            channel.TX.Next = transmit;
                        }

                        nextch = (byte)((nextch + 1) % channels.Length);
                    }
                    while (nextch != endch);

                    DoneSendingData:
                    {
                        // A ping may be required either due to having all retransmissions discarded for being unreliable
                        // after an ack timeout or because of an idle timeout.
                        if (ping)
                        {
                            ping = false;
                            // There's no need to send a ping if the transmission backlog is not empty.
                            // We can't rely on BytesInFlight here because an ack could've been written
                            // to the packet while every next message of every other channel is too large 
                            // for the space available in the packet. In such case we end up with 
                            // BytesInFlight == 0 and yet some messages waiting to be transmitted.
                            ref var channel = ref channels[0];
                            if (transmissionBacklog == 0)
                            {
                                EnsureDataPacketIsCreated();

                                var encoder = Host.WorkerEncoder;
                                encoder.Reset();
                                encoder.Ensure(Protocol.Message.Segment.MinSize + 1);
                                encoder.UnsafeWrite(Protocol.MessageFlags.Reliable | Protocol.MessageFlags.Data | Protocol.MessageFlags.Segment);

                                Channel.Outbound.Message transmit;
                                if (packet.Available > Protocol.Message.Segment.MinSize)
                                {
                                    var seq = channel.TX.NextSequenceNumber++;
                                    var rsn = ++channel.TX.NextReliableSequenceNumber;

                                    encoder.UnsafeWrite(seq);
                                    encoder.UnsafeWrite(rsn);
                                    encoder.UnsafeWrite((ushort)0);

                                    transmit = CreatePing(encoder, time);
                                    Debug.Assert(channel.TX.Messages.IsEmpty, "Channel TX buffer should be empty");
                                    channel.TX.Messages.AddLast(transmit);

                                    packet.UnsafeWrite(transmit.Encoded, 0, transmit.Encoded.Length);

                                    transmit.SequenceNumber = seq;
                                    transmit.LatestSendTime = time;

                                    BytesInFlight += 1;
                                    transmitted += 1;
                                    Interlocked.Increment(ref transmissionBacklog);

                                    transmit = transmit.Next;
                                }
                                else
                                {
                                    // Actual SEQ and RSN values must be assigned on transmit by the worker thread.
                                    // This is to avoid locking the generators as they would represent a shared resource between 
                                    // the user and worker threads. The worker thread may need to write update the generators to 
                                    // inject a ping in the output queue.
                                    encoder.UnsafeWrite(0, 2 + 2 + 2); // SEQ:RSN:LEN

                                    transmit = CreatePing(encoder, unchecked(Host.Now() + int.MaxValue));
                                    Debug.Assert(channel.TX.Messages.IsEmpty, "Channel TX buffer should be empty");
                                    channel.TX.Messages.AddLast(transmit);
                                }

                                channel.TX.Next = transmit;
                            }
                        }

                        if (transmitted > 0) // This packet contains user data (or a ping)
                        {
                            // Decrement data packet countdown if neither finished nor infinite.
                            if (capacity > 0)
                                capacity--;

                            // Start ack timer if not started yet
                            if (ackDeadline == null)
                                ackDeadline = time + ackTimeout;

                            // Start connection timer if not started yet                                                 
                            if (connDeadline == null)
                                connDeadline = time + connTimeout;
                        }
                    }
                }

                if (created)
                {
                    if (Session.Options.Contains(SessionOptions.Secure))
                    {
                        var nonce64 = ++Session.Nonce;
                        var nonce = new Nonce(time, nonce64);
                        var (buffer, position, count) = (packet.Buffer, packet.Offset + Protocol.Packet.Header.Size, packet.Count - Protocol.Packet.Header.Size);
                        Session.Cipher.EncryptInPlace(buffer, position, count, in nonce);
                        Session.Cipher.Sign(packet.Buffer, packet.Offset, Protocol.Packet.Header.Size, count, in nonce, out Mac mac);
                        packet.Expand(Protocol.Packet.Secure.N64.Size + Protocol.Packet.Secure.Mac.Size);
                        packet.UnsafeWrite(nonce64);
                        packet.UnsafeWrite(in mac);
                    }
                    else
                    {
                        var (buffer, offset, count) = (packet.Buffer, packet.Offset, packet.Count);
                        var crc = Protocol.Packet.Insecure.Checksum.Compute(buffer, offset, count);
                        packet.Expand(Protocol.Packet.Insecure.Checksum.Size);
                        packet.UnsafeWrite(crc);
                    }
                }
            }

            return created;
        }

        private Channel.Outbound.Message CreateSegment(BinaryWriter encoder, byte channel, Protocol.Delivery delivery, Protocol.Time expiration, byte[] data, int offset, ushort length)
        {
            encoder.Reset();
            encoder.Ensure(Protocol.Message.Segment.MinSize + 1);

            Protocol.MessageFlags flags;
                        
            if (delivery == Protocol.Delivery.Reliable)
            {
                flags = Protocol.MessageFlags.Reliable | Protocol.MessageFlags.Data | Protocol.MessageFlags.Segment
                    | ((Protocol.MessageFlags)channel & Protocol.MessageFlags.Channel);
            }
            else
            {
                flags = Protocol.MessageFlags.Data | Protocol.MessageFlags.Segment
                    | ((Protocol.MessageFlags)channel & Protocol.MessageFlags.Channel);
            }

            encoder.UnsafeWrite(flags);

            // Actual SEQ and RSN values must be assigned on transmit by the worker thread.
            // This is to avoid locking the generators as they would represent a shared resource between 
            // the user and worker threads. The worker thread may need to write update the generators to 
            // inject a ping in the output queue.
            encoder.UnsafeWrite(0, 2 + 2); // SEQ:RSN

            encoder.UnsafeWrite(length);
            encoder.UnsafeWrite(data, offset, length);

            Host.Allocate(out Memory encoded);
            encoded.CopyFrom(encoder.Buffer, encoder.Offset, (ushort)encoder.Count);

            Host.Allocate(out Channel.Outbound.Message message);
            message.Delivery = delivery;
            message.FirstSendTime = expiration;
            message.Payload = length;
            message.Encoded = encoded;

            return message;
        }

        private Channel.Outbound.Message CreateFragment(BinaryWriter encoder, byte channel, Protocol.Delivery delivery, Protocol.Time expiration, byte fragindex, ushort seglen, byte[] data, int offset, ushort length)
        {
            encoder.Reset();
            encoder.Ensure(Protocol.Message.Fragment.MinSize + 1);

            Protocol.MessageFlags flags;

            if (delivery == Protocol.Delivery.Reliable)
            {
                flags = Protocol.MessageFlags.Reliable | Protocol.MessageFlags.Data | Protocol.MessageFlags.Fragment
                    | ((Protocol.MessageFlags)channel & Protocol.MessageFlags.Channel);
            }
            else
            {
                flags = Protocol.MessageFlags.Data | Protocol.MessageFlags.Fragment
                    | ((Protocol.MessageFlags)channel & Protocol.MessageFlags.Channel);
            }

            encoder.UnsafeWrite(flags);

            // Note that actual SEQ and RSN values must be assigned on transmit by the worker thread.
            // This is to avoid locking the generators as they would represent a shared resource between 
            // the user and worker threads. The worker thread needs to update (write) the generators to 
            // inject a ping in the output queue.
            encoder.UnsafeWrite(0, 2 + 2); // SEQ:RSN

            encoder.UnsafeWrite(seglen);
            encoder.UnsafeWrite(fragindex);            
            encoder.UnsafeWrite(length);
            encoder.UnsafeWrite(data, offset, length);

            Host.Allocate(out Memory encoded);
            encoded.CopyFrom(encoder.Buffer, encoder.Offset, (ushort)encoder.Count);

            Host.Allocate(out Channel.Outbound.Message message);
            message.Delivery = delivery;
            message.FirstSendTime = expiration;
            message.Payload = length;
            message.Encoded = encoded;

            return message;
        }

        private Channel.Outbound.Message CreatePing(BinaryWriter encoder, Protocol.Time firstSendTime)
        {
            Host.Allocate(out Memory encoded);
            encoded.CopyFrom(encoder.Buffer, encoder.Offset, (ushort)encoder.Count);

            Host.Allocate(out Channel.Outbound.Message message);
            message.Delivery = Protocol.Delivery.Reliable;

            // A ping must take up at least one byte so we can avoid having to
            // handle pings differently from other reliable messages.
            message.Payload = 1;
            message.Encoded = encoded;
            message.FirstSendTime = firstSendTime;

            return message;
        }

        #endregion

        #region Data Receiving 

        internal void OnReceive(Protocol.Time time, Protocol.Time remoteTime, in Protocol.Message.Ack ack)
        {
            if (BytesInFlight == 0)
                return;

            var channel = ack.Channel;
            if (channel < channels.Length)
            {
                ref var outbound = ref channels[channel].TX;

                if ((outbound.Messages.First == outbound.Next)                                                                  // Obsolete ack (there's nothing to ack)
                 || (ack.AcknowledgedTime < outbound.Messages.First.FirstSendTime)                                              // Old ack
                 || (ack.Last < ack.Next || ack.Next > outbound.NextSequenceNumber || ack.Last > outbound.NextSequenceNumber)   // Invalid ack
                 || (ack.Next < outbound.Ack.Next))                                                                             // Obsolete ack
                {
                    return;
                }

                if (ack.Next == outbound.Ack.Next) // Duplicate ack/gap
                {
                    // Update RTT and ack timeout
                    OnAckReceived(time, ack.AcknowledgedTime);
                    // Release any artificial send limit imposed by a previous timeout
                    capacity = Protocol.Countdown.Infinite;
                    // Reset ack failures
                    ackFailCounter = 0;
                    // Restart timers
                    ackDeadline = time + ackTimeout;
                    connDeadline = time + connTimeout;

                    if (outbound.LatestAckRemoteTime < remoteTime) // Latest ack/gap
                    {
                        // Update send time of the latest ack
                        outbound.LatestAckRemoteTime = remoteTime;
                        // Update gap information (last message to retransmit)
                        outbound.Ack.Last = ack.Last;
                        // Increment the dup ack counter used to trigger an early retransmit
                        outbound.Ack.Count += ack.Count;
                    }
                    else if (ack.Next != ack.Last) // Late gap
                    {
                        // If issued after the last (re)transmission but arrived out of order then
                        // increment the dup ack counter used to trigger an early retransmit        
                        if (ack.AcknowledgedTime >= outbound.Messages.First.LatestSendTime)
                            outbound.Ack.Count += ack.Count;
                    }
                }
                else // New ack
                {
                    // Update RTT and ack timeout
                    OnAckReceived(time, ack.AcknowledgedTime);
                    // Release any artificial send limit imposed by a previous timeout
                    capacity = Protocol.Countdown.Infinite;
                    // Reset ack failures
                    ackFailCounter = 0;

                    // Release acknowledged messages and compute the total acknowleged size
                    var asize = default(ushort);
                    var message = outbound.Messages.First;
                    while (message != outbound.Next && message.SequenceNumber < ack.Next)
                    {
                        asize += message.Payload;

                        // Calling message.Dispose() directly here instead of channel.TX.BUFFER.Dispose() 
                        // because we know how channel.TX.First and Last are going to end up.
                        var next = message.Next;
                        message.Dispose();
                        message = next;
                    }

                    // Assigning channel.TX.First also fixes channel.TX.Last if needed (side effect)
                    outbound.Messages.First = message;

                    // Return space to the transmit backlog
                    DecrementTransmissionBacklog(asize);

                    // If more than half of the congestion window was used 
                    // increase it in either slow start or avoidance mode depending on the estimated link capacity
                    if (BytesInFlight > (CongestionWindow >> 1))
                        CongestionWindow = (ushort)Math.Min(ushort.MaxValue, CongestionWindow + ((CongestionWindow < LinkCapacity) ? asize : 1));

                    // Acknowledged bytes are not in flight anymore
                    BytesInFlight -= asize;

                    if (BytesInFlight == 0) // all messages have been acknowledged                                                                         
                    {
                        // Stop timers
                        ackDeadline = default;
                        connDeadline = default;
                        // Start enquire timer
                        idleDeadline = time + Host.IdleTimeout;

                        // If Channel was retransmitting
                        if (outbound.Retransmit != null)
                        {
                            // Reset the retransmission pointer 
                            outbound.Retransmit = null;
                            // Decrement counter of channels actively retransmitting 
                            retransmittingChannelsCount--;
                        }
                    }
                    else // some messages still remain unacknowledged   
                    {
                        // Restart timers
                        ackDeadline = time + ackTimeout;
                        connDeadline = time + connTimeout;

                        if (outbound.Retransmit != null) // Channel was retransmitting
                        {
                            if (ack.Next > outbound.Ack.Last) // Gap has been completely acknowledged 
                            {
                                // Reset the retransmission pointer 
                                outbound.Retransmit = null;
                                // Decrement counter of channels actively retransmitting 
                                retransmittingChannelsCount--;
                            }
                            else
                            {
                                // If the gap has been updated past the retransmission pointer
                                // the message it was pointing to must have been disposed. 
                                // A message may have a payload of 0 bytes only when disposed.
                                if (outbound.Retransmit.Payload == 0) 
                                    outbound.Retransmit = outbound.Messages.First;
                            }
                        }
                    }

                    // This is guaranteed the latest ack source time and most up-to-date ack/gap information
                    outbound.LatestAckRemoteTime = remoteTime;      
                    outbound.Ack = (ack.Next, ack.Last, ack.Count);
                }

                // Trigger a fast retransmission if channel is not retransmitting yet 
                // and there is a gap wtih enough packets received after.
                if (outbound.Retransmit == null && outbound.Ack.Count >= Protocol.FastRetransmit.Threshold)
                {
                    // Increment counter of channels actively retransmmitting
                    retransmittingChannelsCount++;

                    // Set retransmit pointer to the first message
                    outbound.Retransmit = outbound.Messages.First;

                    // Update statistics
                    Interlocked.Increment(ref fastRetransmissions);

                    // If this is the first channel that started retransmitting adjust the estimated link capacity
                    if (retransmittingChannelsCount == 1)
                        LinkCapacity = (ushort)Math.Max(CongestionWindow >> 1, MaxSegmentSize << 2);
                }
            }
        }

        internal void OnReceive(Protocol.Time time, Protocol.Time remoteTime, bool reliable, bool isFirstInPacket, in Protocol.Message.Segment segment)
        {
            var channel = segment.Channel;
            if (channel >= channels.Length)
                return;

            ref var inbound = ref channels[channel].RX;

            if (segment.SequenceNumber == inbound.NextSequenceNumber) // message is the next expected (either reliable or unreliable)
            {
                var (xseq, window) = GetCrossSequenceNumberAndStaticWindow(inbound.CrossSequenceNumber, 1);

                // Discard if message does not belong to this incarnation of the sliding window
                if (remoteTime <= inbound.NextRemoteTimes[window])
                    return;

                inbound.UpdateNextRemoteTime(window, remoteTime);
                
                if (segment.Data.Count > 0)
                {
                    Interlocked.Add(ref dataReceived, segment.Data.Count);

                    Host.Allocate(out Memory memory);
                    memory.CopyFrom(in segment.Data);

                    Host.Add(new Event(this, new Data(channel, memory)));
                }

                inbound.CrossSequenceNumber = xseq;
                inbound.NextSequenceNumber = segment.SequenceNumber + 1;

                if (reliable)
                {
                    inbound.NextReliableSequenceNumber = segment.ReliableSequenceNumber;

                    // Deliver any messages stalled by this segment
                    Deliver(ref inbound, channel);
                }

                inbound.UpdateLastSequenceNumber();
                inbound.UpdateLowestSequenceNumber();

                inbound.Acknowledge(remoteTime, isFirstInPacket);
            }
            else if (segment.SequenceNumber > inbound.NextSequenceNumber) // message is ahead of the next expected
            {
                var (xseq, window) = GetCrossSequenceNumberAndStaticWindow(inbound.CrossSequenceNumber, (ushort)unchecked((ushort)(segment.SequenceNumber - inbound.NextSequenceNumber) + 1));

                // Discard if message does not belong to this incarnation of the sliding window
                if (remoteTime <= inbound.NextRemoteTimes[window])
                    return;

                if (segment.ReliableSequenceNumber == inbound.NextReliableSequenceNumber) // unreliable message arriving ahead of other unreliable ones (because the ReliableSequenceNumber is still the same)
                {
                    inbound.UpdateNextRemoteTime(window, remoteTime);                    

                    if (segment.Data.Count > 0)
                    {
                        Interlocked.Add(ref dataReceived, segment.Data.Count);

                        Host.Allocate(out Memory memory);
                        memory.CopyFrom(in segment.Data);

                        Host.Add(new Event(this, new Data(channel, memory)));
                    }

                    // Remove previous unreliable fragments
                    inbound.Messages.RemoveAndDisposeBefore(segment.SequenceNumber);

                    // Remove previous reassemblies
                    inbound.Reassemblies.RemoveAndDisposeBefore(segment.SequenceNumber);

                    inbound.CrossSequenceNumber = xseq;
                    inbound.NextSequenceNumber = segment.SequenceNumber + 1;
                    inbound.NextReliableSequenceNumber = segment.ReliableSequenceNumber;

                    inbound.UpdateLastSequenceNumber();
                    inbound.UpdateLowestSequenceNumber();

                    inbound.Acknowledge(remoteTime, isFirstInPacket);
                }
                else if (reliable && segment.ReliableSequenceNumber == inbound.NextReliableSequenceNumber + 1) // message is the next reliable expected
                {
                    inbound.UpdateNextRemoteTime(window, remoteTime);

                    if (segment.Data.Count > 0)
                    {
                        Interlocked.Add(ref dataReceived, segment.Data.Count);

                        Host.Allocate(out Memory memory);
                        memory.CopyFrom(in segment.Data);

                        Host.Add(new Event(this, new Data(channel, memory)));
                    }

                    // Remove previous messages if any
                    inbound.Messages.RemoveAndDisposeBefore(segment.SequenceNumber);

                    // Remove previous reassemblies if any
                    inbound.Reassemblies.RemoveAndDisposeBefore(segment.SequenceNumber);

                    inbound.CrossSequenceNumber = xseq;
                    inbound.NextSequenceNumber = segment.SequenceNumber + 1;
                    inbound.NextReliableSequenceNumber = segment.ReliableSequenceNumber;

                    // Deliver any messages stalled by this segment
                    Deliver(ref inbound, channel);

                    inbound.UpdateLastSequenceNumber();
                    inbound.UpdateLowestSequenceNumber();

                    inbound.Acknowledge(remoteTime, isFirstInPacket);
                }
                else if (segment.ReliableSequenceNumber >= inbound.NextReliableSequenceNumber + 1) // message arrived ahead of at least one reliable message that is missing
                {
                    inbound.UpdateNextRemoteTime(window, remoteTime);

                    if (inbound.Messages.TryAddOrGet(segment.SequenceNumber, Host.Allocate, out Channel.Inbound.Message message)) // message is new
                    {
                        message.ReliableSequenceNumber = segment.ReliableSequenceNumber;
                        message.IsReliable = reliable;

                        if (segment.Data.Count > 0)
                        {
                            Interlocked.Add(ref dataReceived, segment.Data.Count);

                            Host.Allocate(out Memory memory);
                            memory.CopyFrom(in segment.Data);
                            message.Data = memory;
                        }

                        inbound.UpdateLastSequenceNumber();
                    }

                    inbound.Acknowledge(remoteTime, isFirstInPacket);
                }
            }
            else if (segment.SequenceNumber >= inbound.LowestSequenceNumber) // message has been delivered already but is still inside the acknowledgement window
            {
                var (xseq, window) = GetCrossSequenceNumberAndStaticWindow(inbound.CrossSequenceNumber, -(ushort)unchecked((ushort)(inbound.NextSequenceNumber - segment.SequenceNumber) - 1));

                // Discard if message does not belong to this incarnation of the sliding window
                if (remoteTime <= inbound.NextRemoteTimes[window])
                    return;

                inbound.Acknowledge(remoteTime, isFirstInPacket);
            }
        }

        internal void OnReceive(Protocol.Time time, Protocol.Time remoteTime, bool reliable, bool isFirstInPacket, in Protocol.Message.Fragment fragment)
        {
            var channel = fragment.Channel;
            if (channel >= channels.Length)
                return;

            ref var inbound = ref channels[channel].RX;

            if (fragment.SequenceNumber == inbound.NextSequenceNumber) // message is the next expected (either reliable or unreliable)
            {
                var (xseq, window) = GetCrossSequenceNumberAndStaticWindow(inbound.CrossSequenceNumber, 1);

                // Discard if message does not belong to this incarnation of the sliding window
                if (remoteTime <= inbound.NextRemoteTimes[window])
                    return;

                // If an assembly can be added then this is also the first fragment that has arrived, otherwise
                // other fragments have arrived ahead and we're filling a gap.
                if (inbound.Reassemblies.TryAddOrGet(fragment.SequenceNumber + (byte)(fragment.Last - fragment.Index), Host.Allocate, out Channel.Inbound.Reassembly reassembly))
                {
                    Host.Allocate(out Memory memory);
                    memory.Length = fragment.DatagramLength;

                    reassembly.Last = fragment.Last;
                    reassembly.Data = memory;
                }
                else
                {
                    // Last fragment index must match.
                    if (reassembly.Last != fragment.Last)
                        return;

                    // Datagram length must match the allocated buffer length.
                    if (reassembly.Data.Length != fragment.DatagramLength)
                        return;
                }

                inbound.UpdateNextRemoteTime(window, remoteTime);

                Interlocked.Add(ref dataReceived, fragment.Data.Count);
                reassembly.Data.CopyFrom(in fragment.Data, fragment.Index * MaxFragmentSize);

                if (fragment.Index == fragment.Last) // this is the last fragment so reassembly is complete
                {
                    // There's no need to create a message node, just deliver.
                    Host.Add(new Event(this, new Data(channel, reassembly.Data)));
                    reassembly.Data = null;

                    var next = reassembly.SequenceNumber + 1;

                    // Remove all fragment placeholders from the message buffer including the last one.
                    inbound.Messages.RemoveAndDisposeBefore(next);

                    // Remove reassemblies including this one.
                    inbound.Reassemblies.RemoveAndDisposeBefore(next);

                    inbound.CrossSequenceNumber = GetCrossSequenceNumber(inbound.CrossSequenceNumber, (ushort)unchecked((ushort)(reassembly.SequenceNumber - inbound.NextSequenceNumber) + 1));
                    inbound.NextSequenceNumber = next;

                    if (reliable)
                    {
                        // If delivery is reliable every fragment has an individual reliable sequence number and this is the latest one.
                        inbound.NextReliableSequenceNumber = fragment.ReliableSequenceNumber;

                        // Deliver messages waiting for this one (if any)
                        Deliver(ref inbound, channel);
                    }
                }
                else // reassembly may or may not be complete depending whether other fragments have already arrived.
                {
                    // There's no need to create a fragment placeholder, because this is the next expected anyway.
                    inbound.CrossSequenceNumber = GetCrossSequenceNumber(inbound.CrossSequenceNumber, 1);
                    inbound.NextSequenceNumber = fragment.SequenceNumber + 1;
                    inbound.NextReliableSequenceNumber = fragment.ReliableSequenceNumber;

                    // Deliver messages waiting for this one (if any)
                    Deliver(ref inbound, channel);
                }

                inbound.UpdateLastSequenceNumber();
                inbound.UpdateLowestSequenceNumber();

                inbound.Acknowledge(remoteTime, isFirstInPacket);
            }
            else if (fragment.SequenceNumber > inbound.NextSequenceNumber) // message is ahead of the next expected
            {
                var (xseq, window) = GetCrossSequenceNumberAndStaticWindow(inbound.CrossSequenceNumber, (ushort)unchecked((ushort)fragment.SequenceNumber - (ushort)(inbound.NextSequenceNumber + 1)));

                // Discard if message does not belong to this incarnation of the sliding window
                if (remoteTime <= inbound.NextRemoteTimes[window])
                    return;

                if ((fragment.ReliableSequenceNumber == inbound.NextReliableSequenceNumber)                     // unreliable message arriving ahead of other unreliable ones (because the ReliableSequenceNumber is still the same)
                  || (reliable && fragment.ReliableSequenceNumber == inbound.NextReliableSequenceNumber + 1))   // OR message is the next reliable expected
                {
                    // If an assembly can be added then this is also the first fragment that has arrived, otherwise
                    // other fragments have arrived ahead and we're filling a gap.
                    if (inbound.Reassemblies.TryAddOrGet(fragment.SequenceNumber + (byte)(fragment.Last - fragment.Index), Host.Allocate, out Channel.Inbound.Reassembly reassembly))
                    {
                        Host.Allocate(out Memory memory);
                        memory.Length = fragment.DatagramLength;

                        reassembly.Last = fragment.Last;
                        reassembly.Data = memory;
                    }
                    else
                    {
                        // Last fragment index must match.
                        if (reassembly.Last != fragment.Last)
                            return;

                        // Datagram length must match the allocated buffer length.
                        if (reassembly.Data.Length != fragment.DatagramLength)
                            return;
                    }

                    inbound.UpdateNextRemoteTime(window, remoteTime);

                    if (inbound.Messages.TryAddOrGet(fragment.SequenceNumber, Host.Allocate, out Channel.Inbound.Message message)) // message is new
                    {
                        Interlocked.Add(ref dataReceived, fragment.Data.Count);
                        reassembly.Data.CopyFrom(in fragment.Data, fragment.Index * MaxFragmentSize);

                        message.ReliableSequenceNumber = fragment.ReliableSequenceNumber;
                        message.IsReliable = reliable;
                        message.Reassembly = reassembly;

                        // Out of order delivery is expected to be uncommon, it's safer to let fragments from a datagram 
                        // cause other unreliable fragments from a previous datagram to be dropped regardless of completion. 
                        // Otherwise a sender that always transmits fragmented datagrams but never the last fragment may 
                        // induce a full buffer to be constantly allocated on the receiver without ever having any data 
                        // actually delivered.
                        var first = fragment.SequenceNumber - fragment.Index;
                        if (inbound.NextSequenceNumber < first)
                        {
                            // Remove previous datagrams if any
                            inbound.Messages.RemoveAndDisposeBefore(first);

                            // Remove previous reassemblies if any
                            inbound.Reassemblies.RemoveAndDisposeBefore(first);

                            inbound.CrossSequenceNumber = GetCrossSequenceNumber(inbound.CrossSequenceNumber, (ushort)(first - inbound.NextSequenceNumber));
                            inbound.NextSequenceNumber = first;

                            // It's safe to assign the reliable sequence number here because this is either 
                            // an unreliable message arriving ahead of other unreliable ones (so the reliable 
                            // sequence number is in fact the same) OR this is the next reliable message expected.
                            inbound.NextReliableSequenceNumber = fragment.ReliableSequenceNumber;
                        }

                        Deliver(ref inbound, channel);

                        inbound.UpdateLastSequenceNumber();
                        inbound.UpdateLowestSequenceNumber();
                    }

                    inbound.Acknowledge(remoteTime, isFirstInPacket);
                }
                else if (fragment.ReliableSequenceNumber >= inbound.NextReliableSequenceNumber + 1) // message arrived ahead of at least one reliable message that is missing
                {
                    // If an assembly can be added then this is also the first fragment that has arrived, otherwise
                    // other fragments have arrived ahead and we're filling a gap.
                    if (inbound.Reassemblies.TryAddOrGet(fragment.SequenceNumber + (byte)(fragment.Last - fragment.Index), Host.Allocate, out Channel.Inbound.Reassembly reassembly))
                    {
                        Host.Allocate(out Memory memory);
                        memory.Length = fragment.DatagramLength;

                        reassembly.Last = fragment.Last;
                        reassembly.Data = memory;
                    }
                    else
                    {
                        // Last fragment index must match.
                        if (reassembly.Last != fragment.Last)
                            return;

                        // Datagram length must match the allocated buffer length.
                        if (reassembly.Data.Length != fragment.DatagramLength)
                            return;
                    }

                    inbound.UpdateNextRemoteTime(window, remoteTime);

                    if (inbound.Messages.TryAddOrGet(fragment.SequenceNumber, Host.Allocate, out Channel.Inbound.Message message)) // message is new
                    {
                        Interlocked.Add(ref dataReceived, fragment.Data.Count);
                        reassembly.Data.CopyFrom(in fragment.Data, fragment.Index * MaxFragmentSize);

                        message.ReliableSequenceNumber = fragment.ReliableSequenceNumber;
                        message.IsReliable = reliable;
                        message.Reassembly = reassembly;

                        inbound.UpdateLastSequenceNumber();
                    }

                    inbound.Acknowledge(remoteTime, isFirstInPacket);
                }
            }
            else if (fragment.SequenceNumber >= inbound.LowestSequenceNumber) // message has been delivered already but is still inside the acknowledgement window
            {
                var (xseq, window) = GetCrossSequenceNumberAndStaticWindow(inbound.CrossSequenceNumber, -(ushort)unchecked((ushort)inbound.NextSequenceNumber - 1 - (ushort)fragment.SequenceNumber));

                // Discard if message does not belong to this incarnation of the sliding window
                if (remoteTime <= inbound.NextRemoteTimes[window])
                    return;

                inbound.Acknowledge(remoteTime, isFirstInPacket);
            }
        }

        /// <summary>
        /// Calculate a cross sequence number from the provided cross sequence number and a delta.
        /// <para/>
        /// Refer to <see cref="Protocol.Ordinal "/> for an explanation of the idea behind cross sequence numbers and expected source times for static windows.
        /// </summary>
        private static ushort GetCrossSequenceNumber(ushort xseq, int delta) => (ushort)((xseq + delta) & ((4 * Protocol.Ordinal.Window.Size) - 1)); // equivalent to (xseq + delta) % (4 * Protocol.Ordinal.Window.Size)

        /// <summary>
        /// Calculate a cross sequence number and static window from the provided cross sequence number and delta.
        /// <para/>
        /// Refer to <see cref="Protocol.Ordinal "/> for an explanation of the idea behind cross sequence number and static window expected source times.
        /// </summary>
        private static (ushort, int) GetCrossSequenceNumberAndStaticWindow(ushort xseq, int delta)
        {
            xseq = (ushort)((xseq + delta) & ((4 * Protocol.Ordinal.Window.Size) - 1)); // equivalent to (xseq + delta) % (4 * Protocol.Ordinal.Window.Size)
            return (xseq, xseq >> 15);
        }

        #endregion

        #region Data Delivery

        private struct DeliveryState
        {
            public Peer Peer;
            public byte Channel;

            public Protocol.Ordinal NextSequenceNumber;
            public Protocol.Ordinal NextReliableSequenceNumber;
        }

        private void Deliver(ref Channel.Inbound inbound, byte channel)
        {
            var state = new DeliveryState
            {
                Peer = this,
                Channel = channel,
                NextSequenceNumber = inbound.NextSequenceNumber,
                NextReliableSequenceNumber = inbound.NextReliableSequenceNumber
            };

            inbound.Messages.Traverse(Deliver, ref state);

            // Remove processed messages.
            inbound.Messages.RemoveAndDisposeBefore(state.NextSequenceNumber);

            // Remove obsolete reassemblies.
            inbound.Reassemblies.RemoveAndDisposeBefore(state.NextSequenceNumber);

            inbound.CrossSequenceNumber += (ushort)((ushort)state.NextSequenceNumber - (ushort)inbound.NextSequenceNumber);
            inbound.NextSequenceNumber = state.NextSequenceNumber;
            inbound.NextReliableSequenceNumber = state.NextReliableSequenceNumber;
            
        }

        private static bool Deliver(Channel.Inbound.Message message, ref DeliveryState state)
        {
            if (message.SequenceNumber == state.NextSequenceNumber)
            {
                if (message.Data != null) // unfragmented datagram
                {
                    var peer = state.Peer;
                    peer.Host.Add(new Event(peer, new Data(state.Channel, message.Data)));
                    message.Data = null;
                }
                else if (message.Reassembly != null) // fragmented datagram
                {
                    // If this is the last fragment then the datagram is complete and can be delivered.
                    if (message.Reassembly.SequenceNumber == message.SequenceNumber)
                    {
                        var peer = state.Peer;
                        peer.Host.Add(new Event(state.Peer, new Data(state.Channel, message.Reassembly.Data)));
                        message.Reassembly.Data = null;
                    }
                }

                state.NextSequenceNumber++;
                state.NextReliableSequenceNumber = message.ReliableSequenceNumber;
            }
            else if (message.ReliableSequenceNumber == state.NextReliableSequenceNumber) // message is unreliable with no previous reliable one missing
            {
                Debug.Assert(message.Data != null || message.Reassembly != null, "Unreliable messages should never be empty");

                if (message.Data != null) // complete unreliable datagram
                {
                    var peer = state.Peer;
                    peer.Host.Add(new Event(peer, new Data(state.Channel, message.Data)));
                    message.Data = null;

                    state.NextSequenceNumber = message.SequenceNumber + 1;
                }
                else if (message.Reassembly != null) // unreliable fragment
                {
                    // If this fragment is from a more recent datagram than the next expected (that must still be incomplete), drop the previous one.
                    if (state.NextSequenceNumber < message.Reassembly.SequenceNumber - message.Reassembly.Last) 
                        state.NextSequenceNumber = message.SequenceNumber + 1;
                }
                else
                {                    
                    // There's nothing do deliver from an empty unreliable message. 
                    state.NextSequenceNumber = message.SequenceNumber + 1;
                }
            }
            else if (message.ReliableSequenceNumber == state.NextReliableSequenceNumber + 1) // message is the next reliable one
            {
                if (!message.IsReliable) // message is unreliable with a previous reliable one missing (delivery must stall)
                    return false;

                // If message is a complete reliable datagram (empty or not) with no other previous reliable one missing, or the last fragment 
                // of a reliable datagram, it must be delivered. 

                if (message.Data != null) // complete unreliable datagram
                {
                    var peer = state.Peer;
                    peer.Host.Add(new Event(peer, new Data(state.Channel, message.Data)));
                    message.Data = null;
                }
                else if (message.Reassembly != null)
                {
                    // If this is the last fragment then the datagram is complete and can be delivered.
                    if (message.Reassembly.SequenceNumber == message.SequenceNumber)
                    {
                        var peer = state.Peer;
                        peer.Host.Add(new Event(peer, new Data(state.Channel, message.Reassembly.Data)));
                        message.Reassembly.Data = null;
                    }
                }

                state.NextSequenceNumber = message.SequenceNumber + 1;
                state.NextReliableSequenceNumber++;
            }
            else
            {
                return false;
            }
            
            return true;
        }

        #endregion

        /// <summary>
        /// Actively disconnects from the remote host.
        /// <para/>
        /// When <paramref name="force"/> is true all pending events are discarded
        /// and the next event returned by this peer is guaranteed to be a 
        /// <see cref="EventType.Disconnection"/>; otherwise, all buffered events 
        /// are preserved and delivered before <see cref="EventType.Disconnection"/>.
        /// <para/>
        /// In any case the connection will remain in a <see cref="PeerState.Disconnecting"/>
        /// until a <see cref="EventType.Disconnection"/> is raised.
        /// <para/>
        /// This method may be safely called multiple times.
        /// </summary>
        public void Close(bool force = false)
        {
            if (Session.TryDisconnect(out Protocol.State previous))
            {
                // Send a RESET packet unless there's no remote session 
                // yet in which case there's nothing we can do.
                if (previous != Protocol.State.Connecting)
                {
                    Host.Add(new Reset(in EndPoint, Session.Remote,
                                Session.Options.Contains(SessionOptions.Secure) 
                                    ? Host.EncodeReset(Host.UserEncoder, Host.Now(), ref Session)
                                    : Host.EncodeReset(Host.UserEncoder, Host.Now(), Session.Remote)));
                }
            }

            if (State != PeerState.Disconnected && State != PeerState.Disconnecting)
            {
                State = PeerState.Disconnecting;
                Host.Add(new Event(this, PeerReason.Closed));
            }

            // Set termination regardless so subsequent calls to Disconnect may override it 
            // if there's still time (a disconnection event hasn't been retrieved for this connection yet);
            // otherwise, this has no effect.
            Terminated = force;
        }

        internal void Reset(PeerReason reason, PeerMode mode = PeerMode.Passive)
        {
            if (Session.TryDisconnect(out Protocol.State previous))
            {
                // If this is an active reset, send a RESET packet unless there's no 
                // remote session yet in which case there's nothing we can do.
                if (mode == PeerMode.Active)
                {
                    if (previous != Protocol.State.Connecting)
                    {
                        Host.Add(new Reset(in EndPoint, Session.Remote,
                                    Session.Options.Contains(SessionOptions.Secure)
                                        ? Host.EncodeReset(Host.UserEncoder, Host.Now(), ref Session)
                                        : Host.EncodeReset(Host.UserEncoder, Host.Now(), Session.Remote)));
                    }
                }
                else if (previous == Protocol.State.Accepting) // this is a passive peer that was in the accepting state
                {
                    // There's no point in raising a disconnection event 
                    // for an incomplete incoming connection.

                    State = PeerState.Disconnected;
                    StopTime = DateTime.Now;
                    return;
                }

                Host.Add(new Event(this, reason));
            }
        }

        internal void Dispose()
        {
            Session.State = Protocol.State.Disconnected;
            if (Session.Cipher is IDisposable disposable)
                disposable.Dispose();

            Session.Cipher = default;

            State = PeerState.Disconnected;
            StopTime = DateTime.Now;

            if (events != null)
                foreach (var e in events)
                    e.Dispose();

            if (channels != null)
                for (int i = 0; i < channels.Length; ++i)
                    channels[i].Dispose();
            
            RoundTripTime = default;
            RemoteWindow = default;
            RemoteBandwidth = default;
            LinkCapacity = ushort.MaxValue;
            CongestionWindow = default;
            BandwidthWindow = default;
            SendWindow = default;
            BytesInFlight = default;

            transmissionBacklog = 0;
            events = null;
            channels = null;
        }

        private static int Min(int a, int b, int c, int d) => Math.Min(a, Math.Min(b, Math.Min(c, d)));
        private static int Max(int a, int b) => Math.Max(a, b);
    }
}
