using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;

// TODO: export naming styles to .editorconfig using VS2019 maybe?

using Carambolas.Security.Cryptography;

using Socket = Carambolas.Net.Sockets.Socket;

namespace Carambolas.Net
{
    [Flags]
    public enum ConnectionTypes
    {
        None = 0,
        Insecure = 1,
        Secure = 2,
    }

    public enum ConnectionMode
    {
        Insecure = 0,
        Secure = 1
    }

    public static class ConnectionTypesExtensions { public static bool Contains(this ConnectionTypes e, ConnectionTypes flags) => (e & flags) == flags; }

    public sealed partial class Host: IDisposable
    {
        public Host(string name = "") : this(name, default, default) { }
        public Host(string name, IRandomNumberGenerator random) : this(name, random, default) { }
        public Host(string name, ICipherFactory cipherFactory) : this(name, default, cipherFactory) { }
        public Host(string name, IRandomNumberGenerator random, ICipherFactory cipherFactory, IKeychain keyChain = default)
        {
            Name = name ?? "";            
            Random = random ?? new ISAAC(DateTime.UtcNow.Ticks);

            CipherFactory = cipherFactory ?? Net.CipherFactory.Default;
            Keychain = keyChain ?? Net.Keychain.Default;
        }
            
        public readonly string Name;

        internal readonly IRandomNumberGenerator Random;
        internal readonly ICipherFactory CipherFactory;
        internal readonly IKeychain Keychain;

        private bool enabled;
        private Socket socket;
        private Exception exception;

        internal (Key Private, Key Public) Keys;

        public IPEndPoint EndPoint { get; private set; }
        public DateTime StartTime { get; private set; }

        public readonly Stream Upstream = new Stream();
        public readonly Stream Downstream = new Stream();        

        /// <summary>
        /// An encoder with a separate buffer used to serialize output messages from the user thread.
        /// </summary>
        internal readonly BinaryWriter UserEncoder = new BinaryWriter();

        /// <summary>
        /// An encoder with a separate buffer used to serialize output messages from the worker thread.
        /// </summary>
        internal readonly BinaryWriter WorkerEncoder = new BinaryWriter();

        public byte TTL { get; private set; }

        /// <summary>
        /// Maximum frequency in hertz to update connections and send packets. 
        /// Represents the network frame rate. Maximum value is 1000.
        /// </summary>
        /// <remarks>
        /// This property can be used to establish an upper bound to network 
        /// updates (thus conserving CPU) at the cost of a minimum imposed 
        /// latency. E.g. 20 updates per second amounts to a minimum latency 
        /// of 1/20 = 50ms.
        /// </remarks>
        public ushort UpdateRate
        {
            get => updateRate;
            set
            {
                updateRate = value;
                updatePeriod = (ushort)(Protocol.Update.Rate.MaxValue / Protocol.Update.Rate.Clamp(value));
            }
        }

        private ushort updateRate = Protocol.Update.Rate.Default;

        /// <summary>
        /// Intended duration of an update frame in seconds.
        /// </summary>
        private float updatePeriod = 1.0f / Protocol.Update.Rate.Default;

        /// <summary>
        /// Maximum number of passive connection supported by the host. 
        /// Does not affect the ability of the host to initiate active connections.
        /// </summary>
        public ushort Capacity { get; private set; }

        /// <summary>
        /// Size of the largest protocol data unit (PDU) that can be transmitted in 
        /// a single operation.
        /// </summary>
        public ushort MaxTransmissionUnit { get; private set; }

        /// <summary>
        /// Highest data channel supported.
        /// </summary>
        public byte MaxChannel { get; private set; }

        /// <summary>
        /// Maximum receive bandwidth in bits per second intended for each peer. 
        /// This is the bandwidth advertised to the remote peer upon connection. 
        /// Note that this value applies only to payload data. Actual bandwidth 
        /// consumed may be higher due to protocol overhead.
        /// </summary>
        public uint MaxBandwidth { get; private set; }

        /// <summary>
        /// Maximum amount of user data in bytes that may be buffered for transmission 
        /// in each peer (across all channels). This is the initial value assigned when 
        /// a peer is first connected. The actual value may be directly modified on each 
        /// peer object.
        /// </summary>
        public int MaxTransmissionBacklog { get; private set; }

        public uint MaxReceivePacketsPerFrame => Downstream.PacketRate / updateRate;

        public uint MaxSendPacketsPerFrame => Upstream.PacketRate / updateRate;

        /// <summary>
        /// Types of connection requests that can be accepted by the host as long 
        /// as within <see cref="Capacity"/>.
        /// Does not affect the ability of the host to initiate active connections
        /// on its own.
        /// </summary>
        public ConnectionTypes AcceptableConnetionTypes;        

        public bool IsOpen => socket != null;

        public void Open() => Open(in IPEndPoint.Any);
        public void Open(in IPEndPoint localEndPoint, ConnectionTypes acceptableConnectionTypes = default) => Open(in localEndPoint, in Host.Settings.Default, acceptableConnectionTypes, Random.GetKey());
        public void Open(in IPEndPoint localEndPoint, in Host.Settings settings, ConnectionTypes acceptableConnectionTypes = default) => Open(in localEndPoint, in settings, acceptableConnectionTypes, Random.GetKey());
        public void Open(in IPEndPoint localEndPoint, in Host.Settings settings, ConnectionTypes acceptableConnectionTypes, in Key privateKey)
        {
            if (socket != null)
                throw new InvalidOperationException(Resources.GetString(Strings.Net.Host.AlreadyOpen));

            settings.CreateSocketSettings(out Socket.Settings socketopts);
            socket = new Socket(in localEndPoint, in socketopts);

            Keys = (privateKey, Keychain.CreatePublicKey(in privateKey));

            try
            {
                // Sockets bound to a local IPv6 address are configured in AddressMode.Dual. 
                // Only if it fails to bind that it will be downgraded to IPv6 only (AddressMode.IPv6).
                // There's no point in offering IP stack mode as part of the settings because:
                //   1) A socket bound to a local IPv4 address cannot be put in dual mode;
                //   2) A remote IPv4 host cannot initiate a connection to an IPv6 address;
                //   3) If a user binds the socket to a local IPv6 address but does not want 
                //      to connect to IPv4 addresses thenn simply don't initiate a connection.
                //      Dual stack support makes no difference in this case and is inditinguishable 
                //      from IPv6 only.
                if (socket.AddressMode == Carambolas.Net.Sockets.AddressMode.IPv6)
                    Log.Warn("Platform does not support dual stack. Using IPv6 stack only.");

                memoryPool = new Memory.Pool();
                outboundMessagePool = new Channel.Outbound.Message.Pool();
                inboundMessagePool = new Channel.Inbound.Message.Pool();
                inboundReassemblyPool = new Channel.Inbound.Reassembly.Pool();

                Upstream.Reset(settings.Upstream);
                Downstream.Reset(settings.Downstream);

                StartTime = DateTime.Now;

                // Socket may force a different TTL than that indicated in the settings and it's fine.
                TTL = socket.TTL;
                Capacity = settings.Capacity;
                EndPoint = socket.LocalEndPoint;
                MaxTransmissionUnit = Protocol.MTU.Clamp(settings.MaxTransmissionUnit);
                MaxChannel = Protocol.MTC.Clamp(settings.MaxChannel);
                MaxBandwidth = Protocol.Bandwidth.Clamp(settings.MaxBandwidth);
                MaxTransmissionBacklog = Math.Max(0, settings.MaxTransmissionBacklog);
                AcceptableConnetionTypes = acceptableConnectionTypes;                

                if (UserEncoder.Buffer.Length < MaxTransmissionUnit)
                    UserEncoder.Reset(new byte[MaxTransmissionUnit], 0, MaxTransmissionUnit);

                if (WorkerEncoder.Buffer.Length < MaxTransmissionUnit)
                    WorkerEncoder.Reset(new byte[MaxTransmissionUnit], 0, MaxTransmissionUnit);
                
                worker = new Thread(Work) { IsBackground = true, Name = $"{Name} Networking" };
                enabled = true;
                worker.Start();
            }
            catch
            {
                Close();
                throw;
            }
        }

        public void Close()
        {
            enabled = false;
            worker?.Wait();
            worker = default;
            socket?.Close();
            socket = default;
            exception = default;

            Keys = default;

            inboundMessagePool?.Dispose();
            inboundMessagePool = default;

            inboundReassemblyPool?.Dispose();
            inboundReassemblyPool = default;

            outboundMessagePool?.Dispose();
            outboundMessagePool = default;

            memoryPool?.Dispose();
            memoryPool = default;

            publicPeers.Clear();
            events.Clear();
            resets.Clear();

            foreach (var kv in peers)
                kv.Value.Dispose();

            peers.Clear();

            first = default;
            acceptedCount = default;            

            Upstream.Reset();
            Downstream.Reset();

            StartTime = default;
            TTL = default;
            Capacity = default;
            EndPoint = default;
            MaxTransmissionUnit = default;
            MaxChannel = default;
            MaxBandwidth = default;
            MaxTransmissionBacklog = default;
            AcceptableConnetionTypes = default;
            
        }

