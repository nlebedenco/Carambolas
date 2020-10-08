using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

using SystemSocket = System.Net.Sockets.Socket;
using SystemIPAddress = System.Net.IPAddress;
using SystemIPEndPoint = System.Net.IPEndPoint;

namespace Carambolas.Net.Sockets
{
    public enum SocketMode
    {
        NonBlocking = 0,
        Blocking = 1        
    }

    public enum AddressMode
    {
        IPv4,
        IPv6,
        Dual
    }

    public sealed partial class Socket: IDisposable
    {
        public static bool OSSupportsIPv4 => SystemSocket.OSSupportsIPv4;
        public static bool OSSupportsIPv6 => SystemSocket.OSSupportsIPv6;

        private readonly ISocket socket;

        /// <summary>
        /// Indicates the type of internet protocol stack under use.
        /// Note that some operating systems may not support dual stack mode 
        /// and yet accept the socket option without reporting an error.
        /// </summary>
        public readonly AddressMode AddressMode;

        public AddressFamily AddressFamily => socket.AddressFamily;

        public IPEndPoint LocalEndPoint => socket.LocalEndPoint;

        public readonly bool Blocking;
        public readonly byte TTL;
        public readonly int ReceiveBufferSize;
        public readonly int SendBufferSize;        

        public int Available => socket.Available;

        public Socket(in IPEndPoint endPoint) : this(in endPoint, in Settings.Default, Log.Default) { }
        public Socket(in IPEndPoint endPoint, in Settings settings) : this(in endPoint, in settings, Log.Default) { }

#if USE_NATIVE_SOCKET
        /// <summary>
        /// Zero if a log message has not been issued yet indicating that the native library is in use.
        /// </summary>
        private static int logged;
#endif

        public Socket(in IPEndPoint endPoint, ILog log) : this(in endPoint, in Settings.Default, log) { }        
        public Socket(in IPEndPoint endPoint, in Settings settings, ILog log)
        {
            var addressFamily = endPoint.Address.AddressFamily;
            if ((addressFamily == AddressFamily.InterNetwork && !OSSupportsIPv4)
             || (addressFamily == AddressFamily.InterNetworkV6 && !OSSupportsIPv6))
                throw new NotSupportedException(string.Format(SR.Socket.AddressFamilyNotSupported, addressFamily));

            if (socket != null)
                throw new SocketException((int)SocketError.IsConnected);

#if USE_NATIVE_SOCKET
            try
            {
                socket = new Native.Socket(addressFamily);
                if (logged == 0 && Interlocked.Exchange(ref logged, 1) == 0)
                    log.Info($"Using {typeof(Native.Socket).FullName}");
            }
            catch (DllNotFoundException)
            {
                socket = new Fallback.Socket(addressFamily);
            }
#else       
            socket = new Fallback.Socket(addressFamily);
#endif

            try
            {                
                if (addressFamily == AddressFamily.InterNetworkV6)
                {
                    AddressMode = AddressMode.Dual;

                    // Let this IPv6 socket accept IPv4 connections.
                    socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
                }

                // On windows we have to use Policy-based Quality of Service(QoS) to set DSCP in IP packets.
                // Usually the configuration of a Policy-based Quality of Service is done by defining a 
                // Group Policy Object(GPO) in the Group Policy Management Console(GPMC).
                // This could be done for a single computer or distributed to a number of computers via the domain controller.
                // Note that the configuration option at "Policy-based QoS->Advanced QoS Settings->DSCP Marking Override" DOES NOT work as expected.
                // See https://social.technet.microsoft.com/Forums/en-US/eb440e1c-1fb0-4fa0-9801-3b9ae128f9ad/dscp-marking-override?forum=win10itpronetworking
                // Decent platforms may still support this option as expected so we still set it here 
                // despite microsoft explicitly stating that we shouldn't.
                if (addressFamily == AddressFamily.InterNetwork)
                    socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.TypeOfService, (int)TOS.LowDelay);

                socket.Blocking = settings.Mode == SocketMode.Blocking;
                Blocking = socket.Blocking;

                socket.ReceiveTimeout = settings.ReceiveTimeout;
                socket.SendTimeout = settings.SendTimeout;

                if (settings.ReceiveBufferSize > 0)
                {
                    socket.ReceiveBufferSize = ReceiveBufferSize = settings.ReceiveBufferSize;
                    if (socket.ReceiveBufferSize < ReceiveBufferSize)
                        throw new SocketException((int)SocketError.NoBufferSpaceAvailable);
                }
                else
                {
                    ReceiveBufferSize = socket.ReceiveBufferSize;
                }

                if (settings.SendBufferSize > 0)
                {
                    socket.SendBufferSize = SendBufferSize = settings.SendBufferSize;
                    if (socket.SendBufferSize < SendBufferSize)
                        throw new SocketException((int)SocketError.NoBufferSpaceAvailable);
                }
                else
                {
                    SendBufferSize = socket.SendBufferSize;
                }

                if (addressFamily == AddressFamily.InterNetwork)
                {
                    if (settings.TTL > 0)
                    {
                        socket.Ttl = settings.TTL;
                        TTL = (byte)socket.Ttl;
                    }

                    socket.DontFragment = true;
                }
                else if (addressFamily == AddressFamily.InterNetworkV6)
                {
                    socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.HopLimit, settings.TTL);
                    TTL = (byte)socket.GetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.HopLimit);
                }

                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
                socket.ExclusiveAddressUse = true;

