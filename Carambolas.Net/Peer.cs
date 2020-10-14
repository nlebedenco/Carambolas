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
        internal Peer(Host host, Protocol.Time time, in IPEndPoint endPoint, PeerMode mode)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
            EndPoint = endPoint;
            State = PeerState.Connecting;
            Mode = mode;

            lastOrdinalWindowTimesAdjustment = time;
        }

        internal Peer(Host host, Protocol.Time time, in IPEndPoint endPoint, PeerMode mode, SessionOptions options, in Key remoteKey)
            : this(host, time, in endPoint, mode)
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
            set => maxBacklog = Max(0, value);
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

                InitialCongestionWindow = (ushort)Min(65535, (Protocol.FastRetransmit.Threshold + 1) * MaxSegmentSize);
            }
        }

        public ushort MaxSegmentSize { get; private set; }
        public ushort MaxFragmentSize { get; private set; }

        private Channel[] channels;
        private Channel.Outbound.Mediator mediator;

        private void SetChannels(int length)
        {

        }

        private byte maxChannel;
        public byte MaxChannel => maxChannel;

        private void SetMaxChannel(byte mtc, Protocol.Time remoteTime)
        {
            maxChannel = mtc;
            var length = mtc + 1;
            channels = new Channel[length];
            for (int i = 0; i < length; ++i)
                channels[i].Initialize((byte)i, remoteTime);
            mediator = new Channel.Outbound.Mediator(length);
        }

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
        /// Receive window advertised to the remote host.
        /// </summary>
        public ushort ReceiveWindow => (ushort)Min(ushort.MaxValue, Host.Downstream.BufferShare);

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
        /// This property must not return a value lower than (<see cref="Protocol.FastRetransmit.Threshold"/> + 1) * <see cref="MaxSegmentSize"/>
        /// </summary>
        internal ushort InitialCongestionWindow { get; private set; }

        /// <summary>
        /// The maximum number of data bytes that can be transmitted in a single burst.
        /// <para/>
        /// (<see cref="SendWindow"/> - <see cref="BytesInFlight"/>) is the maximum number of data bytes that can be transmisted in a frame. 
        /// </summary>
        public int SendWindow { get; private set; }

        /// <summary>
        /// Number of bytes sent but not yet acknowledged.
        /// </summary>
        public int BytesInFlight { get; private set; }  // must be an int because an eventual ping must consume 1 "virtual" and the previous 32767 unreliable messages may have taken up 65535 bytes already.

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
        private int sendCapacity = Protocol.Countdown.Infinite;

        /// <summary>
        /// Number of channels actively retransmitting
        /// </summary>
        private byte retransmittingChannelsCount;

        #endregion

        #region Statistics
       
        public DateTime StartTime { get; internal set; }
        public DateTime StopTime { get; internal set; }

        private TickCounter upticks;

        private Protocol.Time lastOrdinalWindowTimesAdjustment;

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

        private (Command Command, Protocol.Time AcceptanceTime, Acknowledgment Ack, Protocol.Time AcknowledgedTime) control;

        /// <summary>
        /// Asynchronously send a connect packet.
        /// </summary>
        internal void Connect()
        {
            if (control.Command != Command.Connect)
                control.Command = Command.Transmit | Command.Connect;
        }

        /// <summary>
        /// Asynchronously send accept packet.
        /// </summary>
        internal void Accept(Protocol.Time time)
        {
            if (control.Command != Command.Accept)
            {
                control.Command = Command.Transmit | Command.Accept;
                control.AcceptanceTime = time;
            }
        }

        /// <summary>
        /// Asynchronously send acknowledgement for a command.
        /// </summary>
        internal void Acknowledge(Acknowledgment value, Protocol.Time acknowledgedTime)
        {
            if (control.Ack != value)
            {
                control.Ack = value;
                control.AcknowledgedTime = acknowledgedTime;
            }
        }

        /// <summary>
        /// Asynchronously send a cumulative acknowledgement. Latest remote time is updated to be used 
        /// as acknowledged send time in the next ack transmitted.
        /// </summary>
        private void Acknowledge(ref Channel channel, Protocol.Time remoteTime, bool isFirstInPacket)
        {
            if (channel.RX.Ack.Count == 0)
            {
                channel.RX.Ack = (channel.RX.NextSequenceNumber, 1, remoteTime);

                // If channel was not ready to send before, add it to the end of the send list (right before the current channel)
                if ((channel.NextToSend | channel.PreviousToSend) == 0)
                    channel.AddToSendListBefore(ref channels[currentChannelIndex]);
            }
            else
            {
                if (channel.RX.Ack.SequenceNumber != channel.RX.NextSequenceNumber)
                {
                    channel.RX.Ack.SequenceNumber = channel.RX.NextSequenceNumber;
                    channel.RX.Ack.Count = 1;
                }
                else if (isFirstInPacket)
                {
                    // Dup acks must be counted by packet. 
                    // It makes no sense to count dup acks per message. A packet may contain several messages
                    // but they all share the same fate as of their containing packet. Counting dup acks by message 
                    // would only produce distortions and negatively affect fast retransmissions.
                    channel.RX.Ack.Count++;
                }

                if (channel.RX.Ack.LatestRemoteTime < remoteTime)
                    channel.RX.Ack.LatestRemoteTime = remoteTime;
            }
        }

        private void OnAckReceived(Protocol.Time time, Protocol.Time acknowledgedTime)
        {
            var rtt = (uint)(time - acknowledgedTime);

            // Update roundtrip time following Jacobson/Karels's algorithm
            if (RoundTripTime == 0)
            {
                RoundTripTime = Max(1, rtt);
                roundTripTimeVariance = rtt >> 1;
            }
            else
            {
                RoundTripTime = Max(1, (7 * RoundTripTime + rtt) >> 3);
                roundTripTimeVariance = (3 * roundTripTimeVariance + (uint)Math.Abs(RoundTripTime - (long)rtt)) >> 2;
            }

            ackTimeout = Host.AckTimeoutClamp(RoundTripTime + (roundTripTimeVariance << 2));
        }

        private void OnAckMatched(Protocol.Time time, Protocol.Time acknowledgedTime)
        {
            OnAckReceived(time, acknowledgedTime);

            ackFailCounter = 0;
            ackDeadline = default;
            connDeadline = default;
            idleDeadline = time + Host.IdleTimeout;

            sendCapacity = Protocol.Countdown.Infinite;
        }

        private void OnAckTimeout(Protocol.Time time)
        {
            if (ackFailCounter == 1) // First ack timeout
            {
                // If not retransmitting, reduce the estimated link capacity
                if (retransmittingChannelsCount == 0)
                    LinkCapacity = (ushort)Max(CongestionWindow >> 1, InitialCongestionWindow);

                // Reset the congestion window for a new slow start, 
                // this value should never be lower than one MaxSegmentSize
                CongestionWindow = MaxSegmentSize;
            }
            else if (ackFailCounter == 2) // Second consecutive ack timeout
            {
                // Reset RTT estimate as it's probably too wrong now
                RoundTripTime = 0;
            }

            // If there's a control command waiting for an ack, flag it for retransmission otherwise set every channel to retransmit. 
            if (control.Command != default)
            {
                control.Command |= Command.Transmit;
            }
            else
            {
                // Check the channels in the send list and increment the counter if a channel is set to retransmit.
                ref var channel = ref channels[currentChannelIndex];
                do
                {
                    // If Messages.First is null, Transmit must also be null; otherwise there's something to retransmit only if Transmit 
                    // is past Messages.First (hence different).
                    if (channel.TX.Messages.First != channel.TX.Transmit)
                    {
                        if (channel.TX.Retransmit == null)
                        {
                            retransmittingChannelsCount++;
                            channel.TX.Ack.Last = channel.TX.NextSequenceNumber - 1;
                            channel.TX.Ack.Count = 1;

                            // If the channel was not ready to send before, add it to the end of the send list (right before the current channel)
                            if ((channel.NextToSend | channel.PreviousToSend) == 0)
                                channel.AddToSendListBefore(ref channels[currentChannelIndex]);
                        }

                        channel.TX.Retransmit = channel.TX.Messages.First;
                    }

                    channel = ref channels[channel.NextToSend];
                }
                while (channel.Index != currentChannelIndex);

                // This could be a congestion so restrict sending to at most 1 data packet 
                // (retransmission or not) until either ack is received or another timeout occurs.
                sendCapacity = 1;
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

            // Don't assign MaxChannel here yet. Channels should only be set after the connection is established to avoid unecessary allocations.

            CongestionWindow = InitialCongestionWindow;
            RemoteWindow = ushort.MaxValue;

            StartTime = DateTime.Now;

            Connect();
        }

        internal void OnAccepting(Protocol.Time time, Protocol.Time remoteTime, uint remoteSession, in Protocol.Message.Connect connect)
        {
            Session.State = Protocol.State.Accepting;
            Session.Local = (uint)time;
            Session.Remote = remoteSession;

            SetMaxChannel(connect.MaximumTransmissionChannel, remoteTime);

            RemoteBandwidth = connect.MaximumBandwidth >> 3;
            CongestionWindow = InitialCongestionWindow;

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

            SetMaxChannel(connect.MaximumTransmissionChannel, remoteTime);

            MaxTransmissionUnit = connect.MaximumTransmissionUnit;            
            RemoteBandwidth = connect.MaximumBandwidth >> 3;
            CongestionWindow = InitialCongestionWindow;

            Accept(LatestRemoteTime);
        }

        internal void OnConnected(Protocol.Time time, Protocol.Time remoteTime, uint remoteSession, in Protocol.Message.Accept accept)
        {
            Session.State = Protocol.State.Connected;
            Session.Remote = remoteSession;

            upticks = new TickCounter(TickCounter.GetTicks());

            SetMaxChannel(accept.MaximumTransmissionChannel, remoteTime);

            MaxTransmissionUnit = accept.MaximumTransmissionUnit;
            RemoteBandwidth = accept.MaximumBandwidth >> 3;
            CongestionWindow = InitialCongestionWindow;

            Acknowledge(Acknowledgment.Accept, LatestRemoteTime);

            control.Command = default;
            OnAckMatched(time, accept.AcknowledgedTime);
        }

        internal void OnAccepted(Protocol.Time time, Protocol.Time acknowledgedTime)
        {
            Session.State = Protocol.State.Connected;

            upticks = new TickCounter(TickCounter.GetTicks());
        
            control.Command = default;
            OnAckMatched(time, acknowledgedTime);
        }

        private void OnUpdate(Protocol.Time time)
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
        }

        internal void OnConnectingUpdate(Protocol.Time time) => OnUpdate(time);

        internal void OnConnectedUpdate(Protocol.Time time)
        {
            OnUpdate(time);

            // Adjust sequence window times that remained inactive for more than a packet lifetime in all channels.
            // There's no need to do this very often because any packet older than Protocol.Packet.Lifetime is going to be discarded
            // before any message gets processed anyway. But it must still be executed at least once every 24 days because in the 
            // unlikely event a connection is mantained for more than 2147483648ms (aprox 24 days 20 hours and 24 minutes), at least 
            // one channel may sit unused for the whole duration and the ordinal window times stored could fall behind the current 
            // remote time by more than the time window resulting in ambiguity.  
            if (lastOrdinalWindowTimesAdjustment - time > Protocol.Ordinal.Window.Times.MaxAdjustmentTime)
            {
                lastOrdinalWindowTimesAdjustment = time;
                foreach (var channel in channels)
                    channel.Adjust(LatestRemoteTime - Protocol.Packet.LifeTime);
            }

            // Bandwidth window is the maximum number of bytes that can be sent in a burst and still keep the output rate less than or equal to 
            // the remote bandwidth. Protocol overhead is disconsidered hence why this.datatSent is used instead of this.bytesSent.
            // There's no need to use Interlocked.Read(ref dataSent) here because this is the same thread where the field is modified.
            var bwnd = (RemoteBandwidth * upticks.ElapsedMilliseconds() / 1000) - dataSent;

            // Maximum number of user data bytes that can be in flight this frame.
            SendWindow = (ushort)Min(bwnd, Max(MaxSegmentSize, Min(Host.Upstream.BufferShare, CongestionWindow, RemoteWindow)));

            // Flush messages sent by the user thread, if any, and updates the list of channels to send.
            mediator.Flush(channels, ref channels[currentChannelIndex]);
        }

        #region Data Sending

        /// <summary>
        /// Current channel to process in <see cref="OnConnectedSend"/>. Only channels linked by <see cref="Channel.NextToSend"/> 
        /// are considered for transmission. This serves to reduce iteration time when a peer has a large number of channels 
        /// (> 16) that are sparsely used. Channel 0 serves as fallback in case there's no channel ready to send.
        /// </summary>
        private byte currentChannelIndex;

        /// <summary>
        /// True if peer must be pinged.
        /// </summary>
        private bool ping;        

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
                var expiration = unchecked(Host.Timestamp() + ((qos.Timelimit - 1) & int.MaxValue));
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
                    fraglen = (ushort)Min(length, MaxFragmentSize);
                    var message = CreateFragment(Host.UserEncoder, channel, qos.Delivery, expiration, fragindex, seglen, data, offset, fraglen);
                    message.AddAfter(last);
                    last = message;
                    fragindex++;
                }
                while (fragindex <= fraglast);

                mediator.Send(ref channels[channel], first, last);
            }
            else
            {
                var message = CreateSegment(Host.UserEncoder, channel, qos.Delivery, unchecked(Host.Timestamp() + ((qos.Timelimit - 1) & int.MaxValue)), data, offset, (ushort)length);
                mediator.Send(ref channels[channel], message);
            }            
        }

        /// <summary>
        /// Send connection handshake packets. 
        /// Returns true if a packet was written and must be transmitted; otherwise false (yielding to the next peer).
        /// </summary>
        internal bool OnConnectingSend(uint time, BinaryWriter packet)
        {
            var created = false;

            // Send control commands
            switch (control.Command)
            {
                case Command.Transmit | Command.Connect:

                    control.Command &= ~Command.Transmit;
                    created = true;

                    // There's no need to reserve space for the checksum because a CONNECT 
                    // (secure or insecure) must be way below the minimum MTU.
                    packet.Reset(MaxTransmissionUnit);

                    if (Session.Options.Contains(SessionOptions.Secure))
                    {
                        packet.UncheckedWrite(time);
                        packet.UncheckedWrite(Protocol.PacketFlags.Secure | Protocol.PacketFlags.Connect);
                        packet.UncheckedWrite(Session.Local);
                        packet.UncheckedWrite(Host.MaxTransmissionUnit);
                        packet.UncheckedWrite(Host.MaxChannel);
                        packet.UncheckedWrite(Host.MaxBandwidth);
                        packet.UncheckedWrite(in Host.Keys.Public);
                    }
                    else
                    {
                        packet.UncheckedWrite(time);
                        packet.UncheckedWrite(Protocol.PacketFlags.Connect);
                        packet.UncheckedWrite(Session.Local);
                        packet.UncheckedWrite(Host.MaxTransmissionUnit);
                        packet.UncheckedWrite(Host.MaxChannel);
                        packet.UncheckedWrite(Host.MaxBandwidth);
                    }

                    packet.UncheckedWrite(Protocol.Packet.Insecure.Checksum.Compute(packet.Buffer, packet.Offset, packet.Count));

                    if (ackDeadline == null)
                        ackDeadline = time + ackTimeout;

                    if (connDeadline == null)
                        connDeadline = time + connTimeout;

                    break;
                case Command.Transmit | Command.Accept:
                    control.Command &= ~Command.Transmit;
                    created = true;

                    // There's no need to reserve space for the checksum (or nonce/mac) because an ACCEPT
                    // (secure or insecure) must be way below the minimum MTU.
                    packet.Reset(MaxTransmissionUnit);

                    if (Session.Options.Contains(SessionOptions.Secure))
                    {
                        packet.Write(time);
                        packet.UncheckedWrite(Protocol.PacketFlags.Secure | Protocol.PacketFlags.Accept);
                        packet.UncheckedWrite(Session.Local);
                        packet.UncheckedWrite(Host.MaxTransmissionUnit);
                        packet.UncheckedWrite(Host.MaxChannel);
                        packet.UncheckedWrite(Host.MaxBandwidth);
                        packet.UncheckedWrite(control.AcceptanceTime);

                        var (buffer, offset, position, count) = (packet.Buffer, packet.Offset, packet.Position, sizeof(ushort));
                        packet.UncheckedWrite(ReceiveWindow);

                        var nonce64 = ++Session.Nonce;
                        var nonce = new Nonce(time, nonce64);

                        Session.Cipher.EncryptInPlace(buffer, position, count, in nonce);
                        Session.Cipher.Sign(buffer, offset, position - offset, count, in nonce, out Mac mac);

                        packet.UncheckedWrite(in Host.Keys.Public);
                        packet.UncheckedWrite(nonce64);
                        packet.UncheckedWrite(in mac);
                    }
                    else
                    {
                        packet.Write(time);
                        packet.UncheckedWrite(Protocol.PacketFlags.Accept);
                        packet.UncheckedWrite(Session.Local);
                        packet.UncheckedWrite(Host.MaxTransmissionUnit);
                        packet.UncheckedWrite(Host.MaxChannel);
                        packet.UncheckedWrite(Host.MaxBandwidth);
                        packet.UncheckedWrite(control.AcceptanceTime);
                        packet.UncheckedWrite(ReceiveWindow);
                        packet.UncheckedWrite(Session.Remote);
                        packet.UncheckedWrite(Protocol.Packet.Insecure.Checksum.Compute(packet.Buffer, packet.Offset, packet.Count));
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

            return created;
        }

        /// <summary>
        /// Send data packets.
        /// Returns true if a packet was written and must be transmitted; otherwise false (yielding to the next peer).
        /// </summary>
        internal bool OnConnectedSend(uint time, BinaryWriter packet)
        {
            var created = false;

            void EnsureDataPacketIsCreated()
            {
                if (!created)
                {
                    created = true;

                    if (Session.Options.Contains(SessionOptions.Secure))
                    {
                        packet.Reset(MaxTransmissionUnit - (Protocol.Packet.Secure.N64.Size + Protocol.Packet.Secure.Mac.Size));
                        packet.UncheckedWrite(time);
                        packet.UncheckedWrite(Protocol.PacketFlags.Secure | Protocol.PacketFlags.Data);
                        packet.UncheckedWrite(ReceiveWindow);
                    }
                    else
                    {
                        packet.Reset(MaxTransmissionUnit - Protocol.Packet.Insecure.Checksum.Size);
                        packet.UncheckedWrite(time);
                        packet.UncheckedWrite(Protocol.PacketFlags.Data);
                        packet.UncheckedWrite(Session.Local);
                        packet.UncheckedWrite(ReceiveWindow);
                    }
                }
            }

            // Send control acks
            switch (control.Ack)
            {
                case Acknowledgment.Accept:
                    control.Ack = default;

                    EnsureDataPacketIsCreated();
                    packet.UncheckedWrite(Protocol.MessageFlags.Ack | Protocol.MessageFlags.Accept);
                    packet.UncheckedWrite(control.AcknowledgedTime);

                    break;
                default:
                    break;
            }
            
            if((uint)sendCapacity > 0) // Send both data and acks
            {
                var transmitted = 0;

                // If peer has one or more channels that need to retransmit data
                if (retransmittingChannelsCount > 0)
                {
                    // At this point BytesInFlight must be greater than 0 and at least one channel must have the Retransmit pointer not null.
                    Debug.Assert(BytesInFlight > 0, $"{nameof(BytesInFlight)} must be greater than zero when there are restransmitting channels ({retransmittingChannelsCount})");

                    // Even if in the end all retransmissions are void (only unreliable messages were waiting) 
                    // an Enquire should still be transmitted so it's safe to ensure a packet header here. 
                    EnsureDataPacketIsCreated();

                    do
                    {
                        ref var channel = ref channels[currentChannelIndex];

                        if (channel.RX.Ack.Count > 0) // Channel must send ack first.
                        {
                            // It's better to spread ACKs over multiple packets ahead of their own channel streams so that we don't 
                            // always end up with packets full of acks that may compromise several channels at once if lost.
                            if (!packet.TryWrite(new Protocol.Message.Ack(currentChannelIndex, channel.RX.Ack.Count, channel.RX.NextSequenceNumber, channel.RX.LastSequenceNumber, channel.RX.Ack.LatestRemoteTime)))
                                goto DoneSendingData;

                            channel.RX.Ack.Count = 0;
                        }

                        var retransmit = channel.TX.Retransmit;                        
                        if (retransmit != null) // Channel has something to retransmit.  
                        {
                            var transmit = channel.TX.Transmit; // transmit is always ahead of the retransmit pointer (pointing to the next message that has not been transmitted yet)
                            while (true)
                            {
                                // If no more data to retransmit in this channel (compare to end first because it may be null)
                                if (retransmit == transmit || retransmit.SequenceNumber > channel.TX.Ack.Last)
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
                                    Debug.Assert(BytesInFlight >= retransmit.Payload, "Invalid bytes in flight");
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

                        var nextChannelIndex = channel.NextToSend;
                        // Any eventual ack for this channel must have been transmitted by now.
                        // If there's nothing else to transmit remove the channel.
                        if (channel.TX.Transmit == null && channel.TX.Retransmit == null)
                            channel.RemoveFromSendList(ref channels[channel.PreviousToSend], ref channels[channel.NextToSend]);

                        currentChannelIndex = nextChannelIndex;
                    }
                    while (retransmittingChannelsCount > 0);// Repeat until no more channels need to retransmit

                    // If all messages have been dropped check that remote host is still alive
                    if (BytesInFlight == 0)
                        ping = true;
                }

                // Send new data after all retransmissions have been processed.        
                var endChannelIndex = currentChannelIndex;
                do
                {
                    ref var channel = ref channels[currentChannelIndex];

                    if (channel.RX.Ack.Count > 0) // channel must send ACK
                    {
                        EnsureDataPacketIsCreated();

                        // It's better to spread ACKs over multiple packets ahead of their own channel streams so that we don't 
                        // always end up with packets full of acks that may compromise several channels at once if lost.
                        if (!packet.TryWrite(new Protocol.Message.Ack(currentChannelIndex, channel.RX.Ack.Count, channel.RX.NextSequenceNumber, channel.RX.LastSequenceNumber, channel.RX.Ack.LatestRemoteTime)))
                            goto DoneSendingData;

                        channel.RX.Ack.Count = 0;
                    }

                    var transmit = channel.TX.Transmit;
                    if (transmit != null) // There's a message to transmit                                                                       
                    {
                        do
                        {
                            // If either the send window limit or the sequence window limit has been reached then stall (break to next channel).
                            // Every data message in flight must be consuming at least 1 byte of the send window. In the worst case the number 
                            // of messages in flight is going to be equal to Protocol.Ordinal.Window.Size (Protocol.Ordinal.Window.Size-1 messages 
                            // containing a single byte of user data and 1 reliable ping message taking up 1 "virtual" byte). At full occupation 
                            // there will be Protocol.Ordinal.Window.Size-1 messages taking up 65535 bytes in total and 1 "virtual" byte (extra) 
                            // for the ping so BytesInFlight may actually reach 65536 in this particular circunstance.
                            //
                            // Note that unreliable messages should not be dropped due to the lack of send window or sequence window space. 
                            // Otherwise all datagrams larger than the send window are going to be ultimately lost (last fragments dropped). 
                            // And if the user never sends a datagram smaller than the send window *several broken datagrams* will have to be
                            // partially acknowledged and buffered by the receiver until the send window has grown large enough to accommodate a
                            // complete datagram. Large datagrams will also be wasted if they go across the upper edge of the sequence window. 
                            // And even worst in some cases only the last fragment of a datagram (or a short succession of datagrams) would be 
                            // transmitted because their first fragments would not fit in the send window (SendWindow - BytesInFlight < MaxFragmentSize)
                            // but the last fragment would. Only after the first complete datagram (or ping) arrives is when the receiver would be 
                            // able to discard all other partial datagrams previously buffered and deliver some data to the application.                                
                            if ((SendWindow - BytesInFlight < transmit.Payload)
                             || (channel.TX.NextSequenceNumber == channel.TX.Ack.Next + (Protocol.Ordinal.Window.Size - 1))) // dont' continue if next SEQ to transmit is the last one of the window as it's reserved for a ping. 
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
                                channel.TX.Transmit = transmit;
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
                                    var position = packet.Count + 2;
                                    packet.UncheckedWrite(transmit.Encoded, 0, length);
                                    packet.UncheckedOverwrite(seq, position);
                                    packet.UncheckedOverwrite(channel.TX.NextReliableSequenceNumber, position + 2);
                                    // Unreliable messages are not retransmitted so the encoded message may now be discarded.
                                    transmit.Encoded.Dispose();
                                    transmit.Encoded = null;
                                    break;
                                case Protocol.Delivery.Semireliable:
                                    transmit.Encoded.UncheckedOverwrite(seq, 2);
                                    transmit.Encoded.UncheckedOverwrite(channel.TX.NextReliableSequenceNumber, 4);
                                    packet.UncheckedWrite(transmit.Encoded, 0, length);
                                    break;
                                case Protocol.Delivery.Reliable:
                                    transmit.Encoded.UncheckedOverwrite(seq, 2);
                                    transmit.Encoded.UncheckedOverwrite(++channel.TX.NextReliableSequenceNumber, 4);
                                    packet.UncheckedWrite(transmit.Encoded, 0, length);
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

                        channel.TX.Transmit = transmit;
                    }

                    var nextChannelIndex = channel.NextToSend;
                    // Any eventual ack for this channel must have been transmitted by now.
                    // There must be nothing to RETRANSMIT for this channel at this point because retransmissions have priority over new transmissions. 
                    // If there's nothing else to transmit remove the channel (there may still be something to transmit that didn't fit in the send window for this frame). 
                    if (channel.TX.Transmit == null)
                        channel.RemoveFromSendList(ref channels[channel.PreviousToSend], ref channels[channel.NextToSend]);

                    currentChannelIndex = nextChannelIndex;
                }
                while (currentChannelIndex != endChannelIndex);

                DoneSendingData:
                {
                    // A ping may be required because of an idle timeout or due to having all 
                    // retransmissions discarded in a row (for being unreliable after an ack timeout).
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
                            encoder.Ensure(sizeof(Protocol.MessageFlags) + Protocol.Message.Segment.MinSize);
                            encoder.UncheckedWrite(Protocol.MessageFlags.Reliable | Protocol.MessageFlags.Data | Protocol.MessageFlags.Segment);
                            encoder.UncheckedWrite((byte)0); // CH

                            Channel.Outbound.Message transmit;
                            if (packet.Available > Protocol.Message.Segment.MinSize)
                            {
                                var seq = channel.TX.NextSequenceNumber++;
                                var rsn = ++channel.TX.NextReliableSequenceNumber;

                                encoder.UncheckedWrite(seq);
                                encoder.UncheckedWrite(rsn);
                                encoder.UncheckedWrite((ushort)0);

                                transmit = CreatePing(encoder);
                                
                                Debug.Assert(channel.TX.Messages.IsEmpty, "Channel TX buffer should be empty");
                                channel.TX.Messages.AddLast(transmit);

                                packet.UncheckedWrite(transmit.Encoded, 0, transmit.Encoded.Length);

                                transmit.SequenceNumber = seq;
                                transmit.FirstSendTime = time;
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
                                encoder.UncheckedWrite(0, sizeof(ushort) + sizeof(ushort) + sizeof(ushort)); // SEQ:RSN:LEN

                                transmit = CreatePing(encoder);

                                Debug.Assert(channel.TX.Messages.IsEmpty, "Channel TX buffer should be empty");
                                channel.TX.Messages.AddLast(transmit);

                                transmit.FirstSendTime = unchecked(time + int.MaxValue); // "infinite" time limit to transmit
                            }

                            channel.TX.Transmit = transmit;
                            // If channel was not ready to send before, add it to the end of the send list (right before the current channel)
                            if ((channel.NextToSend | channel.PreviousToSend) == 0)
                                channel.AddToSendListBefore(ref channels[currentChannelIndex]);
                        }
                    }

                    if (transmitted > 0) // This packet contains user data (or a ping)
                    {
                        // Decrement data packet countdown if neither finished nor infinite.
                        if (sendCapacity > 0)
                            sendCapacity--;

                        // Start ack timer if not started yet
                        if (ackDeadline == null)
                            ackDeadline = time + ackTimeout;

                        // Start connection timer if not started yet                                                 
                        if (connDeadline == null)
                            connDeadline = time + connTimeout;
                    }
                }
            }
            else // No more packets with user data allowed but acks may still need to be transmitted.
            {
                var endChannelIndex = currentChannelIndex;                
                do
                {
                    ref var channel = ref channels[currentChannelIndex];

                    if (channel.RX.Ack.Count > 0) // Channel must send ACK
                    {
                        EnsureDataPacketIsCreated();

                        if (!packet.TryWrite(new Protocol.Message.Ack(currentChannelIndex, channel.RX.Ack.Count, channel.RX.NextSequenceNumber, channel.RX.LastSequenceNumber, channel.RX.Ack.LatestRemoteTime)))
                            break;

                        channel.RX.Ack.Count = 0;
                    }

                    var nextChannelIndex = channel.NextToSend;
                    // If there's nothing else to transmit remove the channel
                    if (channel.TX.Transmit == null && channel.TX.Retransmit == null)
                        channel.RemoveFromSendList(ref channels[channel.PreviousToSend], ref channels[channel.NextToSend]);

                    currentChannelIndex = nextChannelIndex;
                }
                while (currentChannelIndex != endChannelIndex);                
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
                    packet.UncheckedWrite(nonce64);
                    packet.UncheckedWrite(in mac);
                }
                else
                {
                    var (buffer, offset, count) = (packet.Buffer, packet.Offset, packet.Count);
                    var crc = Protocol.Packet.Insecure.Checksum.Compute(buffer, offset, count);
                    packet.Expand(Protocol.Packet.Insecure.Checksum.Size);
                    packet.UncheckedWrite(crc);
                }
            }

            return created;
        }

        private Channel.Outbound.Message CreateSegment(BinaryWriter encoder, byte channel, Protocol.Delivery delivery, Protocol.Time expiration, byte[] data, int offset, ushort length)
        {
            encoder.Reset();
            encoder.Ensure(sizeof(Protocol.MessageFlags) + Protocol.Message.Segment.MinSize);

            var flags = delivery == Protocol.Delivery.Reliable
                    ? Protocol.MessageFlags.Reliable | Protocol.MessageFlags.Data | Protocol.MessageFlags.Segment
                    : Protocol.MessageFlags.Data | Protocol.MessageFlags.Segment;

            encoder.UncheckedWrite(flags);
            encoder.UncheckedWrite(channel);

            // Actual SEQ and RSN values must be assigned on transmit by the worker thread.
            // This is to avoid locking the generators as they would represent a shared resource between 
            // the user and worker threads. The worker thread may need to write update the generators to 
            // inject a ping in the output queue.
            encoder.UncheckedWrite(0, sizeof(ushort) + sizeof(ushort)); // SEQ:RSN
            encoder.UncheckedWrite(length);
            encoder.UncheckedWrite(data, offset, length);

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
            encoder.Ensure(sizeof(Protocol.MessageFlags) + Protocol.Message.Fragment.MinSize);

            var flags = delivery == Protocol.Delivery.Reliable
                ? Protocol.MessageFlags.Reliable | Protocol.MessageFlags.Data | Protocol.MessageFlags.Fragment
                : Protocol.MessageFlags.Data | Protocol.MessageFlags.Fragment;

            encoder.UncheckedWrite(flags);
            encoder.UncheckedWrite(channel);

            // Note that actual SEQ and RSN values must be assigned on transmit by the worker thread.
            // This is to avoid locking the generators as they would represent a shared resource between 
            // the user and worker threads. The worker thread needs to update (write) the generators to 
            // inject a ping in the output queue.
            encoder.UncheckedWrite(0, sizeof(ushort) + sizeof(ushort)); // SEQ:RSN
            encoder.UncheckedWrite(seglen);
            encoder.UncheckedWrite(fragindex);            
            encoder.UncheckedWrite(length);
            encoder.UncheckedWrite(data, offset, length);

            Host.Allocate(out Memory encoded);
            encoded.CopyFrom(encoder.Buffer, encoder.Offset, (ushort)encoder.Count);

            Host.Allocate(out Channel.Outbound.Message message);
            message.Delivery = delivery;
            message.FirstSendTime = expiration;
            message.Payload = length;
            message.Encoded = encoded;

            return message;
        }

        private Channel.Outbound.Message CreatePing(BinaryWriter encoder)
        {
            Host.Allocate(out Memory encoded);
            encoded.CopyFrom(encoder.Buffer, encoder.Offset, (ushort)encoder.Count);

            Host.Allocate(out Channel.Outbound.Message message);
            message.Delivery = Protocol.Delivery.Reliable;

            // A ping must take up at least one byte so we can avoid having to
            // handle pings differently from other reliable messages.
            message.Payload = 1;
            message.Encoded = encoded;

            return message;
        }      

        #endregion

        #region Data Receiving 

        internal void OnReceive(Protocol.Time time, Protocol.Time remoteTime, in Protocol.Message.Ack ack)
        {
            if (BytesInFlight == 0)
                return;

            ref var channel = ref channels[ack.Channel];

            if ((channel.TX.Messages.First == channel.TX.Transmit)                                                                  // Obsolete ack (there's nothing to ack)
                || (ack.AcknowledgedTime < channel.TX.Messages.First.FirstSendTime)                                              // Old ack
                || (ack.Last < ack.Next || ack.Next > channel.TX.NextSequenceNumber || ack.Last > channel.TX.NextSequenceNumber)   // Invalid ack
                || (ack.Next < channel.TX.Ack.Next))                                                                             // Obsolete ack
            {
                return;
            }

            if (ack.Next == channel.TX.Ack.Next) // Duplicate ack/gap
            {
                // Update RTT and ack timeout
                OnAckReceived(time, ack.AcknowledgedTime);
                // Release any artificial send limit imposed by a previous timeout
                sendCapacity = Protocol.Countdown.Infinite;
                // Reset ack failures
                ackFailCounter = 0;
                // Restart timers
                ackDeadline = time + ackTimeout;
                connDeadline = time + connTimeout;

                if (channel.TX.LatestAckRemoteTime < remoteTime) // Latest ack/gap
                {
                    // Update send time of the latest ack
                    channel.TX.LatestAckRemoteTime = remoteTime;
                    // Update gap information (last message to retransmit)
                    channel.TX.Ack.Last = ack.Last;
                    // Increment the dup ack counter used to trigger an early retransmit
                    channel.TX.Ack.Count += ack.Count;
                }
                else if (ack.Next != ack.Last) // Late gap
                {
                    // If issued after the last (re)transmission but arrived out of order then
                    // increment the dup ack counter used to trigger an early retransmit        
                    if (ack.AcknowledgedTime >= channel.TX.Messages.First.LatestSendTime)
                        channel.TX.Ack.Count += ack.Count;
                }
            }
            else // New ack
            {
                // Update RTT and ack timeout
                OnAckReceived(time, ack.AcknowledgedTime);
                // Release any artificial send limit imposed by a previous timeout
                sendCapacity = Protocol.Countdown.Infinite;
                // Reset ack failures
                ackFailCounter = 0;

                // Release acknowledged messages and compute the total acknowleged size
                var asize = default(ushort);
                var message = channel.TX.Messages.First;
                while (message != channel.TX.Transmit && message.SequenceNumber < ack.Next)
                {
                    asize += message.Payload;

                    // Calling message.Dispose() directly here instead of channel.TX.BUFFER.Dispose() 
                    // because we know how channel.TX.First and Last are going to end up.
                    var next = message.Next;
                    message.Dispose();
                    message = next;
                }

                // Assigning channel.TX.First also fixes channel.TX.Last if needed (side effect)
                channel.TX.Messages.First = message;

                // Return space to the transmit backlog
                DecrementTransmissionBacklog(asize);

                // If more than half of the congestion window was used  increase it in either 
                // slow start or avoidance mode depending on the estimated link capacity
                if (BytesInFlight > (CongestionWindow >> 1))
                    CongestionWindow = (ushort)Min(ushort.MaxValue, CongestionWindow + ((CongestionWindow < LinkCapacity) ? asize : 1));

                // Acknowledged bytes are not in flight anymore
                Debug.Assert(BytesInFlight >= asize, "Incorrect bytes in flight");
                BytesInFlight -= asize;

                if (BytesInFlight == 0) // all messages have been acknowledged                                                                         
                {
                    // Stop timers
                    ackDeadline = default;
                    connDeadline = default;
                    // Start enquire timer
                    idleDeadline = time + Host.IdleTimeout;

                    // If Channel was retransmitting
                    if (channel.TX.Retransmit != null)
                    {
                        // Reset the retransmission pointer 
                        channel.TX.Retransmit = null;
                        // Decrement counter of channels actively retransmitting 
                        retransmittingChannelsCount--;
                    }
                }
                else // some messages still remain unacknowledged   
                {
                    // Restart timers
                    ackDeadline = time + ackTimeout;
                    connDeadline = time + connTimeout;

                    if (channel.TX.Retransmit != null) // Channel was retransmitting
                    {
                        if (ack.Next > channel.TX.Ack.Last) // Gap has been completely acknowledged 
                        {
                            // Reset the retransmission pointer 
                            channel.TX.Retransmit = null;
                            // Decrement counter of channels actively retransmitting 
                            retransmittingChannelsCount--;
                        }
                        else
                        {
                            // If the gap has been updated past the retransmission pointer
                            // the message it was pointing to must have been disposed. 
                            // A message may have a payload of 0 bytes only when disposed.
                            if (channel.TX.Retransmit.Payload == 0)
                                channel.TX.Retransmit = channel.TX.Messages.First;
                        }
                    }
                }

                // This is guaranteed the latest ack source time and most up-to-date ack/gap information
                channel.TX.LatestAckRemoteTime = remoteTime;
                channel.TX.Ack = (ack.Next, ack.Last, ack.Count);
            }

            // Trigger a fast retransmission if channel is not retransmitting yet 
            // and there is a gap wtih enough packets received after.
            if (channel.TX.Retransmit == null && channel.TX.Ack.Count >= Protocol.FastRetransmit.Threshold)
            {
                // Increment counter of channels actively retransmmitting
                retransmittingChannelsCount++;

                // Set the retransmit pointer to the first message
                channel.TX.Retransmit = channel.TX.Messages.First;
                
                // If channel was not ready to send before, add it to the end of the send list (right before the current channel)
                if ((channel.NextToSend | channel.PreviousToSend) == 0)
                    channel.AddToSendListBefore(ref channels[currentChannelIndex]);

                // Update statistics
                Interlocked.Increment(ref fastRetransmissions);

                // If this is the first channel that started retransmitting adjust the estimated link capacity
                if (retransmittingChannelsCount == 1)
                    LinkCapacity = (ushort)Max(CongestionWindow >> 1, InitialCongestionWindow);
            }
        }

        internal void OnReceive(Protocol.Time time, Protocol.Time remoteTime, bool reliable, bool isFirstInPacket, in Protocol.Message.Segment segment)
        {
            ref var channel = ref channels[segment.Channel];

            if (segment.SequenceNumber == channel.RX.NextSequenceNumber) // message is the next expected (either reliable or unreliable)
            {
                var (xseq, window) = GetCrossSequenceNumberAndStaticWindow(channel.RX.CrossSequenceNumber, 1);

                // Discard if message does not belong to this incarnation of the sliding window
                if (remoteTime <= channel.RX.NextRemoteTimes[window])
                    return;

                channel.RX.UpdateNextRemoteTime(window, remoteTime);
                
                if (segment.Data.Count > 0)
                {
                    Interlocked.Add(ref dataReceived, segment.Data.Count);

                    Host.Allocate(out Memory memory);
                    memory.CopyFrom(in segment.Data);

                    Host.Add(new Event(this, new Data(channel.Index, memory)));
                }

                channel.RX.CrossSequenceNumber = xseq;
                channel.RX.NextSequenceNumber = segment.SequenceNumber + 1;

                if (reliable)
                {
                    channel.RX.NextReliableSequenceNumber = segment.ReliableSequenceNumber;

                    // Deliver any messages stalled by this segment
                    Deliver(ref channel);
                }

                channel.RX.UpdateLastSequenceNumber();
                channel.RX.UpdateLowestSequenceNumber();

                Acknowledge(ref channel, remoteTime, isFirstInPacket);
            }
            else if (segment.SequenceNumber > channel.RX.NextSequenceNumber) // message is ahead of the next expected
            {
                var (xseq, window) = GetCrossSequenceNumberAndStaticWindow(channel.RX.CrossSequenceNumber, (ushort)unchecked((ushort)(segment.SequenceNumber - channel.RX.NextSequenceNumber) + 1));

                // Discard if message does not belong to this incarnation of the sliding window
                if (remoteTime <= channel.RX.NextRemoteTimes[window])
                    return;

                if (segment.ReliableSequenceNumber == channel.RX.NextReliableSequenceNumber) // unreliable message arriving ahead of other unreliable ones (because the ReliableSequenceNumber is still the same)
                {
                    channel.RX.UpdateNextRemoteTime(window, remoteTime);                    

                    if (segment.Data.Count > 0)
                    {
                        Interlocked.Add(ref dataReceived, segment.Data.Count);

                        Host.Allocate(out Memory memory);
                        memory.CopyFrom(in segment.Data);

                        Host.Add(new Event(this, new Data(channel.Index, memory)));
                    }

                    // Remove previous unreliable fragments
                    channel.RX.Messages.RemoveAndDisposeBefore(segment.SequenceNumber);

                    // Remove previous reassemblies
                    channel.RX.Reassemblies.RemoveAndDisposeBefore(segment.SequenceNumber);

                    channel.RX.CrossSequenceNumber = xseq;
                    channel.RX.NextSequenceNumber = segment.SequenceNumber + 1;
                    channel.RX.NextReliableSequenceNumber = segment.ReliableSequenceNumber;

                    channel.RX.UpdateLastSequenceNumber();
                    channel.RX.UpdateLowestSequenceNumber();

                    Acknowledge(ref channel, remoteTime, isFirstInPacket);
                }
                else if (reliable && segment.ReliableSequenceNumber == channel.RX.NextReliableSequenceNumber + 1) // message is the next reliable expected
                {
                    channel.RX.UpdateNextRemoteTime(window, remoteTime);

                    if (segment.Data.Count > 0)
                    {
                        Interlocked.Add(ref dataReceived, segment.Data.Count);

                        Host.Allocate(out Memory memory);
                        memory.CopyFrom(in segment.Data);

                        Host.Add(new Event(this, new Data(channel.Index, memory)));
                    }

                    // Remove previous messages if any
                    channel.RX.Messages.RemoveAndDisposeBefore(segment.SequenceNumber);

                    // Remove previous reassemblies if any
                    channel.RX.Reassemblies.RemoveAndDisposeBefore(segment.SequenceNumber);

                    channel.RX.CrossSequenceNumber = xseq;
                    channel.RX.NextSequenceNumber = segment.SequenceNumber + 1;
                    channel.RX.NextReliableSequenceNumber = segment.ReliableSequenceNumber;

                    // Deliver any messages stalled by this segment
                    Deliver(ref channel);

                    channel.RX.UpdateLastSequenceNumber();
                    channel.RX.UpdateLowestSequenceNumber();

                    Acknowledge(ref channel, remoteTime, isFirstInPacket);
                }
                else if (segment.ReliableSequenceNumber >= channel.RX.NextReliableSequenceNumber + 1) // message arrived ahead of at least one reliable message that is missing
                {
                    channel.RX.UpdateNextRemoteTime(window, remoteTime);

                    if (channel.RX.Messages.TryAddOrGet(segment.SequenceNumber, Host.Allocate, out Channel.Inbound.Message message)) // message is new
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

                        channel.RX.UpdateLastSequenceNumber();
                    }

                    Acknowledge(ref channel, remoteTime, isFirstInPacket);
                }
            }
            else if (segment.SequenceNumber >= channel.RX.LowestSequenceNumber) // message has been delivered already but is still inside the acknowledgement window
            {
                var (xseq, window) = GetCrossSequenceNumberAndStaticWindow(channel.RX.CrossSequenceNumber, -(ushort)unchecked((ushort)(channel.RX.NextSequenceNumber - segment.SequenceNumber) - 1));

                // Discard if message does not belong to this incarnation of the sliding window
                if (remoteTime <= channel.RX.NextRemoteTimes[window])
                    return;

                Acknowledge(ref channel, remoteTime, isFirstInPacket);
            }
        }

        internal void OnReceive(Protocol.Time time, Protocol.Time remoteTime, bool reliable, bool isFirstInPacket, in Protocol.Message.Fragment fragment)
        {
            ref var channel = ref channels[fragment.Channel];

            if (fragment.SequenceNumber == channel.RX.NextSequenceNumber) // message is the next expected (either reliable or unreliable)
            {
                var (xseq, window) = GetCrossSequenceNumberAndStaticWindow(channel.RX.CrossSequenceNumber, 1);

                // Discard if message does not belong to this incarnation of the sliding window
                if (remoteTime <= channel.RX.NextRemoteTimes[window])
                    return;

                // If an assembly can be added then this is also the first fragment that has arrived, otherwise
                // other fragments have arrived ahead and we're filling a gap.
                if (channel.RX.Reassemblies.TryAddOrGet(fragment.SequenceNumber + (byte)(fragment.Last - fragment.Index), Host.Allocate, out Channel.Inbound.Reassembly reassembly))
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

                channel.RX.UpdateNextRemoteTime(window, remoteTime);

                Interlocked.Add(ref dataReceived, fragment.Data.Count);
                reassembly.Data.CopyFrom(in fragment.Data, fragment.Index * MaxFragmentSize);

                if (fragment.Index == fragment.Last) // this is the last fragment so reassembly is complete
                {
                    // There's no need to create a message node, just deliver.
                    Host.Add(new Event(this, new Data(channel.Index, reassembly.Data)));
                    reassembly.Data = null;

                    var next = reassembly.SequenceNumber + 1;

                    // Remove all fragment placeholders from the message buffer including the last one.
                    channel.RX.Messages.RemoveAndDisposeBefore(next);

                    // Remove reassemblies including this one.
                    channel.RX.Reassemblies.RemoveAndDisposeBefore(next);

                    channel.RX.CrossSequenceNumber = GetCrossSequenceNumber(channel.RX.CrossSequenceNumber, (ushort)unchecked((ushort)(reassembly.SequenceNumber - channel.RX.NextSequenceNumber) + 1));
                    channel.RX.NextSequenceNumber = next;

                    if (reliable)
                    {
                        // If delivery is reliable every fragment has an individual reliable sequence number and this is the latest one.
                        channel.RX.NextReliableSequenceNumber = fragment.ReliableSequenceNumber;

                        // Deliver messages waiting for this one (if any)
                        Deliver(ref channel);
                    }
                }
                else // reassembly may or may not be complete depending whether other fragments have already arrived.
                {
                    // There's no need to create a fragment placeholder, because this is the next expected anyway.
                    channel.RX.CrossSequenceNumber = GetCrossSequenceNumber(channel.RX.CrossSequenceNumber, 1);
                    channel.RX.NextSequenceNumber = fragment.SequenceNumber + 1;
                    channel.RX.NextReliableSequenceNumber = fragment.ReliableSequenceNumber;

                    // Deliver messages waiting for this one (if any)
                    Deliver(ref channel);
                }

                channel.RX.UpdateLastSequenceNumber();
                channel.RX.UpdateLowestSequenceNumber();

                Acknowledge(ref channel, remoteTime, isFirstInPacket);
            }
            else if (fragment.SequenceNumber > channel.RX.NextSequenceNumber) // message is ahead of the next expected
            {
                var (xseq, window) = GetCrossSequenceNumberAndStaticWindow(channel.RX.CrossSequenceNumber, (ushort)unchecked((ushort)fragment.SequenceNumber - (ushort)(channel.RX.NextSequenceNumber + 1)));

                // Discard if message does not belong to this incarnation of the sliding window
                if (remoteTime <= channel.RX.NextRemoteTimes[window])
                    return;

                if ((fragment.ReliableSequenceNumber == channel.RX.NextReliableSequenceNumber)                     // unreliable message arriving ahead of other unreliable ones (because the ReliableSequenceNumber is still the same)
                  || (reliable && fragment.ReliableSequenceNumber == channel.RX.NextReliableSequenceNumber + 1))   // OR message is the next reliable expected
                {
                    // If an assembly can be added then this is also the first fragment that has arrived, otherwise
                    // other fragments have arrived ahead and we're filling a gap.
                    if (channel.RX.Reassemblies.TryAddOrGet(fragment.SequenceNumber + (byte)(fragment.Last - fragment.Index), Host.Allocate, out Channel.Inbound.Reassembly reassembly))
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

                    channel.RX.UpdateNextRemoteTime(window, remoteTime);

                    if (channel.RX.Messages.TryAddOrGet(fragment.SequenceNumber, Host.Allocate, out Channel.Inbound.Message message)) // message is new
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
                        if (channel.RX.NextSequenceNumber < first)
                        {
                            // Remove previous datagrams if any
                            channel.RX.Messages.RemoveAndDisposeBefore(first);

                            // Remove previous reassemblies if any
                            channel.RX.Reassemblies.RemoveAndDisposeBefore(first);

                            channel.RX.CrossSequenceNumber = GetCrossSequenceNumber(channel.RX.CrossSequenceNumber, (ushort)(first - channel.RX.NextSequenceNumber));
                            channel.RX.NextSequenceNumber = first;

                            // It's safe to assign the reliable sequence number here because this is either 
                            // an unreliable message arriving ahead of other unreliable ones (so the reliable 
                            // sequence number is in fact the same) OR this is the next reliable message expected.
                            channel.RX.NextReliableSequenceNumber = fragment.ReliableSequenceNumber;
                        }

                        Deliver(ref channel);

                        channel.RX.UpdateLastSequenceNumber();
                        channel.RX.UpdateLowestSequenceNumber();
                    }

                    Acknowledge(ref channel, remoteTime, isFirstInPacket);
                }
                else if (fragment.ReliableSequenceNumber >= channel.RX.NextReliableSequenceNumber + 1) // message arrived ahead of at least one reliable message that is missing
                {
                    // If an assembly can be added then this is also the first fragment that has arrived, otherwise
                    // other fragments have arrived ahead and we're filling a gap.
                    if (channel.RX.Reassemblies.TryAddOrGet(fragment.SequenceNumber + (byte)(fragment.Last - fragment.Index), Host.Allocate, out Channel.Inbound.Reassembly reassembly))
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

                    channel.RX.UpdateNextRemoteTime(window, remoteTime);

                    if (channel.RX.Messages.TryAddOrGet(fragment.SequenceNumber, Host.Allocate, out Channel.Inbound.Message message)) // message is new
                    {
                        Interlocked.Add(ref dataReceived, fragment.Data.Count);
                        reassembly.Data.CopyFrom(in fragment.Data, fragment.Index * MaxFragmentSize);

                        message.ReliableSequenceNumber = fragment.ReliableSequenceNumber;
                        message.IsReliable = reliable;
                        message.Reassembly = reassembly;

                        channel.RX.UpdateLastSequenceNumber();
                    }

                    Acknowledge(ref channel, remoteTime, isFirstInPacket);
                }
            }
            else if (fragment.SequenceNumber >= channel.RX.LowestSequenceNumber) // message has been delivered already but is still inside the acknowledgement window
            {
                var (xseq, window) = GetCrossSequenceNumberAndStaticWindow(channel.RX.CrossSequenceNumber, -(ushort)unchecked((ushort)channel.RX.NextSequenceNumber - 1 - (ushort)fragment.SequenceNumber));

                // Discard if message does not belong to this incarnation of the sliding window
                if (remoteTime <= channel.RX.NextRemoteTimes[window])
                    return;

                Acknowledge(ref channel, remoteTime, isFirstInPacket);
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

        private void Deliver(ref Channel channel)
        {
            var state = new DeliveryState
            {
                Peer = this,
                Channel = channel.Index,
                NextSequenceNumber = channel.RX.NextSequenceNumber,
                NextReliableSequenceNumber = channel.RX.NextReliableSequenceNumber
            };

            channel.RX.Messages.Traverse(Deliver, ref state);

            // Remove processed messages.
            channel.RX.Messages.RemoveAndDisposeBefore(state.NextSequenceNumber);

            // Remove obsolete reassemblies.
            channel.RX.Reassemblies.RemoveAndDisposeBefore(state.NextSequenceNumber);

            channel.RX.CrossSequenceNumber += (ushort)((ushort)state.NextSequenceNumber - (ushort)channel.RX.NextSequenceNumber);
            channel.RX.NextSequenceNumber = state.NextSequenceNumber;
            channel.RX.NextReliableSequenceNumber = state.NextReliableSequenceNumber;
            
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
                                    ? Host.EncodeReset(Host.UserEncoder, Host.Timestamp(), ref Session)
                                    : Host.EncodeReset(Host.UserEncoder, Host.Timestamp(), Session.Remote)));
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
                                        ? Host.EncodeReset(Host.UserEncoder, Host.Timestamp(), ref Session)
                                        : Host.EncodeReset(Host.UserEncoder, Host.Timestamp(), Session.Remote)));
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
            SendWindow = default;
            BytesInFlight = default;

            transmissionBacklog = 0;
            events = null;
            channels = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Min(int a, int b, int c) => Min(a, Min(b, c));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Min(int a, int b) => a < b ? a : b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long Min(long a, long b) => a < b ? a : b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Max(int a, int b) => a < b ? b : a;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Max(uint a, uint b) => a < b ? b : a;
    }
}