        public void Dispose() => Close();        

        #region Public Connections

        /// <summary>
        /// Connections that are visible to the user. 
        /// <para/>
        /// This is a safe collection that is only iterated or modified in the user thread 
        /// so it doesn't require a lock.
        /// </summary>
        /// <remarks>
        /// A valid connection for internal purposes is one that is not <see cref="Protocol.State.Disconnected"/>
        /// but for the end user there may be a dissociation between the perceived state of a connection 
        /// (<see cref="Peer.State"/>) and its actual state (<see cref="Session.State"/>).
        /// This is due to several reasons. In particular, incoming connections that are still in the course 
        /// of being accepted shouldn't be visible at all as they are unable to generate any event except for a 
        /// <see cref="EventType.Connection"/>. It's only by the time the user retrieves the connection
        /// event and becomes aware of this connection's existence that it should be added to the user's collection. 
        /// Another case is that of connections which have been disconnected by either the remote host 
        /// or a timeout but sill have events to be retrieved. These must remain visible and appear as connected
        /// for consistency (although any send operation is doomed to silently fail).
        /// </remarks>
        private readonly Dictionary<IPEndPoint, Peer> publicPeers = new Dictionary<IPEndPoint, Peer>();

        public int Count => publicPeers.Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetPeer(in IPEndPoint endPoint, out Peer peer) => publicPeers.TryGetValue(endPoint, out peer);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator() => new Enumerator(publicPeers.Values.GetEnumerator());

        public struct Enumerator: IEnumerator<Peer>
        {
            private Dictionary<IPEndPoint, Peer>.ValueCollection.Enumerator enumerator;

            internal Enumerator(Dictionary<IPEndPoint, Peer>.ValueCollection.Enumerator e) => enumerator = e;

            public Peer Current => enumerator.Current;
            object IEnumerator.Current => Current;

            public bool MoveNext() => enumerator.MoveNext();
            public void Reset() => throw new NotSupportedException();
            public void Dispose() => enumerator.Dispose();
        }

        #endregion

        #region Events

        private SpinLock eventsLock = new SpinLock(false);

        /// <summary>
        /// A queue to preserve the order in which events are generated between connections
        /// without storing the events themselves. This way they can be released when a 
        /// remote host is disconnected witout affecting other connections or incurring an 
        /// expensive search and remove. 
        /// </summary>
        private readonly Queue<Peer> events = new Queue<Peer>();

        /// <summary>
        /// Gets an event from the buffer. Returns true if an event could be retrieved; otherwise, false.
        /// </summary>
        public bool TryGetEvent(out Event e)
        {
            if (exception != null)
                throw new ThreadException(Resources.GetString(Strings.Net.Host.ThreadException),  exception);

            if (socket == null)
                throw new InvalidOperationException(Resources.GetString(Strings.Net.Host.NotOpen));

            Peer peer;

            var locked = false;
            try
            {
                eventsLock.Enter(ref locked);
                do
                {
                    if (events.Count == 0)
                    {
                        e = default;
                        return false;
                    }

                    peer = events.Dequeue();
                }
                while (peer.State == PeerState.Disconnected);
            }
            finally
            {
                if (locked)
                    eventsLock.Exit(false);
            }

            if (peer.Terminated && peer.State == PeerState.Disconnecting)
            {
                e = new Event(peer, PeerReason.Closed);
                publicPeers.Remove(peer.EndPoint);
                peer.Dispose();
            }
            else
            {
                peer.Dequeue(out e);
                switch (e.EventType)
                {
                    case EventType.Connection:
                        peer.State = PeerState.Connected;
                        if (peer.Mode == PeerMode.Passive)
                            publicPeers.Add(peer.EndPoint, peer);
                        break;
                    case EventType.Disconnection:
                        publicPeers.Remove(peer.EndPoint);
                        peer.Dispose();
                        break;
                    default:
                        break;
                }
            }

            return true;
        }

        internal void Add(in Event e)
        {
            e.Peer.Enqueue(in e);

            var locked = false;
            try
            {
                eventsLock.Enter(ref locked);
                events.Enqueue(e.Peer);
            }
            finally
            {
                if (locked)
                    eventsLock.Exit(false);
            }
        }

        #endregion

        #region Internal Connections

        /// <summary>
        /// Internal connections lock.
        /// <para/>
        /// A lock is needed because <see cref="Connect(in IPEndPoint, out Peer)"/>
        /// runs on the user thread and may add a new connection.
        /// </summary>
        private SpinLock peersLock = new SpinLock(false);

        /// <summary>
        /// Internal connections maintained by the worker thread. 
        /// This is not the same collection observed by the user
        /// (<see cref="publicPeers"/>).
        /// </summary>
        private readonly Dictionary<IPEndPoint, Peer> peers = new Dictionary<IPEndPoint, Peer>();

        /// <summary>
        /// First item of the internal linked-list of connections.
        /// </summary>
        private Peer first;

        /// <summary>
        /// Number of passive (incoming) connections accepted by the worker thread.
        /// </summary>
        private int acceptedCount;

        /// <summary>
        /// Initiates a connection to a remote host.
        /// <para/>
        /// Returns true if a connection could be sucessfully initiated in which case
        /// the output parameter <paramref name="peer"/> contains a reference to the 
        /// newly created <see cref="Peer"/>; otherwise this method returns false and
        /// the output parameter <paramref name="peer"/> must contain a reference to the 
        /// existing <see cref="Peer"/>. 
        /// <para/>
        /// Note that this is a non-blocking call and the returned <paramref name="peer"/> 
        /// is not guaranteed to be connected yet (most often it will not). 
        /// <para/>
        /// The user must call <see cref="TryGetEvent(out Event)"/> in order to determine 
        /// when the connection is successfully connected. If the connections fails an event with 
        /// <see cref="EventType.Disconnection"/> is raised with an indication of the reason.
        /// </summary>
        public bool Connect(in IPEndPoint endPoint, out Peer peer) => Connect(in endPoint, default, default, out peer);
        public bool Connect(in IPEndPoint endPoint, in Key remoteKey, out Peer peer) => Connect(in endPoint, SessionOptions.Secure | SessionOptions.ValidateRemoteKey, in remoteKey, out peer);
        public bool Connect(in IPEndPoint endPoint, ConnectionMode mode, out Peer peer) => Connect(in endPoint, (SessionOptions)mode, default, out peer);

        private bool Connect(in IPEndPoint endPoint, SessionOptions options, in Key remoteKey, out Peer peer)
        {
            // Check if there's a public connection to this endpoint already
            if (TryGetPeer(in endPoint, out peer))
                return false;

            var locked = false;
            try
            {
                peersLock.Enter(ref locked);

                // Check if there has not been an incoming connection racing ahead.
                if (peers.TryGetValue(endPoint, out peer))
                    return false;

                var time = Timestamp();
                peer = new Peer(this, time, in endPoint, PeerMode.Active, options, in remoteKey)
                {
                    MaxTransmissionBacklog = MaxTransmissionBacklog,
                    MaxTransmissionUnit = Protocol.MTU.MinValue
                };

                peer.OnConnecting(time);

                AddOrReplace(peer);
                publicPeers.Add(peer.EndPoint, peer);
                return true;
            }
            finally
            {
                if (locked)
                    peersLock.Exit(false);
            }
        }

