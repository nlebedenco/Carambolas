using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SystemIPAddress = System.Net.IPAddress;
using SystemIPEndPoint = System.Net.IPEndPoint;

namespace Carambolas.Net
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct IPEndPoint: IEquatable<IPEndPoint>, IComparable<IPEndPoint>, IComparable
    {
        public static readonly IPEndPoint Any = new IPEndPoint(IPAddress.Any, 0);
        public static readonly IPEndPoint IPv6Any = new IPEndPoint(IPAddress.IPv6Any, 0);

        public readonly IPAddress Address;
        public readonly ushort Port;
        
        public IPEndPoint(string address, ushort port) : this(string.IsNullOrEmpty(address) ? IPAddress.Any : IPAddress.Parse(address), port) { }
        public IPEndPoint(SystemIPEndPoint endPoint) : this(new IPAddress(endPoint.Address), (ushort)endPoint.Port) { }
        public IPEndPoint(SystemIPAddress address, ushort port) : this(new IPAddress(address), port) { }
        public IPEndPoint(in IPAddress address, ushort port)
        {
            this.Port = port;
            this.Address = address;
        }

        public int CompareTo(IPEndPoint other) => Compare(in this, in other);

        public int CompareTo(object obj) => obj is IPEndPoint other ? Compare(in this, in other) : throw new ArgumentException(string.Format(SR.ArgumentMustBeOfType, GetType().FullName), nameof(obj));

        public bool Equals(IPEndPoint other) => Compare(in this, in other) == 0;

        public override bool Equals(object obj) => obj is IPEndPoint y && Compare(in this, in y) == 0;

        public override int GetHashCode()
        {
            var h1 = Address.GetHashCode();
            var h2 = (int)Port;
            return (int)((uint)(h1 << 5) | (uint)h1 >> 27) + h1 ^ h2;            
        }

        public override string ToString() => Address.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{Address}]:{Port}" : $"{Address}:{Port}";

        public static bool operator ==(IPEndPoint x, IPEndPoint y) => Compare(in x, in y) == 0;

        public static bool operator !=(IPEndPoint x, IPEndPoint y) => Compare(in x, in y) != 0;

        public static IPEndPoint Parse(string value)
        {
            var i = value?.LastIndexOf(":") ?? throw new ArgumentNullException(nameof(value));

            if (i < 0)
                throw new FormatException();

            return new IPEndPoint(value.Substring(0, i), Convert.ToUInt16(value.Substring(i + 1, value.Length - i - 1)));
        }

        public static bool TryParse(string value, out IPEndPoint endpoint)
        {
            if (string.IsNullOrEmpty(value))
            {
                endpoint = default;
                return false;
            }

            var i = value.LastIndexOf(":");

            if (i < 0 || !IPAddress.TryParse(value.Substring(0, i), out IPAddress address) || !ushort.TryParse(value.Substring(i + 1, value.Length - i - 1), out ushort port))
            {
                endpoint = default;
                return false;
            }

            endpoint = new IPEndPoint(address, port);
            return true;
        }

        private static int Compare(in IPEndPoint x, in IPEndPoint y)
        {
            var value = x.Address.CompareTo(y.Address);
            return value == 0 ? x.Port.CompareTo(y.Port) : value;
        }

        public sealed class Comparer: IEqualityComparer<IPEndPoint>
        {
            public readonly static Comparer Default = new Comparer();

            bool IEqualityComparer<IPEndPoint>.Equals(IPEndPoint x, IPEndPoint y) => Compare(x, y) == 0;

            int IEqualityComparer<IPEndPoint>.GetHashCode(IPEndPoint obj) => obj.GetHashCode();
        }
    }
}