                try
                {
                    socket.Bind(in endPoint);
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode != SocketError.AddressAlreadyInUse || addressFamily != AddressFamily.InterNetworkV6)
                        throw;

                    // IPv6 binding could be having problems with dual mode (IPv4 compatibility). Disable and try again.
                    AddressMode = AddressMode.IPv6;
                    socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, true);
                    socket.Bind(in endPoint);
                }
            }
            catch
            {
                socket.Close();
                socket = null;
                throw;
            }
        }

        public void Close() => Dispose();
        public void Dispose() => socket.Dispose();

        public bool Poll(int microSeconds, SelectMode mode) => socket.Poll(microSeconds, mode);

        public int Receive(byte[] buffer, out IPEndPoint endPoint) => Receive(buffer, 0, buffer?.Length ?? throw new ArgumentNullException(nameof(buffer)), out endPoint);
        public int Receive(byte[] buffer, int offset, int size, out IPEndPoint endPoint) => (socket != null) ? UncheckedReceive(buffer, offset, size, out endPoint) : throw new ObjectDisposedException(GetType().FullName);
        public int Receive(byte[] buffer, int offset, int size, int millisecondsTimeout, out IPEndPoint endPoint)
        {
            if (millisecondsTimeout > int.MaxValue / 1000)
                throw new ArgumentOutOfRangeException(string.Format(SR.ArgumentIsGreaterThanMaximum, nameof(millisecondsTimeout), int.MaxValue / 1000), nameof(millisecondsTimeout));

            if (socket.Available == 0 && !socket.Poll(millisecondsTimeout * 1000, SelectMode.SelectRead))
            {
                endPoint = default;
                return 0;
            }

            return UncheckedReceive(buffer, offset, size, out endPoint);
        }

        internal int UncheckedReceive(byte[] buffer, int offset, int size, out IPEndPoint endPoint)
        {
            try
            {
                return socket.ReceiveFrom(buffer, offset, size, out endPoint);
            }
            catch (SocketException e)
            {
                switch (e.SocketErrorCode)
                {
                    case SocketError.MessageSize:
                    case SocketError.NoBufferSpaceAvailable:
                    case SocketError.TimedOut:
                    case SocketError.WouldBlock:
                        endPoint = default;
                        return 0;
                    default:
                        throw;
                }
            }
        }

        public int Send(byte[] buffer, in IPEndPoint endPoint) => Send(buffer, 0, buffer.Length, endPoint);
        public int Send(byte[] buffer, int offset, int size, in IPEndPoint endPoint) => (socket != null) ? UncheckedSend(buffer, offset, size, in endPoint) : throw new ObjectDisposedException(GetType().FullName);
        public int Send(byte[] buffer, int offset, int size, int millisecondsTimeout, in IPEndPoint endPoint)
        {
            if (millisecondsTimeout > int.MaxValue / 1000)
                throw new ArgumentOutOfRangeException(string.Format(SR.ArgumentIsGreaterThanMaximum, nameof(millisecondsTimeout), int.MaxValue / 1000), nameof(millisecondsTimeout));

            return socket.Poll(millisecondsTimeout * 1000, SelectMode.SelectWrite) ? UncheckedSend(buffer, offset, size, in endPoint) : 0;
        }

        internal int UncheckedSend(byte[] buffer, int offset, int size, in IPEndPoint endPoint)
        {
            try
            {
                return socket.SendTo(buffer, offset, size, in endPoint);
            }
            catch (SocketException e)
            {
                switch (e.SocketErrorCode)
                {
                    case SocketError.MessageSize:
                        // Datagram is never going to be delivered so it's as good as dropped. Assume sent and lost.
                        return size;
                    case SocketError.ConnectionReset:
                    case SocketError.NoBufferSpaceAvailable:
                    case SocketError.TimedOut:
                    case SocketError.WouldBlock:
                        // Datagram still has a chance to be delivered if the caller is interested in retrying so return 0 to let the caller know.
                        return 0;
                    default:
                        throw;
                }
            }
        }       
    }

    internal interface ISocket: IDisposable
    {
        AddressFamily AddressFamily { get; }

        IPEndPoint LocalEndPoint { get; }

        bool Blocking { get; set; }

        int Available { get; }

        bool IsBound { get; }

        bool ExclusiveAddressUse { get; set; }

        int ReceiveBufferSize { get; set; }

        int SendBufferSize { get; set; }

        int ReceiveTimeout { get; set; }

        int SendTimeout { get; set; }

        short Ttl { get; set; }

        bool DontFragment { get; set; }

        bool DualMode { get; set; }

        void SetIPProtectionLevel(IPProtectionLevel level);

        void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, bool optionValue);

        void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, int optionValue);

        int GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName);

        void Bind(in IPEndPoint endPoint);

        bool Poll(int microSeconds, SelectMode mode);

        int ReceiveFrom(byte[] buffer, int offset, int size, out IPEndPoint endPoint);

        int SendTo(byte[] buffer, int offset, int size, in IPEndPoint endPoint);

        void Close();
    }