        /// <summary>
        /// Try to accept an incoming connection request while enforcing mutual exclusion against concurrent calls to 
        /// <see cref="Connect(in IPEndPoint, out Peer)"/> from the user thread (due to the potential for receiving 
        /// a connection request from the same end point).
        /// <para/>
        /// It also handles the case where a remote host tries to reconnect before a disconnected <see cref="Peer"/> instance could be 
        /// removed from the internal collection of peers.
        /// </summary>
        /// <returns>
        /// <c>false</c> if a peer already exists and is not disconnected.
        /// If a peer exists but is disconnected then a new peer is created to replace it and this method returns <c>true</c>.
        /// Otherwise this method returns <c>true</c> but a new peer is created only if permitted by the host constraints.
        /// </returns>
        /// <remarks>
        /// Note that the output parameter <paramref name="peer"/> is never null when this method returns false, 
        /// but may be null when this method returns true.
        /// </remarks>
        private bool TryAccept(Protocol.Time time, Protocol.Time remoteTime, uint remoteSession, in Protocol.Message.Connect connect, in IPEndPoint endPoint, out Peer peer)
        {
            var locked = false;
            try
            {
                peersLock.Enter(ref locked);
                var accepted = acceptedCount;
                if (peers.TryGetValue(endPoint, out peer))
                {
                    if (peer.Session.State != Protocol.State.Disconnected)
                        return false;

                    accepted--;
                }

                if (AcceptableConnetionTypes.Contains(ConnectionTypes.Insecure) && accepted < Capacity)
                {
                    peer = new Peer(this, time, in endPoint, PeerMode.Passive)
                    {
                        MaxTransmissionBacklog = MaxTransmissionBacklog,
                        MaxTransmissionUnit = connect.MaximumTransmissionUnit,
                        LatestRemoteTime = remoteTime
                    };

                    peer.OnAccepting(time, remoteTime, remoteSession, in connect);
                    AddOrReplace(peer);
                }                

                return true;
            }
            finally
            {
                if (locked)
                    peersLock.Exit(false);
            }
        }

        /// <summary>
        /// Try to accept an incoming secure connection request while enforcing mutual exclusion against concurrent calls to 
        /// <see cref="Connect(in IPEndPoint, out Peer)"/> from the user thread (due to the potential for receiving 
        /// a connection request from the same end point). 
        /// <seealso cref="TryAccept(Protocol.Time, Protocol.Time, uint, in Protocol.Message.Connect, in IPEndPoint, out Peer)"/>
        /// </summary>
        private bool TryAccept(Protocol.Time time, Protocol.Time remoteTime, uint remoteSession, in Protocol.Message.Connect connect, in Key remoteKey, in IPEndPoint endPoint, out Peer peer)
        {
            var locked = false;
            try
            {
                peersLock.Enter(ref locked);
                var accepted = acceptedCount;
                if (peers.TryGetValue(endPoint, out peer))
                {
                    if (peer.Session.State != Protocol.State.Disconnected)
                        return false;

                    accepted--;
                }

                if (AcceptableConnetionTypes.Contains(ConnectionTypes.Secure) && accepted < Capacity)
                {
                    peer = new Peer(this, time, in endPoint, PeerMode.Passive, SessionOptions.Secure | SessionOptions.ValidateRemoteKey, in remoteKey)
                    {
                        MaxTransmissionBacklog = MaxTransmissionBacklog,
                        MaxTransmissionUnit = connect.MaximumTransmissionUnit,
                        LatestRemoteTime = remoteTime
                    };

                    peer.OnAccepting(time, remoteTime, remoteSession, in connect);
                    AddOrReplace(peer);
                }

                return true;
            }
            finally
            {
                if (locked)
                    peersLock.Exit(false);
            }
        }

        private bool TryGet(in IPEndPoint endPoint, out Peer peer)
        {
            var locked = false;
            try
            {
                peersLock.Enter(ref locked);
                return peers.TryGetValue(endPoint, out peer);
            }
            finally
            {
                if (locked)
                    peersLock.Exit(false);
            }
        }

        private void Remove(List<Peer> list)
        {
            var locked = false;
            try
            {
                peersLock.Enter(ref locked);
                foreach (var peer in list)
                {
                    if (!peers.TryGetValue(peer.EndPoint, out Peer stored) || peer != stored)
                        continue;

                    peers.Remove(peer.EndPoint);
                    if (peer.Next != null)
                        peer.Next.Prev = peer.Prev;

                    if (first == peer)
                        first = peer.Next;
                    else
                        peer.Prev.Next = peer.Next;

                    if (peer.Mode == PeerMode.Passive)
                        acceptedCount--;
                }

                var count = peers.Count;
                Upstream.Count = count;
                Downstream.Count = count;
            }
            finally
            {
                if (locked)
                    peersLock.Exit(false);
            }
        }

        private void AddOrReplace(Peer peer)
        {
            peers[peer.EndPoint] = peer;            
            peer.Next = first;
            if (first != null)
                first.Prev = peer;
            first = peer;

            var count = peers.Count;
            Upstream.Count = count;
            Downstream.Count = count;

            if (peer.Mode == PeerMode.Passive)
                acceptedCount++;
        }

        #endregion

        #region Timeouts

        /// <summary>
        /// Time in milliseconds a peer may remain unresponsive 
        /// until it's considered disconnected.
        /// </summary>
        public uint ConnectionTimeout = Protocol.Limits.Connection.Timeout.Default;

        /// <summary>
        /// Time in milliseconds a peer may rest unchecked (idle)
        /// <para/>
        /// If no acknowledgement arrives and no reliable message is transmitted 
        /// within this time then an empty reliable segment is transmitted to verify 
        /// that the peer is still connected.
        /// </summary>
        public uint IdleTimeout = Protocol.Limits.Idle.Timeout.Default;

        /// <summary>
        /// Minimum and maximum limits in milliseconds for the acknowledgement timeout.
        /// </summary>
        public Range<uint> AckTimeoutLimit = new Range<uint>(Protocol.Limits.Ack.Timeout.MinValue, Protocol.Limits.Ack.Timeout.MaxValue);

        /// <summary>
        /// Maximum number of consecutive times a peer may fail to acknowledge a packet 
        /// until it's considered disconnected.
        /// </summary>
        public byte AckFailLimit = Protocol.Limits.Ack.Fail.Default;

        /// <summary>
        /// Multiplicative factor used to backoff the acknowledgement timeout when 
        /// a peer fails to send any acks on time.
        /// <para/>
        /// A value &gt; 1 produces an exponential backoff (ever longer timeouts tending to infinity); 
        /// 1 produces no backoff; a value &lt; 1 produces an exponential decay 
        /// (ever shorter timeouts tending to zero).
        /// </summary>
        public float AckTimeoutFactor = Protocol.Limits.Ack.Timeout.Backoff.Default;

        internal uint AckTimeoutClamp(uint value) => Protocol.Limits.Ack.Timeout.Clamp(AckTimeoutLimit.Clamp(value));

        internal uint AckTimeoutBackoff(uint value) => (uint)(value * Math.Max(0f, AckTimeoutFactor));

        #endregion

        #region Resets

        /// <summary>
        /// Collection of reset packets to send.
        /// </summary>
        private readonly HashSet<Reset> resets = new HashSet<Reset>();

        internal void Add(in Reset reset) => resets.Add(reset);

        #endregion

        #region Worker Thread 

        private Thread worker;

