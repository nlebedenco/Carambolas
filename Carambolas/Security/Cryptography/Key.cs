using System;
using System.Diagnostics;

namespace Carambolas.Security.Cryptography
{
    public readonly struct Key: IEquatable<Key>
    {
        public const int Size = 32;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly uint k0;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly uint k1;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly uint k2;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly uint k3;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly uint k4;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly uint k5;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly uint k6;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly uint k7;

        public Key(uint a, uint b, uint c, uint d, uint e, uint f, uint g, uint h)
        {
            k0 = a;
            k1 = b;
            k2 = c;
            k3 = d;
            k4 = e;
            k5 = f;
            k6 = g;
            k7 = h;
        }

        public Key(byte[] array, int index = 0)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            if (index > (array.Length - Size))
                throw new ArgumentException(string.Format(SR.IndexOutOfRangeOrNumberOfElementsIsLessThanMinimum, nameof(index), Size, nameof(array)), nameof(array));

            k0 = ((uint)array[index +  0]) | ((uint)array[index  + 1] << 8) | ((uint)array[index +  2] << 16) | ((uint)array[index +  3] << 24);
            k1 = ((uint)array[index +  4]) | ((uint)array[index  + 5] << 8) | ((uint)array[index +  6] << 16) | ((uint)array[index +  7] << 24);
            k2 = ((uint)array[index +  8]) | ((uint)array[index  + 9] << 8) | ((uint)array[index + 10] << 16) | ((uint)array[index + 11] << 24);
            k3 = ((uint)array[index + 12]) | ((uint)array[index + 13] << 8) | ((uint)array[index + 14] << 16) | ((uint)array[index + 15] << 24);
            k4 = ((uint)array[index + 16]) | ((uint)array[index + 17] << 8) | ((uint)array[index + 18] << 16) | ((uint)array[index + 19] << 24);
            k5 = ((uint)array[index + 20]) | ((uint)array[index + 21] << 8) | ((uint)array[index + 22] << 16) | ((uint)array[index + 23] << 24);
            k6 = ((uint)array[index + 24]) | ((uint)array[index + 25] << 8) | ((uint)array[index + 26] << 16) | ((uint)array[index + 27] << 24);
            k7 = ((uint)array[index + 28]) | ((uint)array[index + 29] << 8) | ((uint)array[index + 30] << 16) | ((uint)array[index + 31] << 24);
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
            array[index + 16] = (byte)(k4 >> 0);
            array[index + 17] = (byte)(k4 >> 8);
            array[index + 18] = (byte)(k4 >> 16);
            array[index + 19] = (byte)(k4 >> 24);
            array[index + 20] = (byte)(k5 >> 0);
            array[index + 21] = (byte)(k5 >> 8);
            array[index + 22] = (byte)(k5 >> 16);
            array[index + 23] = (byte)(k5 >> 24);
            array[index + 24] = (byte)(k6 >> 0);
            array[index + 25] = (byte)(k6 >> 8);
            array[index + 26] = (byte)(k6 >> 16);
            array[index + 27] = (byte)(k6 >> 24);
            array[index + 28] = (byte)(k7 >> 0);
            array[index + 29] = (byte)(k7 >> 8);
            array[index + 30] = (byte)(k7 >> 16);
            array[index + 31] = (byte)(k7 >> 24);
        }

        public void CopyTo(uint[] array, int index = 0)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            if (index < 0)
                throw new ArgumentNullException(nameof(index));

            if (index > (array.Length - (Size / sizeof(uint))))
                throw new ArgumentException(string.Format(SR.IndexOutOfRangeOrNumberOfElementsIsLessThanMinimum, nameof(index), (Size / sizeof(uint)), nameof(array)), nameof(array));

            array[index + 0] = k0;
            array[index + 1] = k1;
            array[index + 2] = k2;
            array[index + 3] = k3;
            array[index + 4] = k4;
            array[index + 5] = k5;
            array[index + 6] = k6;
            array[index + 7] = k7;
        }

        public byte[] ToArray()
        {
            var a = new byte[Size];
            CopyTo(a);
            return a;
        }

        public void Deconstruct(out uint t0, out uint t1, out uint t2, out uint t3, out uint t4, out uint t5, out uint t6, out uint t7) => (t0, t1, t2, t3, t4, t5, t6, t7) = (k0, k1, k2, k3, k4, k5, k6, k7);

        public bool Equals(Key other) => Equals(in this, in other);

        public bool Equals(in Key other) => Equals(in this, in other);

        public override bool Equals(object obj) => obj is Key other && Equals(in this, in other);

        public override int GetHashCode() => HashCode.Combine((int)k0, (int)k1, (int)k2, (int)k3, (int)k4, (int)k5, (int)k6, (int)k7);

        public override string ToString() => $"{k0:X8}-{k1:X8}-{k2:X8}-{k3:X8}-{k4:X8}-{k5:X8}-{k6:X8}-{k7:X8}";

        public static bool operator ==(in Key a, in Key b) => Equals(in a, in b);
        public static bool operator !=(in Key a, in Key b) => !Equals(in a, in b);

        /// <summary>
        /// Constant time equality
        /// </summary>
        private static bool Equals(in Key a, in Key b)
            => unchecked((a.k0 ^ b.k0) | (a.k1 ^ b.k1) | (a.k2 ^ b.k2) | (a.k3 ^ b.k3) | (a.k4 ^ b.k4) | (a.k5 ^ b.k5) | (a.k6 ^ b.k6) | (a.k7 ^ b.k7)) == 0;
    }
}