#if USE_NATIVE_SOCKET
    internal static class Native
    {
        public sealed class Socket: ISocket
        {
            private static readonly object sync = new object();
            private static bool initialized;

            private static void Initialize()
            {
                if (initialized)
                    return;

                lock (sync)
                {
                    if (initialized)
                        return;

                    if (Native.Initialize() != 0)
                        throw new SocketException((int)SocketError.SocketError);

                    initialized = true;
                }
            }

            public Socket(AddressFamily addressFamily)
            {
                Initialize();
                var socketError = Native.Open(addressFamily, out handle);
                if (socketError != SocketError.Success)
                    throw new SocketException((int)socketError);

                this.addressFamily = addressFamily;
            }

            private int handle;
            private bool blocking;

            public bool Blocking
            {
                get => blocking;
                set
                {
                    if (handle < 0)
                        throw new ObjectDisposedException(GetType().FullName);

                    var socketError = Native.SetBlocking(handle, value ? 1 : 0);
                    if (socketError != SocketError.Success)
                        throw new SocketException((int)socketError);

                    blocking = value;
                }
            }

            #pragma warning disable IDE0044
            private AddressFamily addressFamily;
            public AddressFamily AddressFamily => addressFamily;
            #pragma warning restore IDE0044

            public int Available
            {
                get
                {
                    if (handle < 0)
                        throw new ObjectDisposedException(GetType().FullName);

                    var socketError = Native.Available(handle, out int value);
                    if (socketError != SocketError.Success)
                        throw new SocketException((int)socketError);

                    return value;
                }
            }

            public IPEndPoint LocalEndPoint { get; private set; }

            public bool IsBound => LocalEndPoint != default;

            public bool ExclusiveAddressUse
            {
                get => GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse) != 0;

                set
                {
                    if (IsBound)
                        throw new InvalidOperationException(SR.Socket.AlreadyBound);

                    SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, value ? 1 : 0);
                }
            }

            public int ReceiveBufferSize
            {
                get => GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer);

                set
                {
                    if (value < 0)
                        throw new ArgumentOutOfRangeException(nameof(value));

                    SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, value);
                }
            }

            public int SendBufferSize
            {
                get => GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer);

                set
                {
                    if (value < 0)
                        throw new ArgumentOutOfRangeException(nameof(value));

                    SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, value);
                }
            }

            public int ReceiveTimeout
            {
                get => GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout);

                set
                {
                    if (value < -1)
                        throw new ArgumentOutOfRangeException(nameof(value));

                    if (value == -1)
                        value = 0;

                    SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, value);
                }
            }

            public int SendTimeout
            {
                get => GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout);

                set
                {
                    if (value < -1)
                        throw new ArgumentOutOfRangeException(nameof(value));
                    if (value == -1)
                        value = 0;

                    SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, value);
                }
            }

            public short Ttl
            {
                get
                {
                    if (addressFamily == AddressFamily.InterNetwork)
                        return (short)GetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive);

                    if (addressFamily == AddressFamily.InterNetworkV6)
                        return (short)GetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IpTimeToLive);

                    throw new NotSupportedException(string.Format(SR.Socket.AddressFamilyNotSupported, addressFamily));
                }

                set
                {
                    if (value < 0 || value > byte.MaxValue)
                        throw new ArgumentOutOfRangeException(nameof(value));

                    if (addressFamily == AddressFamily.InterNetwork)
                        SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, (int)value);
                    else if (addressFamily == AddressFamily.InterNetworkV6)
                        SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IpTimeToLive, (int)value);
                    else
                        throw new NotSupportedException(string.Format(SR.Socket.AddressFamilyNotSupported, addressFamily));
                }
            }

            public bool DontFragment
            {
                get => addressFamily == AddressFamily.InterNetwork && GetSocketOption(SocketOptionLevel.IP, SocketOptionName.DontFragment) != 0;

                set
                {
                    if (addressFamily != AddressFamily.InterNetwork)
                        throw new NotSupportedException(string.Format(SR.Socket.AddressFamilyNotSupported, addressFamily));

                    SetSocketOption(SocketOptionLevel.IP, SocketOptionName.DontFragment, value ? 1 : 0);
                }
            }

            public bool DualMode
            {
                get => (addressFamily == AddressFamily.InterNetworkV6) && GetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only) == 0;

                set
                {
                    if (addressFamily != AddressFamily.InterNetworkV6)
                        throw new NotSupportedException(string.Format(SR.Socket.AddressFamilyNotSupported, addressFamily));

                    SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, value ? 0 : 1);
                }
            }

            public void SetIPProtectionLevel(IPProtectionLevel level)
            {
                if (level == IPProtectionLevel.Unspecified)
                    throw new ArgumentException("Invalid value", nameof(level));

                if (addressFamily == AddressFamily.InterNetwork)
                    SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IPProtectionLevel, (int)level);
                else if (addressFamily == AddressFamily.InterNetworkV6)
                    SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPProtectionLevel, (int)level);

                throw new NotSupportedException(string.Format(SR.Socket.AddressFamilyNotSupported, addressFamily));
            }

            public void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, bool optionValue) => SetSocketOption(optionLevel, optionName, optionValue ? 1 : 0);

            public void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, int optionValue)
            {
                if (handle < 0)
                    throw new ObjectDisposedException(GetType().FullName);

                CheckSetOptionPermissions(optionLevel, optionName);

                var socketError = Native.SetSocketOption(handle, optionLevel, optionName, optionValue);
                if (socketError != SocketError.Success)
                    throw new SocketException((int)socketError);
            }

            public int GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName)
            {
                if (handle < 0)
                    throw new ObjectDisposedException(GetType().FullName);

                var socketError = Native.GetSocketOption(handle, optionLevel, optionName, out int optionValue);
                if (socketError != SocketError.Success)
                    throw new SocketException((int)socketError);

                return optionValue;
            }

            private static void CheckSetOptionPermissions(SocketOptionLevel optionLevel, SocketOptionName optionName)
            {
                if (optionLevel == SocketOptionLevel.Udp && (optionName == SocketOptionName.Debug || optionName == SocketOptionName.ChecksumCoverage)
                 || optionLevel == SocketOptionLevel.Socket && (optionName == SocketOptionName.ReuseAddress || optionName == SocketOptionName.DontLinger || optionName == SocketOptionName.SendBuffer || optionName == SocketOptionName.ReceiveBuffer || optionName == SocketOptionName.SendTimeout || optionName == SocketOptionName.ExclusiveAddressUse || optionName == SocketOptionName.ReceiveTimeout)
                 || optionLevel == SocketOptionLevel.IP && (optionName == SocketOptionName.TypeOfService || optionName == SocketOptionName.IpTimeToLive || optionName == SocketOptionName.DontFragment)
                 || optionLevel == SocketOptionLevel.IPv6 && (optionName == SocketOptionName.IpTimeToLive || optionName == SocketOptionName.IPProtectionLevel || optionName == SocketOptionName.IPv6Only || optionName == SocketOptionName.HopLimit))
                    return;

                throw new NotSupportedException();
            }

            public void Bind(in IPEndPoint endPoint)
            {
                if (handle < 0)
                    throw new ObjectDisposedException(GetType().FullName);

                IPEndPoint localEndPoint = endPoint;
                var socketError = Native.Bind(handle, ref localEndPoint);
                if (socketError != SocketError.Success)
                    throw new SocketException((int)socketError);

                LocalEndPoint = localEndPoint;
            }

            public bool Poll(int microSeconds, SelectMode mode)
            {
                if (handle < 0)
                    throw new ObjectDisposedException(GetType().FullName);

                var socketError = Native.Poll(handle, microSeconds, mode, out int result);
                if (socketError != SocketError.Success)
                    throw new SocketException((int)socketError);

                return result > 0;
            }

            public int ReceiveFrom(byte[] buffer, int offset, int size, out IPEndPoint endPoint)
            {
                if (handle < 0)
                    throw new ObjectDisposedException(GetType().FullName);

                var socketError = Native.ReceiveFrom(handle, buffer, offset, size, out endPoint, out int nbytes);
                if (socketError != SocketError.Success)
                    throw new SocketException((int)socketError);

                return nbytes;
            }

            public int SendTo(byte[] buffer, int offset, int size, in IPEndPoint endPoint)
            {
                if (handle < 0)
                    throw new ObjectDisposedException(GetType().FullName);

                var socketError = Native.SendTo(handle, buffer, offset, size, in endPoint, out int nbytes);
                if (socketError != SocketError.Success)
                    throw new SocketException((int)socketError);

                return nbytes;
            }

            public void Close() => Dispose();

            public void Dispose()
            {
                OnDisposed(true);
                GC.SuppressFinalize(this);
            }

            ~Socket() => OnDisposed(false);

            private void OnDisposed(bool disposing)
            {
                var value = Interlocked.Exchange(ref handle, -1);
                if (value < 0)
                    return;

                Native.Close(value);
            }
        }