        private void Work()
        {
            // Collection of peers that have disconnected and must be removed.                        
            var disconnected = new List<Peer>();

            // Shared buffer capable of handling the maximum MTU to avoid the 
            // performance penalty of handling exceptions due to incoming 
            // datagrams that are bigger than the receive buffer.
            var buffer = new byte[Protocol.MTU.MaxValue];   

            var reader = new BinaryReader(buffer, 0, 0);
            var writer = new BinaryWriter(buffer);

            try
            {
                while (enabled)
                {
                    // Start ticks used to calculate the remaining frame time in the end of the loop.
                    var start = timeSource.ElapsedTicks();
                    // Current timestamp
                    var time = timeSource.ElapsedTicksToTimestamp(start);    

                    var receiveLimit = MaxReceivePacketsPerFrame;
                    var sendLimit = MaxSendPacketsPerFrame;

                    for (var peer = first; peer != null; peer = peer.Next)
                    {
                        switch (peer.Session.State)
                        {
                            case Protocol.State.Connecting:
                            case Protocol.State.Accepting:
                                peer.OnConnectingUpdate(time);
                                if (peer.Session.State == Protocol.State.Disconnected)
                                {
                                    disconnected.Add(peer);
                                    continue;
                                }

                                if (sendLimit > 0 && peer.OnConnectingSend(time, writer))
                                {
                                    var length = writer.Count;
                                    socket.UncheckedSend(buffer, 0, length, in peer.EndPoint);
                                    sendLimit--;

                                    Interlocked.Increment(ref peer.packetsSent);
                                    Interlocked.Add(ref peer.bytesSent, length);
                                }
                                break;
                            case Protocol.State.Connected:
                                peer.OnConnectedUpdate(time);
                                if (peer.Session.State == Protocol.State.Disconnected)
                                {
                                    disconnected.Add(peer);
                                    continue;
                                }

                                // Send as much data as possible
                                while (sendLimit > 0 && peer.OnConnectedSend(time, writer))
                                {
                                    var length = writer.Count;
                                    socket.UncheckedSend(buffer, 0, length, in peer.EndPoint);
                                    sendLimit--;

                                    Interlocked.Increment(ref peer.packetsSent);
                                    Interlocked.Add(ref peer.bytesSent, length);
                                }
                                break;
                            default:
                                break;
                        }
                    }

                    // Remove disconnected connections
                    if (disconnected.Count > 0)
                    {
                        Remove(disconnected);
                        disconnected.Clear();
                    }

                    // Send resets
                    if (resets.Count > 0)
                    {
                        foreach (var reset in resets)
                        {
                            var encoded = reset.Encoded;
                            var length = encoded.Length;
                            
                            encoded.CopyTo(buffer);
                            socket.UncheckedSend(buffer, 0, length, in reset.EndPoint);
                            encoded.Dispose();
                        }

                        resets.Clear();
                    }

                    float elapsed, timeout;
                    var ticks = timeSource.ElapsedTicks();

                    // Read anything that may arrive until there's less than one millisecond remaining for this frame.
                    if ((timeout = updatePeriod - (elapsed = (float)TickCounter.TicksToSeconds(ticks - start))) > 0.001)
                    {
                        do
                        {
                            // If the receive limit has been reached just sleep for the rest of the frame.
                            if (receiveLimit == 0)
                            {
                                Thread.Sleep((int)(timeout * 1000));
                                break;
                            }

                            // Receive all immediately available data 
                            var available = socket.Available;
                            if (available > 0)
                            {
                                time = timeSource.ElapsedTicksToTimestamp(ticks);
                                var nbytes = 0;
                                while (nbytes < available)
                                {
                                    var length = socket.UncheckedReceive(buffer, 0, buffer.Length, out IPEndPoint sender);
                                    if (length > 0)
                                    {
                                        nbytes += length;
                                        if (length <= MaxTransmissionUnit)
                                        {
                                            reader.Reset(0, length);
                                            OnReceive(in sender, time, reader);
                                        }

                                        receiveLimit--;
                                        if (receiveLimit == 0)
                                            break;
                                    }
                                }

                                ticks = timeSource.ElapsedTicks();
                            }
                            else // if there's no data immediately available wait for more.
                            {
                                var microSeconds = (int)(timeout * 1000000);
                                if (!socket.Poll(microSeconds, SelectMode.SelectRead))
                                    break;

                                ticks = timeSource.ElapsedTicks();
                            }
                        }
                        while ((timeout = updatePeriod - (elapsed = (float)TickCounter.TicksToSeconds(ticks - start))) > 0.001);
                    }
                }

                // If the thread has stopped normally (by Host.Close() instead of an exception), 
                // try to send any last minute RESETS.
                // There's no need to clear resets here as they're going to be cleared by Host.Close() anyway.
                foreach (var reset in resets)
                {
                    var encoded = reset.Encoded;
                    var length = encoded.Length;

                    encoded.CopyTo(buffer);
                    socket.UncheckedSend(buffer, 0, length, in reset.EndPoint);
                    encoded.Dispose();
                }

                // Give the socket a chance to flush those RESETs.
                Thread.Sleep(100);
            }
            catch (ThreadInterruptedException e)
            {
                // This shouldn't normally happen.
                enabled = false;
                exception = e;
            }
            catch (ThreadAbortException e)
            {
                Thread.ResetAbort();
                enabled = false;
                exception = e;
            }
            catch (ObjectDisposedException e)
            {
                // Socket has been closed, nothing to do but terminate.
                enabled = false;
                exception = e;
            }
            catch (Exception e)
            {                
                enabled = false;
                exception = e;
                Log.Exception(e);
            }
        }

