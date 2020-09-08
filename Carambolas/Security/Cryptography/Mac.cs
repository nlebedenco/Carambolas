using System;
using System.Diagnostics;

namespace Carambolas.Security.Cryptography
{
    public readonly struct Mac: IEquatable<Mac>
    {
        public const int Size = 16;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly uint k0;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly uint k1;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly uint k2;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly uint k3;

        public Mac(uint a, uint b, uint c, uint d)
        {
            k0 = a;
            k1 = b;
            k2 = c;
            k3 = d;
        }

        public Mac(byte[] array, int index = 0)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            if (index > (array.Length - Size))
                throw new ArgumentException(string.Format(SR.IndexOutOfRangeOrNumberOfElementsIsLessThanMinimum, nameof(index), Size, nameof(array)), nameof(array));

            k0 = ((uint)array[index +  0]) | ((uint)array[index +  1] << 8) | ((uint)array[index +  2] << 16) | ((uint)array[index +  3] << 24);
            k1 = ((uint)array[index +  4]) | ((uint)array[index +  5] << 8) | ((uint)array[index +  6] << 16) | ((uint)array[index +  7] << 24);
            k2 = ((uint)array[index +  8]) | ((uint)array[index +  9] << 8) | ((uint)array[index + 10] << 16) | ((uint)array[index + 11] << 24);
            k3 = ((uint)array[index + 12]) | ((uint)array[index + 13] << 8) | ((uint)array[index + 14] << 16) | ((uint)array[index + 15] << 24);
        }

        public void CopyTo(byte[] array, int index = 0)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            if (index < 0)
                throw new ArgumentNullException(nameof(index));

            if (index > (array.Length - Size))
                throw new ArgumentException(string.Format(SR.IndexOutOfRangeOrNumberOfElementsIsLessThanMinimum, nameof(index), Size, nameof(array)), nameof(array));

            array[index + 0]  = (byte)(k0 >> 0);
            array[index + 1]  = (byte)(k0 >> 8);
            array[index + 2]  = (byte)(k0 >> 16);
            array[index + 3]  = (byte)(k0 >> 24);
            array[index + 4]  = (byte)(k1 >> 0);
            array[index + 5]  = (byte)(k1 >> 8);
            array[index + 6]  = (byte)(k1 >> 16);
            array[index + 7]  = (byte)(k1 >> 24);
            array[index + 8]  = (byte)(k2 >> 0);
            array[index + 9]  = (byte)(k2 >> 8);
            array[index + 10] = (byte)(k2 >> 16);
            array[index + 11] = (byte)(k2 >> 24);
            array[index + 12] = (byte)(k3 >> 0);
            array[index + 13] = (byte)(k3 >> 8);
            array[index + 14] = (byte)(k3 >> 16);
            array[index + 15] = (byte)(k3 >> 24);
        }

        public byte[] ToArray()
        {
            var a = new byte[Size];
            CopyTo(a);
            return a;
        }

        public void Deconstruct(out uint t0, out uint t1, out uint t2, out uint t3) => (t0, t1, t2, t3) = (k0, k1, k2, k3);

        public bool Equals(Mac other) => Equals(this, other);

        public bool Equals(in Mac other) => Equals(in this, in other);

        public override bool Equals(object obj) => obj is Mac other && Equals(in this, in other);

        public override int GetHashCode() => HashCode.Combine((int)k0, (int)k1, (int)k2, (int)k3);

        public override string ToString() => $"{k0:X8}-{k1:X8}-{k2:X8}-{k3:X8}";

        public static bool operator ==(in Mac a, in Mac b) => Equals(in a, in b);
        public static bool operator !=(in Mac a, in Mac b) => !Equals(in a, in b);

        /// <summary>
        /// Constant time equality
        /// </summary>
        private static bool Equals(in Mac a, in Mac b)
            => unchecked((a.k0 ^ b.k0) | (a.k1 ^ b.k1) | (a.k2 ^ b.k2) | (a.k3 ^ b.k3)) == 0;
    }
}
