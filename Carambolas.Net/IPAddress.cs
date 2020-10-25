using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;

using SystemIPAddress = System.Net.IPAddress;

using Carambolas.Internal;

namespace Carambolas.Net
{
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public readonly struct IPAddress: IEquatable<IPAddress>, IComparable<IPAddress>, IComparable
    {
        public static readonly IPAddress Any = new IPAddress(AddressFamily.InterNetwork);

        public static readonly IPAddress Loopback = new IPAddress((uint)SystemIPAddress.NetworkToHostOrder((int)0x7F000001));

        public static readonly IPAddress Broadcast = new IPAddress(uint.MaxValue);

        public static readonly IPAddress IPv6Any = new IPAddress(AddressFamily.InterNetworkV6);

        public static readonly IPAddress IPv6Loopback = new IPAddress((ulong)SystemIPAddress.NetworkToHostOrder(0x0000000000000000L), (ulong)SystemIPAddress.NetworkToHostOrder(0x000000000000001L));

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        [FieldOffset(0)]
        private readonly ulong ipv6PackedAddress0;
        public ulong IPv6PackedAddress0 => ipv6PackedAddress0;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        [FieldOffset(8)]
        private readonly ulong ipv6PackedAddress1;
        public ulong IPv6PackedAddress1 => ipv6PackedAddress1;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        [FieldOffset(12)]
        private readonly uint ipv4PackedAddress;
        public uint IPv4PackedAddress => ipv4PackedAddress;

        [FieldOffset(16)]
        private readonly ushort addressFamily;
        public AddressFamily AddressFamily => (AddressFamily)addressFamily;

        internal IPAddress(AddressFamily addressFamily)
        {
            this.ipv6PackedAddress0 = 0;
            this.ipv6PackedAddress1 = 0;
            this.ipv4PackedAddress = 0;
            this.addressFamily = (ushort)addressFamily;
        }

        public IPAddress(uint address)
        {
            this.ipv6PackedAddress0 = 0;
            this.ipv6PackedAddress1 = 0;
            this.ipv4PackedAddress = address;
            this.addressFamily = (ushort)AddressFamily.InterNetwork;
        }

        public IPAddress(ulong msb, ulong lsb)
        {
            this.ipv4PackedAddress = 0;
            this.ipv6PackedAddress0 = msb;
            this.ipv6PackedAddress1 = lsb;
            this.addressFamily = (ushort)AddressFamily.InterNetworkV6;
        }

        public IPAddress(byte[] address)
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));

            var length = address.Length;
            if (length == 4)
            {
                this.ipv6PackedAddress0 = 0;
                this.ipv6PackedAddress1 = 0;
                this.ipv4PackedAddress = (uint)address[3] << 24 | (uint)address[2] << 16 | (uint)address[1] << 8 | (uint)address[0];
                this.addressFamily = (ushort)AddressFamily.InterNetwork;
            }
            else if (length == 16)
            {
                this.ipv4PackedAddress = 0;
                this.ipv6PackedAddress0 = ((ulong)address[7] << 56) | ((ulong)address[6] << 48) | ((ulong)address[5] << 40) | ((ulong)address[4] << 32) | ((ulong)address[3] << 24) | ((ulong)address[2] << 16) | ((ulong)address[1] << 8) | ((ulong)address[0]);
                this.ipv6PackedAddress1 = ((ulong)address[15] << 56) | ((ulong)address[14] << 48) | ((ulong)address[13] << 40) | ((ulong)address[12] << 32) | ((ulong)address[11] << 24) | ((ulong)address[10] << 16) | ((ulong)address[9] << 8) | ((ulong)address[8]);
                this.addressFamily = (ushort)AddressFamily.InterNetworkV6;
            }
            else
            {
                throw new ArgumentException(string.Format(Resources.GetString(Strings.Net.IPAddress.LengthMustBe4Or16), nameof(address)), nameof(address));
            }
        }

        public IPAddress(SystemIPAddress address) : this(address.GetAddressBytes()) { }

        internal void SerializeIPv4(byte[] array)
        {
            var v = this.ipv4PackedAddress;
            array[0] = (byte)(v);
            array[1] = (byte)(v >> 8);
            array[2] = (byte)(v >> 16);
            array[3] = (byte)(v >> 24);
        }

        internal void SerializeIPv6(byte[] array)
        {
            var v0 = this.ipv6PackedAddress0;
            var v1 = this.ipv6PackedAddress1;
            array[0] = (byte)(v0);
            array[1] = (byte)(v0 >> 8);
            array[2] = (byte)(v0 >> 16);
            array[3] = (byte)(v0 >> 24);
            array[4] = (byte)(v0 >> 32);
            array[5] = (byte)(v0 >> 40);
            array[6] = (byte)(v0 >> 48);
            array[7] = (byte)(v0 >> 56);
            array[8] = (byte)(v1);
            array[9] = (byte)(v1 >> 8);
            array[10] = (byte)(v1 >> 16);
            array[11] = (byte)(v1 >> 24);
            array[12] = (byte)(v1 >> 32);
            array[13] = (byte)(v1 >> 40);
            array[14] = (byte)(v1 >> 48);
            array[15] = (byte)(v1 >> 56);
        }

        public byte[] GetAddressBytes()
        {
            if (AddressFamily == AddressFamily.InterNetwork)
            {
                var array = new byte[4];
                SerializeIPv4(array);
                return array;
            }
            else
            {
                var array = new byte[16];
                SerializeIPv6(array);
                return array;
            }
        }

        public int CompareTo(IPAddress other) => Compare(this, other);

        public int CompareTo(object obj) => obj is IPAddress other ? Compare(in this, in other) 
            : throw new ArgumentException(string.Format(Resources.GetString(Strings.ArgumentMustBeOfType), GetType().FullName), nameof(obj));

        public bool Equals(IPAddress other) => Compare(this, other) == 0;

        public override bool Equals(object obj) => obj is IPAddress address && Compare(this, address) == 0;

        public override int GetHashCode()
        {
            var h1 = (int)(ipv6PackedAddress0 ^ (ipv6PackedAddress0 >> 32));
            var h2 = (int)(ipv6PackedAddress1 ^ (ipv6PackedAddress1 >> 32));
            return (int)((uint)(h1 << 5) | (uint)h1 >> 27) + h1 ^ h2;
        }

        public override string ToString()
        {
            if (AddressFamily == AddressFamily.InterNetwork)
            {
                var a = (byte)(this.ipv4PackedAddress);
                var b = (byte)(this.ipv4PackedAddress >> 8);
                var c = (byte)(this.ipv4PackedAddress >> 16);
                var d = (byte)(this.ipv4PackedAddress >> 24);
                return $"{a}.{b}.{c}.{d}";
            }
            else
            {
                var v0 = this.ipv6PackedAddress0;
                var v1 = this.ipv6PackedAddress1;                
                return string.Format("{0:x}:{1:x}:{2:x}:{3:x}:{4:x}:{5:x}:{6:x}:{7:x}",
                    SystemIPAddress.NetworkToHostOrder((short)(v0)),
                    SystemIPAddress.NetworkToHostOrder((short)(v0 >> 16)),
                    SystemIPAddress.NetworkToHostOrder((short)(v0 >> 32)),
                    SystemIPAddress.NetworkToHostOrder((short)(v0 >> 48)),
                    SystemIPAddress.NetworkToHostOrder((short)(v1)),
                    SystemIPAddress.NetworkToHostOrder((short)(v1 >> 16)),
                    SystemIPAddress.NetworkToHostOrder((short)(v1 >> 32)),
                    SystemIPAddress.NetworkToHostOrder((short)(v1 >> 48))
                );
            }
        }

        public static bool operator ==(IPAddress x, IPAddress y) => Compare(x, y) == 0;

        public static bool operator !=(IPAddress x, IPAddress y) => Compare(x, y) != 0;

        public static IPAddress Parse(string ipString) => new IPAddress(SystemIPAddress.Parse(ipString));

        public static bool TryParse(string ipString, out IPAddress address)
        {
            if (!SystemIPAddress.TryParse(ipString, out SystemIPAddress sysaddr))
            {
                address = default;
                return false;
            }

            address = new IPAddress(sysaddr);
            return true;
        }

        public static IPAddress operator &(IPAddress a, IPAddress b) => new IPAddress(a.ipv6PackedAddress0 & b.ipv6PackedAddress0, a.ipv6PackedAddress1 & b.ipv6PackedAddress1);

        private static int Compare(in IPAddress x, in IPAddress y)
        {
            var value = x.ipv6PackedAddress0.CompareTo(y.ipv6PackedAddress0);
            return value == 0 ? x.ipv6PackedAddress1.CompareTo(y.ipv6PackedAddress1) : value;
        }

        public sealed class Comparer: IComparer<IPAddress>, IEqualityComparer<IPAddress>
        {
            public static readonly Comparer Default = new Comparer();

            int IComparer<IPAddress>.Compare(IPAddress x, IPAddress y) => Compare(x, y);

            bool IEqualityComparer<IPAddress>.Equals(IPAddress x, IPAddress y) => Compare(x, y) == 0;

            int IEqualityComparer<IPAddress>.GetHashCode(IPAddress obj) => obj.GetHashCode();
        }
    }
}