        private void OnReceive(in IPEndPoint endPoint, Protocol.Time time, BinaryReader reader)
        {
            if (reader.Available < Protocol.Packet.Header.Size)
                return;

            var (buffer, offset, length) = (reader.Buffer, reader.Position, reader.Available);

            // Packet grammar. The number in parenthesis is the atom size in bytes. Square brackets denote optional elements. Curly brackets denote encrypted elements.
            // 
            // STM(4) PFLAGS(1) <CON | SECCON | ACC | SECACC | DAT | SECDAT | RST | SECRST>
            // 
            //     CON ::= SSN(4) MTU(2) MTC(1) MBW(4) CRC(4)
            //  SECCON ::= SSN(4) MTU(2) MTC(1) MBW(4) PUBKEY(32) CRC(4)
            //     ACC ::= SSN(4) MTU(2) MTC(1) MBW(4) ATM(4) RW(2) ASSN(4) CRC(4)
            //  SECACC ::= SSN(4) MTU(2) MTC(1) MBW(4) ATM(4) {RW(2)} PUBKEY(32) NONCE(8) MAC(16)16)
            //     DAT ::= SSN(4) RW(2) MSGS CRC(4)
            //  SECDAT ::= {RW(2) MSGS} NONCE(8) MAC(16)
            //     RST ::= SSN(4) CRC(4)
            //  SECRST ::= PUBKEY(32) NONCE(8) MAC(16)
            //  
            //    MSGS ::= MSG [MSG...]
            //     MSG ::= MSGFLAGS(1) <ACKACC | ACK | DUPACK | GAP | DUPGAP | SEG | FRAG>
            //  ACKACC ::= ATM(4)
            //     ACK ::= CH(1) NEXT(2) ATM(4)
            //  DUPACK ::= CH(1) CNT(2) NEXT(2) ATM(4)
            //     GAP ::= CH(1) NEXT(2) LAST(2) ATM(4)
            //  DUPGAP ::= CH(1) CNT(2) NEXT(2) LAST(2) ATM(4)
            //     SEG ::= CH(1) SEQ(2) RSN(2) SEGLEN(2) PAYLOAD(N)
            //    FRAG ::= CH(1) SEQ(2) RSN(2) SEGLEN(2) FRAGINDEX(1) FRAGLEN(2) PAYLOAD(N)

            reader.UncheckedRead(out Protocol.Time remoteTime);
            reader.UncheckedRead(out Protocol.PacketFlags pflags);

            switch (pflags)
            {
                case Protocol.PacketFlags.Connect: // SSN(4) MTU(2) MTC(1) MBW(4) CRC(4)
                    if (reader.Available == (sizeof(uint) + Protocol.Message.Connect.Size + Protocol.Packet.Insecure.Checksum.Size)) 
                    {
                        if (!Protocol.Packet.Insecure.Checksum.Verify(buffer, offset, length))
                            break;

                        reader.UncheckedRead(out uint remoteSession);

                        // CONNECT with an invalid MTU must be silently ignored.
                        reader.UncheckedRead(out ushort mtu);
                        if (!(mtu >= Protocol.MTU.MinValue && mtu <= Protocol.MTU.MaxValue))
                            break;

                        if (mtu > MaxTransmissionUnit)
                            mtu = MaxTransmissionUnit;

                        // CONNECT with an invalid MTC must be silently ignored.
                        reader.UncheckedRead(out byte mtc);
                        if (!(mtc >= Protocol.MTC.MinValue && mtc <= Protocol.MTC.MaxValue))
                            break;

                        if (mtc > MaxChannel)
                            mtc = MaxChannel;

                        reader.UncheckedRead(out uint mbw);
                        mbw = Protocol.Bandwidth.Clamp(mbw);

                        var connect = new Protocol.Message.Connect(mtu, mtc, mbw);

                        // Try to accept as a new peer, if failed then the peer already exists.
                        TryAccept:                        
                        if (!TryAccept(time, remoteTime, remoteSession, in connect, in endPoint, out Peer peer))
                        {
                            // Insecure CONNECT must be ignored by secure sessions.
                            if (peer.Session.Options.Contains(SessionOptions.Secure))
                                break;

                            Interlocked.Increment(ref peer.packetsReceived);
                            Interlocked.Add(ref peer.bytesReceived, length);

                            var state = peer.Session.State;
                            if (state == Protocol.State.Connecting)
                            {
                                peer.LatestRemoteTime = remoteTime;
                                peer.OnCrossConnecting(time, remoteTime, remoteSession, in connect);
                            }
                            else
                            {
                                if (peer.LatestRemoteTime < remoteTime)
                                {
                                    if (peer.Session.Remote == remoteSession) // this a retransmission.
                                    {
                                        peer.LatestRemoteTime = remoteTime;
                                        if (state == Protocol.State.Accepting)
                                            peer.Accept(remoteTime);
                                    }
                                    else // this is a re-connection attempt over a half-open connection.
                                    {
                                        // There's no need to send a RESET, just TryAccept again.
                                        peer.Reset(PeerReason.Error);
                                        goto TryAccept;
                                    }
                                }
                            }
                        }
                    }
                    break;
                case Protocol.PacketFlags.Secure | Protocol.PacketFlags.Connect: // SSN(4) MTU(2) MTC(1) MBW(4) PUBKEY(32) CRC(4)
                    if (reader.Available == (sizeof(uint) + Protocol.Message.Connect.Size + Protocol.Packet.Secure.Key.Size + Protocol.Packet.Insecure.Checksum.Size))
                    {
                        if (!Protocol.Packet.Insecure.Checksum.Verify(buffer, offset, length))
                            break;

                        reader.UncheckedRead(out uint remoteSession);

                        // CONNECT with an invalid MTU must be silently ignored.
                        reader.UncheckedRead(out ushort mtu);
                        if (!(mtu >= Protocol.MTU.MinValue && mtu <= Protocol.MTU.MaxValue))
                            break;

                        if (mtu > MaxTransmissionUnit)
                            mtu = MaxTransmissionUnit;

                        // CONNECT with an invalid MTC must be silently ignored.
                        reader.UncheckedRead(out byte mtc);
                        if (!(mtc >= Protocol.MTC.MinValue && mtc <= Protocol.MTC.MaxValue))
                            break;

                        if (mtc > MaxChannel)
                            mtc = MaxChannel;

                        reader.UncheckedRead(out uint mbw);
                        mbw = Protocol.Bandwidth.Clamp(mbw);

                        reader.UncheckedRead(out Key remoteKey);

                        var connect = new Protocol.Message.Connect(mtu, mtc, mbw);

                        TryAccept:
                        // Try to accept as a new peer, if failed then the peer already exists.
                        if (!TryAccept(time, remoteTime, remoteSession, in connect, in remoteKey, in endPoint, out Peer peer))
                        {
                            Interlocked.Increment(ref peer.packetsReceived);
                            Interlocked.Add(ref peer.bytesReceived, length);

                            var state = peer.Session.State;
                            if (state == Protocol.State.Connecting)
                            {
                                peer.LatestRemoteTime = remoteTime;
                                if (!peer.Session.Options.Contains(SessionOptions.Secure)) // session is not secure
                                {
                                    // An insecure connection request cannot cross with a secure one and half-open 
                                    // secure connections cannot be reliably detected by the receiver so the remote 
                                    // host will never reply to our request. We have no choice but to abort and try 
                                    // to accept the remote request on its own terms.
                                    peer.Reset(PeerReason.Error);
                                    goto TryAccept;
                                }

                                // Secure connection requests CAN cross with each other but we may have to validate the remote key first.
                                if (peer.Session.Options.Contains(SessionOptions.ValidateRemoteKey))
                                {
                                    if (peer.Session.RemoteKey != remoteKey)
                                        break;

                                    peer.OnCrossConnecting(time, remoteTime, remoteSession, in connect);
                                }
                                else
                                {
                                    peer.OnCrossConnecting(time, remoteTime, remoteSession, in connect, in remoteKey);
                                }
                            }
                            else
                            {
                                if (peer.LatestRemoteTime < remoteTime)
                                {
                                    if (!peer.Session.Options.Contains(SessionOptions.Secure)) // this is a re-connection attempt over a previously insecure half-open connection.
                                    {
                                        // There's no need to send a RESET packet, just TryAccept again.
                                        peer.Reset(PeerReason.Error);
                                        goto TryAccept;
                                    }

                                    if (peer.Session.RemoteKey == remoteKey) // this is a re-transmission
                                        peer.Accept(remoteTime);

                                    // Half-open secure connections cannot be reliably detected by the receiver because despite the name, a secure 
                                    // connection request is in fact an insecure packet so it cannot be trusted. When a connection request arrives 
                                    // from the same remote host there's no way to tell whether the packet is legitimate or has been forged/tampered. 
                                    // While the former case would just force the remote host to wait for a half-open connection to timeout, the 
                                    // latter would cause an improper reset of a valid session.
                                }
                            }
                        }
                    }
                    break;
                case Protocol.PacketFlags.Accept: // SSN(4) MTU(2) MTC(1) MBW(4) ATM(4) RW(2) ASSN(4) CRC(4)
                    if (reader.Available == (sizeof(uint) + Protocol.Message.Accept.Size + sizeof(ushort) + sizeof(uint) + Protocol.Packet.Insecure.Checksum.Size)) 
                    {
                        if (!Protocol.Packet.Insecure.Checksum.Verify(buffer, offset, length))
                            break;

                        reader.UncheckedRead(out uint remoteSession);

                        // If a peer wasn't found the remote host must be in a half-open insecure session.
                        if (!TryGet(in endPoint, out Peer peer) 
                            || peer.Session.State == Protocol.State.Disconnected)
                        {
                            Add(new Reset(endPoint, remoteSession, EncodeReset(WorkerEncoder, time, remoteSession)));
                            break;
                        }

                        // Insecure packets other than CONNECT must be silently ignored by secure sessions.
                        if (peer.Session.Options.Contains(SessionOptions.Secure))
                        {
                            Interlocked.Increment(ref peer.packetsDropped);
                            break;
                        }

                        Interlocked.Increment(ref peer.packetsReceived);
                        Interlocked.Add(ref peer.bytesReceived, length);

                        // ACCEPT with an invalid MTU must be silently ignored.
                        reader.UncheckedRead(out ushort mtu);
                        if (!(mtu >= Protocol.MTU.MinValue && mtu <= Protocol.MTU.MaxValue))
                        {
                            Interlocked.Increment(ref peer.packetsDropped);
                            break;
                        }

                        if (mtu > MaxTransmissionUnit)
                            mtu = MaxTransmissionUnit;

                        // ACCEPT with an invalid MTC must be silently ignored.
                        reader.UncheckedRead(out byte mtc);
                        if (!(mtc >= Protocol.MTC.MinValue && mtc <= Protocol.MTC.MaxValue))
                        {
                            Interlocked.Increment(ref peer.packetsDropped);
                            break;
                        }

                        if (mtc > MaxChannel)
                            mtc = MaxChannel;

                        reader.UncheckedRead(out uint mbw);
                        mbw = Protocol.Bandwidth.Clamp(mbw);

                        reader.UncheckedRead(out uint atm);

                        reader.UncheckedRead(out ushort remoteWindow);
                        reader.UncheckedRead(out uint acknowledgedSession);                        

                        var state = peer.Session.State;
                        if (state == Protocol.State.Connecting)
                        {
                            // If Session.Local doesn't match the acknowledged session, this is either an old ACCEPT to which there's no point
                            // replying (not even with a RESET) or this is a legitimate ACCEPT for an old duplicate CONNECT that the remote host 
                            // picked up before ours by accident. In this case, when the remote eventually picks one of our CONNECTs which are 
                            // more recent, it will automatically reset and accept the new connection.
                            if (peer.Session.Local == acknowledgedSession)
                            {
                                peer.LatestRemoteTime = remoteTime;
                                peer.RemoteWindow = remoteWindow;

                                peer.OnConnected(time, remoteTime, remoteSession, new Protocol.Message.Accept(mtu, mtc, mbw, atm));
                                Add(new Event(peer));
                            }
                            else
                            {
                                Interlocked.Increment(ref peer.packetsDropped);
                            }
                        }
                        else
                        {
                            if ((peer.LatestRemoteTime < remoteTime)
                             && (peer.Session.Remote == remoteSession && peer.Session.Local == acknowledgedSession)) // this is a legitimate re-transmission
                            {
                                peer.LatestRemoteTime = remoteTime;
                                peer.RemoteWindow = remoteWindow;

                                if (state == Protocol.State.Accepting)
                                {
                                    peer.OnAccepted(time, atm);
                                    Add(new Event(peer));
                                }
                                else if (peer.Mode == PeerMode.Active)
                                {
                                    peer.Acknowledge(Acknowledgment.Accept, remoteTime);
                                }
                                else
                                {
                                    Interlocked.Increment(ref peer.packetsDropped);
                                }
                            }
                            else
                            {
                                Interlocked.Increment(ref peer.packetsDropped);
                            }
                        }
                    }
                    break;
                case Protocol.PacketFlags.Secure | Protocol.PacketFlags.Accept: // SSN(4) MTU(2) MTC(1) MBW(4) ATM(4) {RW(2)} PUBKEY(32) NONCE(8) MAC(16)
                    if (reader.Available == (sizeof(uint) + Protocol.Message.Accept.Size + sizeof(ushort) + Protocol.Packet.Secure.Key.Size + Protocol.Packet.Secure.N64.Size + Protocol.Packet.Secure.Mac.Size))
                    {
                        // If a peer wasn't found the remote host must be in a half-open secure session.
                        // It's expected to ignore any insecure resets and there's no way to send it a secure reset anymore.
                        if (!TryGet(in endPoint, out Peer peer) 
                            || peer.Session.State == Protocol.State.Disconnected)
                            break;

                        // Secure packets other than SEC-CONNECT must be silently ignored by insecure sessions.
                        if (!peer.Session.Options.Contains(SessionOptions.Secure))
                        {
                            Interlocked.Increment(ref peer.packetsDropped);
                            break;
                        }

                        reader.UncheckedRead(out uint remoteSession);

                        // ACCEPT with an invalid MTU must be silently ignored.
                        reader.UncheckedRead(out ushort mtu);
                        if (!(mtu >= Protocol.MTU.MinValue && mtu <= Protocol.MTU.MaxValue))
                        {
                            Interlocked.Increment(ref peer.packetsDropped);
                            break;
                        }

                        if (mtu > MaxTransmissionUnit)
                            mtu = MaxTransmissionUnit;

                        // ACCEPT with an invalid MTC must be silently ignored.
                        reader.UncheckedRead(out byte mtc);
                        if (!(mtc >= Protocol.MTC.MinValue && mtc <= Protocol.MTC.MaxValue))
                        {
                            Interlocked.Increment(ref peer.packetsDropped);
                            break;
                        }

                        if (mtc > MaxChannel)
                            mtc = MaxChannel;

                        reader.UncheckedRead(out uint mbw);
                        mbw = Protocol.Bandwidth.Clamp(mbw);

                        reader.UncheckedRead(out uint atm);

                        // Save position and size of the ciphertext
                        var (position, count) = (reader.Position, sizeof(ushort));

                        reader.UncheckedSkip(count);
                        reader.UncheckedRead(out Key remoteKey);
                        reader.UncheckedRead(out ulong nonce64);
                        reader.UncheckedRead(out Mac mac);

                        var state = peer.Session.State;
                        if (state == Protocol.State.Connecting)
                        {
                            // Secure connection requests CAN cross with each other but we may have to validate the remote key first.
                            if (peer.Session.Options.Contains(SessionOptions.ValidateRemoteKey))
                            {
                                if (peer.Session.RemoteKey != remoteKey)
                                {
                                    Interlocked.Increment(ref peer.packetsDropped);
                                    break;
                                }
                            }
                            else
                            {
                                peer.Session.RemoteKey = remoteKey;
                                peer.Session.Cipher.Key = Keychain.CreateSharedKey(in Keys.Private, in remoteKey);
                            }

                            var nonce = new Nonce((uint)remoteTime, nonce64);
                            if (!peer.Session.Cipher.Verify(buffer, offset, Protocol.Packet.Header.Size + sizeof(uint) + Protocol.Message.Accept.Size, sizeof(ushort), in nonce, in mac))
                            {
                                Interlocked.Increment(ref peer.packetsDropped);
                                break;
                            }

                            peer.Session.Cipher.DecryptInPlace(buffer, position, count, in nonce);

                            Interlocked.Increment(ref peer.packetsReceived);
                            Interlocked.Add(ref peer.bytesReceived, length);

                            // There's no need for the remote host to acknowledge the session number in a secure session 
                            // because a successful decryption already proves the packet belongs to it.

                            reader.UncheckedReset(position, sizeof(ushort));
                            reader.UncheckedRead(out ushort remoteWindow);

                            peer.LatestRemoteTime = remoteTime;
                            peer.RemoteWindow = remoteWindow;

                            peer.OnConnected(time, remoteTime, remoteSession, new Protocol.Message.Accept(mtu, mtc, mbw, atm));
                            Add(new Event(peer));
                        }
                        else
                        {
                            var nonce = new Nonce((uint)remoteTime, nonce64);
                            if (!peer.Session.Cipher.Verify(buffer, offset, Protocol.Packet.Header.Size + sizeof(uint) + Protocol.Message.Accept.Size, sizeof(ushort), in nonce, in mac))
                            {
                                Interlocked.Increment(ref peer.packetsDropped);
                                break;
                            }

                            peer.Session.Cipher.DecryptInPlace(buffer, position, sizeof(ushort), in nonce);

                            if (peer.LatestRemoteTime < remoteTime) // this is a re-transmission
                            {
                                reader.UncheckedReset(position, sizeof(ushort));
                                reader.UncheckedRead(out ushort remoteWindow);

                                peer.LatestRemoteTime = remoteTime;
                                peer.RemoteWindow = remoteWindow;

                                if (state == Protocol.State.Accepting)
                                {
                                    peer.OnAccepted(time, atm);
                                    Add(new Event(peer));
                                }
                                else if (peer.Mode == PeerMode.Active)
                                {
                                    peer.Acknowledge(Acknowledgment.Accept, remoteTime);
                                }
                                else
                                {
                                    Interlocked.Increment(ref peer.packetsDropped);
                                }
                            }
                            else
                            {
                                Interlocked.Increment(ref peer.packetsDropped);
                            }
                        }
                    }
                    break;
                case Protocol.PacketFlags.Data: // SSN(4) RW(2) MSGS(N) CRC(4)
                    if (reader.Available > (sizeof(uint) + sizeof(ushort) + Protocol.Packet.Insecure.Checksum.Size)) 
                    {
                        if (!Protocol.Packet.Insecure.Checksum.Verify(buffer, offset, length))
                            break;
                        
                        reader.UncheckedRead(out uint remoteSession);

                        // If a peer wasn't found the remote host must be in a half-open connection.
                        if (!TryGet(in endPoint, out Peer peer) 
                            || peer.Session.State == Protocol.State.Disconnected)
                        {
                            Add(new Reset(endPoint, remoteSession, EncodeReset(WorkerEncoder, time, remoteSession)));
                            break;
                        }

                        // Insecure packets other than CONNECT must be silently ignored by secure sessions.
                        if (peer.Session.Options.Contains(SessionOptions.Secure))
                            break;

                        Interlocked.Increment(ref peer.packetsReceived);
                        Interlocked.Add(ref peer.bytesReceived, length);

                        if (peer.Session.State == Protocol.State.Connecting                    // If the peer is still connecting (only ACCEPT can be received at this stage) 
                          || peer.Session.Remote != remoteSession                              //   OR packet is from an unknown session
                          || remoteTime < peer.LatestRemoteTime - Protocol.Packet.LifeTime)    //   OR packet lived beyond its lifetime
                        {
                            Interlocked.Increment(ref peer.packetsDropped);
                            break;
                        }

                        reader.UncheckedTruncate(Protocol.Packet.Insecure.Checksum.Size);
                        reader.UncheckedRead(out ushort remoteWindow);
                        
                        // Update latest remote time and remote window. 
                        if (peer.LatestRemoteTime < remoteTime)
                        {
                            peer.LatestRemoteTime = remoteTime;
                            peer.RemoteWindow = remoteWindow;
                        }
                        
                        OnReceive(peer, time, remoteTime, reader);
                    }
                    break;
                case Protocol.PacketFlags.Secure | Protocol.PacketFlags.Data: // {RW(2) MSGS(N)} NONCE(8) MAC(16)
                    if (reader.Available > (sizeof(ushort) + Protocol.Packet.Secure.N64.Size + Protocol.Packet.Secure.Mac.Size)) 
                    {
                        if (!TryGet(in endPoint, out Peer peer)
                            || peer.Session.State == Protocol.State.Disconnected)
                            break;

                        // Secure packets other than SEC-CONNECT must be silently ignored by insecure sessions.
                        if (!peer.Session.Options.Contains(SessionOptions.Secure))
                            break;

                        // Save position and size of the ciphertext
                        var (position, count) = (reader.Position, reader.Available - (Protocol.Packet.Secure.N64.Size + Protocol.Packet.Secure.Mac.Size));

                        reader.UncheckedSkip(count);
                        reader.UncheckedRead(out ulong nonce64);
                        reader.UncheckedRead(out Mac mac);

                        var nonce = new Nonce((uint)remoteTime, nonce64);

                        if (!peer.Session.Cipher.Verify(buffer, offset, Protocol.Packet.Header.Size, count, in nonce, in mac))
                        {
                            Interlocked.Increment(ref peer.packetsDropped);
                            break;
                        }

                        peer.Session.Cipher.DecryptInPlace(buffer, position, count, in nonce);

                        Interlocked.Increment(ref peer.packetsReceived);
                        Interlocked.Add(ref peer.bytesReceived, length);

                        if (peer.Session.State == Protocol.State.Connecting                    // If the peer is still connecting (only ACCEPT can be received at this stage) 
                          || remoteTime < peer.LatestRemoteTime - Protocol.Packet.LifeTime)    //   OR packet lived beyond its lifetime
                        {
                            Interlocked.Increment(ref peer.packetsDropped);
                            break;
                        }

                        reader.UncheckedReset(position, count);
                        reader.UncheckedRead(out ushort remoteWindow);

                        // Update latest remote time and remote window. 
                        if (peer.LatestRemoteTime < remoteTime)
                        {
                            peer.LatestRemoteTime = remoteTime;
                            peer.RemoteWindow = remoteWindow;
                        }                        

                        OnReceive(peer, time, remoteTime, reader);

                    }
                    break;
                case Protocol.PacketFlags.Reset: // SSN(4) CRC(4)
                    if (reader.Available == (sizeof(uint) + Protocol.Packet.Insecure.Checksum.Size)) 
                    {
                        if (!Protocol.Packet.Insecure.Checksum.Verify(buffer, offset, length))
                            break;

                        reader.UncheckedRead(out uint session);

                        if (!TryGet(in endPoint, out Peer peer) 
                            || peer.Session.State == Protocol.State.Disconnected 
                            || peer.Session.Local != session)
                            break;

                        // Insecure packets other than CONNECT must be silently ignored by secure sessions.
                        if (peer.Session.Options.Contains(SessionOptions.Secure))
                            break;

                        // RESET may have a send time that is prior to the peer's latest remote time 
                        // so we can't rely on a RESET always being "the latest packet".
                        // E.g: after a system reboot where the system clock is reset and go back in time (inadvertently or not).
                        // Also RESET does not affect packet counters (sent or received). 
                        peer.Reset(peer.Session.State < Protocol.State.Connected ? PeerReason.Refused : PeerReason.Reset);
                    }
                    break;
                case Protocol.PacketFlags.Secure | Protocol.PacketFlags.Reset: // PUBKEY(32) NONCE(8) MAC(16)
                    if (reader.Available == (Protocol.Packet.Secure.Key.Size + Protocol.Packet.Secure.N64.Size + Protocol.Packet.Secure.Mac.Size)) 
                    {
                        if (!TryGet(in endPoint, out Peer peer) 
                            || peer.Session.State == Protocol.State.Disconnected)
                            break;

                        // Secure packets other than SEC-CONNECT must be silently ignored by insecure sessions.
                        if (!peer.Session.Options.Contains(SessionOptions.Secure))
                            break;
                       
                        reader.UncheckedRead(out Key remoteKey);
                        reader.UncheckedRead(out ulong nonce64);
                        reader.UncheckedRead(out Mac mac);

                        var state = peer.Session.State;
                        if (state == Protocol.State.Connecting)
                        {
                            if (peer.Session.Options.Contains(SessionOptions.ValidateRemoteKey))
                            {
                                if (peer.Session.RemoteKey != remoteKey)
                                    break;
                            }
                            else
                            {
                                peer.Session.RemoteKey = remoteKey;
                                peer.Session.Cipher.Key = Keychain.CreateSharedKey(in Keys.Private, in remoteKey);
                            }
                        }

                        // A packet that cannot be verified must be silently ignored.
                        // At this point the internal cipher must have been already initialized with the correct 
                        // shared key so there's no need to pass in a key index or compare the Session.RemoteKey.
                        if (!peer.Session.Cipher.Verify(buffer, offset, Protocol.Packet.Header.Size, 0, new Nonce((uint)remoteTime, nonce64), in mac))
                            break;

                        // RESET may have a send time that is prior to the peer's latest remote time 
                        // so we can't rely on a RESET always being "the latest packet".
                        // E.g: after a system reboot where the system clock is reset and go back in time (inadvertently or not).
                        // Also RESET does not affect packet counters (sent or received). 
                        peer.Reset(peer.Session.State < Protocol.State.Connected ? PeerReason.Refused : PeerReason.Reset);
                    }
                    break;
                default:
                    // Unknown packet types must be silently ignored.
                    break;
            }
        }

