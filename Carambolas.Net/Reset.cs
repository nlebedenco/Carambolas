using System;
using System.Runtime.InteropServices;

namespace Carambolas.Net
{
    [StructLayout(LayoutKind.Auto)]
    internal readonly struct Reset: IEquatable<Reset>
    {
        public readonly IPEndPoint EndPoint;
        public readonly uint Session;
        public readonly Memory Encoded;

        public Reset(in IPEndPoint endPoint, uint session, Memory encoded) => (EndPoint, Session, Encoded) = (endPoint, session, encoded);

        private static bool Equals(in Reset a, in Reset b) => a.EndPoint == b.EndPoint && a.Session == b.Session;

        public bool Equals(Reset other) => Equals(in this, in other);

        public override bool Equals(object obj) => obj is Reset other && Equals(in this, in other);

        public override int GetHashCode()
        {
            var h1 = EndPoint.GetHashCode();
            var h2 = (int)Session;
            return (int)((uint)(h1 << 5) | (uint)h1 >> 27) + h1 ^ h2;
        }

        public override string ToString() => $"({EndPoint}, {Session})";

        public static bool operator ==(in Reset a, in Reset b) => Equals(in a, in b);
        public static bool operator !=(in Reset a, in Reset b) => !Equals(in a, in b);
    }
}
