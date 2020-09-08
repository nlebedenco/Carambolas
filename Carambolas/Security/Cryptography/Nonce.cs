using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Carambolas.Security.Cryptography
{
    public readonly struct Nonce: IEquatable<Nonce>
    {
        public const int Size = 12;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly uint k0;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly uint k1;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly uint k2;

        public Nonce(ulong b) : this(0, b) { }

        public Nonce(uint a, ulong b) : this(a, (uint)(b >> 32), (uint)b) { }

        public Nonce(uint a, uint b, uint c)
        {
            k0 = a;
            k1 = b;
            k2 = c;
        }

        public Nonce(byte[] array, int index = 0)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            if (index < 0)
                throw new ArgumentNullException(nameof(index));

            if (index > (array.Length - Size))
                throw new ArgumentException(string.Format(SR.IndexOutOfRangeOrNumberOfElementsIsLessThanMinimum, nameof(index), Size, nameof(array)), nameof(array));

            k0 = ((uint)array[index + 0]) | ((uint)array[index + 1] << 8) | ((uint)array[index +  2] << 16) | ((uint)array[index +  3] << 24);
            k1 = ((uint)array[index + 4]) | ((uint)array[index + 5] << 8) | ((uint)array[index +  6] << 16) | ((uint)array[index +  7] << 24);
            k2 = ((uint)array[index + 8]) | ((uint)array[index + 9] << 8) | ((uint)array[index + 10] << 16) | ((uint)array[index + 11] << 24);
        }

        public void CopyTo(byte[] array, int index = 0)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));

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
        }

        public byte[] ToArray()
        {
            var a = new byte[Size];
            CopyTo(a);
            return a;
        }

        public void Deconstruct(out uint t0, out uint t1, out uint t2) => (t0, t1, t2) = (k0, k1, k2);

        public bool Equals(Nonce other) => Equals(this, other);

        public bool Equals(in Nonce other) => Equals(in this, in other);

        public override bool Equals(object obj) => obj is Nonce other && Equals(in this, in other);

        public override int GetHashCode() => HashCode.Combine((int)k0, (int)k1, (int)k2);

        public override string ToString() => $"{k0:X8}-{k1:X8}-{k2:X8}";

        public static bool operator ==(in Nonce a, in Nonce b) => Equals(in a, in b);
        public static bool operator !=(in Nonce a, in Nonce b) => !Equals(in a, in b);

        public static Nonce operator ++(in Nonce a)
        {
            unchecked
            {
                var carry = 1u;

                var k2 = a.k2 + carry;
                carry = ((a.k2 ^ k2) & ~(a.k0 ^ 0x80000000)) >> 31;

                var k1 = a.k1 + carry;
                carry = ((a.k1 ^ k1) & ~(a.k1 ^ 0x80000000)) >> 31;

                var k0 = a.k0 + carry;

                return new Nonce(k0, k1, k2);
            }
        }

        /// <summary>
        /// Constant time equality
        /// </summary>
        private static bool Equals(in Nonce a, in Nonce b)
            => unchecked((a.k0 ^ b.k0) | (a.k1 ^ b.k1) | (a.k2 ^ b.k2)) == 0;
    }
}