        private void OnReceive(Peer peer, Protocol.Time time, Protocol.Time remoteTime, BinaryReader reader)
        {
            // Bitset where each bit represents a channel. A bit value of 0 means no message has been 
            // parsed for this channel; otherwise one or more messages have been parsed 
            // for this channel already.
            Span<uint> channels = stackalloc uint[8] { 0, 0, 0, 0, 0, 0, 0, 0 };

            // Set and return true if the channel flag has not been set yet for this packet; otherwise return false.
            bool TrySetUsed(byte channel, in Span<uint> from)
            {
                var i = channel >> 32;
                var bit = channel % 32;
                var mask = 1u << bit;
                var prev = from[i];
                from[i] = prev | mask;
                return (prev & mask) == 0;
            }

            reader.UncheckedRead(out Protocol.MessageFlags mflags);

            do
            {
                if (mflags == (Protocol.MessageFlags.Ack | Protocol.MessageFlags.Accept))
                {
                    if (reader.Available >= Protocol.Message.Accept.Ack.Size) // ATM(4)
                    {
                        reader.UncheckedRead(out Protocol.Time atm);
                        if (peer.Session.State == Protocol.State.Accepting)
                        {
                            peer.OnAccepted(time, atm);
                            Add(new Event(peer));
                        }

                        continue;
                    }
                    goto Incomplete;
                }
                
                switch (mflags)
                {
                    case Protocol.MessageFlags.Ack | Protocol.MessageFlags.Data: // CH(1) NEXT(2) ATM(4)
                        if (reader.Available >= Protocol.Message.Ack.Size) 
                        {
                            reader.UncheckedRead(out byte channel);
                            reader.UncheckedRead(out Protocol.Ordinal seq);
                            reader.UncheckedRead(out Protocol.Time atm);

                            if (peer.Session.State >= Protocol.State.Connected && channel < channels.Length)
                                peer.OnReceive(time, remoteTime, new Protocol.Message.Ack(channel, seq, atm));

                            continue;
                        }
                        goto Incomplete;
                    case Protocol.MessageFlags.Dup | Protocol.MessageFlags.Ack | Protocol.MessageFlags.Data: // CH(1) CNT(2) NEXT(2) ATM(4)
                        if (reader.Available >= Protocol.Message.Ack.Dup.Size) 
                        {
                            reader.UncheckedRead(out byte channel);
                            reader.UncheckedRead(out ushort count);
                            reader.UncheckedRead(out Protocol.Ordinal next);
                            reader.UncheckedRead(out Protocol.Time atm);

                            if (peer.Session.State >= Protocol.State.Connected && count > 0 && channel < channels.Length)
                                peer.OnReceive(time, remoteTime, new Protocol.Message.Ack(channel, count, next, atm));

                            continue;
                        }
                        goto Incomplete;
                    case Protocol.MessageFlags.Ack | Protocol.MessageFlags.Data | Protocol.MessageFlags.Gap: // CH(1) NEXT(2) LAST(2) ATM(4)
                        if (reader.Available >= Protocol.Message.Ack.Gap.Size) 
                        {
                            reader.UncheckedRead(out byte channel);
                            reader.UncheckedRead(out Protocol.Ordinal next);
                            reader.UncheckedRead(out Protocol.Ordinal last);
                            reader.UncheckedRead(out Protocol.Time atm);

                            if (peer.Session.State >= Protocol.State.Connected && channel < channels.Length)
                                peer.OnReceive(time, remoteTime, new Protocol.Message.Ack(channel, next, last, atm));

                            continue;
                        }
                        goto Incomplete;
                    case Protocol.MessageFlags.Dup | Protocol.MessageFlags.Ack | Protocol.MessageFlags.Data | Protocol.MessageFlags.Gap: // CH(1) CNT(2) NEXT(2) LAST(2) ATM(4)
                        if (reader.Available >= Protocol.Message.Ack.Gap.Dup.Size) 
                        {
                            reader.UncheckedRead(out byte channel);
                            reader.UncheckedRead(out ushort count);
                            reader.UncheckedRead(out Protocol.Ordinal next);
                            reader.UncheckedRead(out Protocol.Ordinal last);
                            reader.UncheckedRead(out Protocol.Time atm);

                            if (peer.Session.State >= Protocol.State.Connected && count > 0 && channel < channels.Length)
                                peer.OnReceive(time, remoteTime, new Protocol.Message.Ack(channel, count, next, last, atm));

                            continue;
                        }
                        goto Incomplete;
                    case Protocol.MessageFlags.Reliable | Protocol.MessageFlags.Data | Protocol.MessageFlags.Segment:  // CH(1) SEQ(2) RSN(2) LEN(2) DAT(N)
                    case Protocol.MessageFlags.Data | Protocol.MessageFlags.Segment:
                        if (reader.Available >= Protocol.Message.Segment.MinSize)
                        {
                            reader.UncheckedRead(out byte channel);
                            reader.UncheckedRead(out Protocol.Ordinal seq);
                            reader.UncheckedRead(out Protocol.Ordinal rsn);
                            reader.UncheckedRead(out ushort seglen);

                            if (seglen == 0)
                            {
                                if (mflags.Contains(Protocol.MessageFlags.Reliable) && peer.Session.State >= Protocol.State.Connected && channel == 0) // this is a ping
                                    peer.OnReceive(time, remoteTime, true, TrySetUsed(channel, channels), new Protocol.Message.Segment(channel, seq, rsn, default));

                                continue;
                            }

                            if (reader.Available >= seglen)
                            {
                                var data = new ArraySegment<byte>(reader.Buffer, reader.Position, seglen);
                                
                                if (peer.Session.State >= Protocol.State.Connected && channel < channels.Length)
                                    peer.OnReceive(time, remoteTime, mflags.Contains(Protocol.MessageFlags.Reliable), TrySetUsed(channel, channels), new Protocol.Message.Segment(channel, seq, rsn, data));

                                reader.UncheckedSkip(seglen);
                                continue;
                            }
                        }
                        goto Incomplete;
                    case Protocol.MessageFlags.Reliable | Protocol.MessageFlags.Data | Protocol.MessageFlags.Fragment: // CH(1) SEQ(2) RSN(2) SEGLEN(2) IDX(1) LEN(2) DAT(N)
                    case Protocol.MessageFlags.Data | Protocol.MessageFlags.Fragment:
                        if (reader.Available > Protocol.Message.Fragment.MinSize) 
                        {
                            reader.UncheckedRead(out byte channel);
                            reader.UncheckedRead(out Protocol.Ordinal seq);
                            reader.UncheckedRead(out Protocol.Ordinal rsn);
                            reader.UncheckedRead(out ushort seglen);
                            reader.UncheckedRead(out byte fragindex);
                            reader.UncheckedRead(out ushort fraglen);

                            if (fraglen == 0)
                                continue;

                            if (reader.Available >= fraglen)
                            {
                                // Invariants: 
                                //      seglen > mss;
                                //      mfs >= 256 (this is asserted by the property); 
                                //      1 <= fraglast <= 255; 
                                //      0 <= fragindex <= fraglast; 
                                //      fraglen == { mfs when fragindex < fraglast, (seglen % mfs) when fragindex == fraglast }
                                if (seglen > peer.MaxSegmentSize && channel < channels.Length)
                                {
                                    var mfs = peer.MaxFragmentSize;
                                    var fraglast = (byte)((seglen - 1) / mfs);
                                    if ((fragindex < fraglast && fraglen == mfs) || (fragindex == fraglast && fraglen == (seglen % mfs)))
                                    {
                                        var data = new ArraySegment<byte>(reader.Buffer, reader.Position, fraglen);

                                        if (peer.Session.State >= Protocol.State.Connected)
                                            peer.OnReceive(time, remoteTime, mflags.Contains(Protocol.MessageFlags.Reliable), TrySetUsed(channel, channels), new Protocol.Message.Fragment(channel, seq, rsn, fragindex, fraglast, seglen, data));
                                    }
                                }

                                reader.UncheckedSkip(fraglen);
                                continue;
                            }
                        }
                        goto Incomplete;
                    default:
                        goto Invalid;
                }
            }
            while (reader.TryRead(out mflags));

            return;

            Invalid:
            Log.Info($"Message parsing aborted with {reader.Available} bytes remaining in the packet. Invalid message: {mflags}.");
            Interlocked.Increment(ref peer.packetsDropped);
            return;

            Incomplete:
            Log.Info($"Message parsing aborted with {reader.Available} bytes remaining in the packet. Truncated message: {mflags}.");
            Interlocked.Increment(ref peer.packetsDropped);
            return;
        }