#if __IOS__ || UNITY_IOS && !UNITY_EDITOR
        private const string nativeLibrary = "__Internal";
#else
        private const string nativeLibrary = "Carambolas.Net.Native.dll";
#endif

        [DllImport(nativeLibrary, EntryPoint = "carambolas_net_initialize", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Initialize();

        [DllImport(nativeLibrary, EntryPoint = "carambolas_net_socket_open", CallingConvention = CallingConvention.Cdecl)]
        public static extern SocketError Open(AddressFamily addressFamily, out int sockfd);

        [DllImport(nativeLibrary, EntryPoint = "carambolas_net_socket_close", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Close(int sockfd);

        [DllImport(nativeLibrary, EntryPoint = "carambolas_net_socket_setsockopt", CallingConvention = CallingConvention.Cdecl)]
        public static extern SocketError SetSocketOption(int sockfd, SocketOptionLevel level, SocketOptionName optionName, int optionValue);

        [DllImport(nativeLibrary, EntryPoint = "carambolas_net_socket_getsockopt", CallingConvention = CallingConvention.Cdecl)]
        public static extern SocketError GetSocketOption(int sockfd, SocketOptionLevel level, SocketOptionName optionName, out int optionValue);

        [DllImport(nativeLibrary, EntryPoint = "carambolas_net_socket_setblocking", CallingConvention = CallingConvention.Cdecl)]
        public static extern SocketError SetBlocking(int sockfd, int value);

        [DllImport(nativeLibrary, EntryPoint = "carambolas_net_socket_setconnreset", CallingConvention = CallingConvention.Cdecl)]
        public static extern SocketError SetConnReset(int sockfd, int value);

        [DllImport(nativeLibrary, EntryPoint = "carambolas_net_socket_bind", CallingConvention = CallingConvention.Cdecl)]
        public static extern SocketError Bind(int sockfd, ref IPEndPoint endPoint);

        [DllImport(nativeLibrary, EntryPoint = "carambolas_net_socket_available", CallingConvention = CallingConvention.Cdecl)]
        public static extern SocketError Available(int sockfd, out int value);

        [DllImport(nativeLibrary, EntryPoint = "carambolas_net_socket_poll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SocketError Poll(int sockfd, int microSeconds, SelectMode mode, out int result);

        [DllImport(nativeLibrary, EntryPoint = "carambolas_net_socket_recvfrom", CallingConvention = CallingConvention.Cdecl)]
        public static extern SocketError ReceiveFrom(int sockfd, byte[] buffer, int offset, int size, out IPEndPoint endPoint, out int nbytes);

        [DllImport(nativeLibrary, EntryPoint = "carambolas_net_socket_sendto", CallingConvention = CallingConvention.Cdecl)]
        public static extern SocketError SendTo(int sockfd, byte[] buffer, int offset, int size, in IPEndPoint endPoint, out int nbytes);
    }

#endif
    
    internal static class Fallback
    {
        public sealed class Socket: ISocket
        {
            private SystemSocket socket;

            public Socket(AddressFamily addressFamily) => socket = new SystemSocket(addressFamily, SocketType.Dgram, ProtocolType.Udp);
            
            public bool Blocking
            {
                get => socket.Blocking;
                set => socket.Blocking = value;
            }

            public AddressFamily AddressFamily => socket.AddressFamily;

            public int Available => socket.Available;

            public IPEndPoint LocalEndPoint { get; private set; }

            public bool IsBound => socket.IsBound;

            public bool ExclusiveAddressUse
            {
                get => socket.ExclusiveAddressUse;
                set => socket.ExclusiveAddressUse = value;
            }

            public int ReceiveBufferSize
            {
                get => socket.ReceiveBufferSize;
                set => socket.ReceiveBufferSize = value;
            }

            public int SendBufferSize
            {
                get => socket.SendBufferSize;
                set => socket.SendBufferSize = value;
            }

            public int ReceiveTimeout
            {
                get => socket.ReceiveTimeout;
                set => socket.ReceiveTimeout = value;
            }

            public int SendTimeout
            {
                get => socket.SendTimeout;
                set => socket.ReceiveTimeout = value;
            }

            public short Ttl
            {
                get => socket.Ttl;
                set => socket.Ttl = value;
            }

            public bool DontFragment
            {
                get => socket.DontFragment;

                set => socket.DontFragment = value;
            }

            public bool DualMode
            {
                get => socket.DualMode;

                set => socket.DualMode = value;
            }

            public void SetIPProtectionLevel(IPProtectionLevel level) => socket.SetIPProtectionLevel(level);

            public void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, bool optionValue) => socket.SetSocketOption(optionLevel, optionName, optionValue);

            public void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, int optionValue) => socket.SetSocketOption(optionLevel, optionName, optionValue);

            public int GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName) => (int)socket.GetSocketOption(optionLevel, optionName);

            private readonly byte[] ipv4 = new byte[4];
            private readonly byte[] ipv6 = new byte[16];

            private const int SIO_UDP_CONNRESET = -1744830452; //SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12
            private readonly static byte[] DISABLED = new byte[] { 0 };

            public void Bind(in IPEndPoint endPoint)
            {
                if (endPoint.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    endPoint.Address.SerializeIPv4(ipv4);
                    socket.Bind(new SystemIPEndPoint(new SystemIPAddress(ipv4), endPoint.Port));
                }
                else
                {
                    endPoint.Address.SerializeIPv6(ipv6);
                    socket.Bind(new SystemIPEndPoint(new SystemIPAddress(ipv6), endPoint.Port));
                }

                // Ignore ICMP PORT_UNREACHABLE packets.
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    socket.IOControl(SIO_UDP_CONNRESET, DISABLED, null);

                LocalEndPoint = new IPEndPoint(socket.LocalEndPoint as SystemIPEndPoint);
            }

            public bool Poll(int microSeconds, SelectMode mode) => socket.Poll(microSeconds, mode);

            public int ReceiveFrom(byte[] buffer, int offset, int size, out IPEndPoint endPoint)
            {
                var ep = default(EndPoint);
                var length = socket.ReceiveFrom(buffer, offset, size, SocketFlags.None, ref ep);
                if (ep is SystemIPEndPoint ip)
                {
                    endPoint = new IPEndPoint(new IPAddress(ip.Address.GetAddressBytes()), (ushort)ip.Port);
                    return length;
                }

                endPoint = default;
                return 0;
            }

            public int SendTo(byte[] buffer, int offset, int size, in IPEndPoint endPoint)
            {
                SystemIPEndPoint ip;
                if (endPoint.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    endPoint.Address.SerializeIPv4(ipv4);
                    ip = new SystemIPEndPoint(new SystemIPAddress(ipv4), endPoint.Port);
                }
                else
                {
                    endPoint.Address.SerializeIPv6(ipv6);
                    ip = new SystemIPEndPoint(new SystemIPAddress(ipv6), endPoint.Port);
                }
            
                return socket.SendTo(buffer, offset, size, SocketFlags.None, ip);
            }
            public void Close() => socket.Close();

            public void Dispose() => socket.Dispose();
        }
    }
}