        internal Memory EncodeReset(BinaryWriter encoder, Protocol.Time time, uint remoteSession)
        {
            encoder.Reset();
            encoder.UncheckedWrite(time);
            encoder.UncheckedWrite(Protocol.PacketFlags.Reset);
            encoder.UncheckedWrite(remoteSession);

            var crc = Protocol.Packet.Insecure.Checksum.Compute(encoder.Buffer, encoder.Offset, encoder.Count);
            encoder.UncheckedWrite(crc);

            Allocate(out Memory encoded);
            encoded.CopyFrom(encoder.Buffer, encoder.Offset, encoder.Count);
            return encoded;
        }

        internal Memory EncodeReset(BinaryWriter encoder, Protocol.Time time, ref Session session)
        {
            encoder.Reset();
            encoder.UncheckedWrite(time);
            encoder.UncheckedWrite(Protocol.PacketFlags.Secure | Protocol.PacketFlags.Reset);
            encoder.UncheckedWrite(in Keys.Public);

            // A reset is supposed to be the last packet in a session anyway, so it shouldn't be such a 
            // security risk to use a constant nonce64 (0) here. It saves us from having to lock or perform
            // an interlocked increment of the Session.Nonce.
            var nonce = new Nonce((uint)time, 0, 0);            
            session.Cipher.Sign(encoder.Buffer, encoder.Offset, Protocol.Packet.Header.Size, 0, in nonce, out Mac mac);

            encoder.UncheckedWrite((ulong)0);
            encoder.UncheckedWrite(in mac);

            Allocate(out Memory encoded);
            encoded.CopyFrom(encoder.Buffer, encoder.Offset, encoder.Count);
            return encoded;
        }

        #endregion

        #region Time Source

        /// <summary>
        /// Measures elapsed time using a <see cref="Stopwatch"/>.
        /// </summary>
        /// <remarks>
        /// Time source is implemented on top of the <see cref="Stopwatch"/> and relies on the target platform supporting a high resolution timer.
        /// Note that the <see cref="Stopwatch"/> is still subject to a measurable drift when compared to the system clock which may accumulate over a short period of time. When there
        /// is a constant clock skew between two clocks, the clock offset between them gradually increases or decreases over time, depending on the sign of the skew. The amount of increase
        /// or decrease in the clock offset is proportional to the time duration of observation. Some sources like https://www.codeproject.com/articles/792410/high-resolution-clock-in-csharp 
        /// suggest that this drift must be aproximately 0.02% that is 0.0002s per second (ie. every millisecond actually lasts 1±0.02ms). It remains to be verified however if the 
        /// <see cref="Stopwatch"/> accuracy may vary at higher rates or if it may be suject to negative effects due to runtime system adjustments such as CPU throttling.
        /// <para/>
        /// This should not be a problem since the time source is internally used to calculate relatively small time durations between correlate timestamps (offsets) and never as a source 
        /// of absolute time references to be used externally or compared to the system clock. 
        /// <para/>
        /// In regard to `RTT` estimation, the variability of measured values may be so high, as pointed out by [Sessini and Mahanti](https://pages.cpsc.ucalgary.ca/~mahanti/papers/spects.submission.pdf) 
        /// that a time source skew of 0.02% is probably going to pass unnoticed.         
        /// </remarks>
        internal readonly struct TimeSource // internal for testing
        {
            private readonly TickCounter counter;
            private readonly uint start;

            public TimeSource(DateTime from) => (start, counter) = ((uint)(from.Ticks / TimeSpan.TicksPerMillisecond), new TickCounter(TickCounter.GetTicks()));

            /// <summary>
            /// Number of milliseconds elapsed since 12:00:00 midnight, January 1, 0001 in the Gregorian calendar mod 2^32. 
            /// May drift in relation to the actual system clock.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public uint Timestamp() => unchecked(start + (uint)counter.ElapsedMilliseconds());

            /// <summary>
            /// Number of ticks elapsed since creation of the time source.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public long ElapsedTicks() => counter.ElapsedTicks();

            /// <summary>
            /// Return the time stamp corresponding to the provided <paramref name="ticks"/> elapsed sincce creation of the time source.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public uint ElapsedTicksToTimestamp(long ticks) => unchecked(start + (uint)TickCounter.TicksToMilliseconds(ticks));
        }

        private readonly TimeSource timeSource = new TimeSource(DateTime.UtcNow);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal uint Timestamp() => timeSource.Timestamp();

        #endregion

        #region Pools

        private Memory.Pool memoryPool;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Allocate(out Memory instance) => instance = memoryPool.Get();

        private Channel.Outbound.Message.Pool outboundMessagePool;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Allocate(out Channel.Outbound.Message instance) => instance = outboundMessagePool.Get();

        private Channel.Inbound.Message.Pool inboundMessagePool;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Allocate(out Channel.Inbound.Message instance) => instance = inboundMessagePool.Get();

        private Channel.Inbound.Reassembly.Pool inboundReassemblyPool;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Allocate(out Channel.Inbound.Reassembly instance) => instance = inboundReassemblyPool.Get();

        #endregion
    }    
}
