using System;
using System.Diagnostics;

// Based on https://github.com/force-net/Crc32.NET

namespace Carambolas.Security.Cryptography
{
    /// <summary>
    /// Represents a CRC32-C (Castagnoli) value.
    /// </summary>
    public readonly struct Crc32C: IEquatable<Crc32C>
    {
        public const int Size = sizeof(uint);

        private const uint POLYNOMIAL = 0x82F63B78u;

        private static readonly uint[] TABLE;

        static Crc32C()
        {
            TABLE = new uint[16 * 256];
            for (int i = 0; i < 256; ++i)
            {
                var res = (uint)i;
                for (int t = 0; t < 16; t++)
                {
                    for (int k = 0; k < 8; k++)
                        res = (res & 1) == 1 ? POLYNOMIAL ^ (res >> 1) : (res >> 1);

                    TABLE[(t * 256) + i] = res;
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly uint value;

        public Crc32C(uint a) => value = a;

        public Crc32C(byte[] array) : this(array, 0) { }

        public Crc32C(byte[] array, int index)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            if (index > (array.Length - Size))
                throw new ArgumentException(string.Format(Resources.GetString(Strings.IndexOutOfRangeOrLengthIsGreaterThanNumberOfElements), nameof(index), $"{nameof(Crc32C)}.{nameof(Size)} ({Size})", nameof(array)), nameof(array));

            value = ((uint)array[index + 3] << 24) | ((uint)array[index + 2] << 16) | ((uint)array[index + 1] << 8) | ((uint)array[index]);
        }

        private Crc32C(byte[] buffer, int offset, int length)
        {
            var crc = uint.MaxValue ^ 0;
            var table = TABLE;
            while (length >= 16)
            {
                var a = table[(3 * 256) + buffer[offset + 12]]
                    ^ table[(2 * 256) + buffer[offset + 13]]
                    ^ table[(1 * 256) + buffer[offset + 14]]
                    ^ table[(0 * 256) + buffer[offset + 15]];

                var b = table[(7 * 256) + buffer[offset + 8]]
                    ^ table[(6 * 256) + buffer[offset + 9]]
                    ^ table[(5 * 256) + buffer[offset + 10]]
                    ^ table[(4 * 256) + buffer[offset + 11]];

                var c = table[(11 * 256) + buffer[offset + 4]]
                    ^ table[(10 * 256) + buffer[offset + 5]]
                    ^ table[(9 * 256) + buffer[offset + 6]]
                    ^ table[(8 * 256) + buffer[offset + 7]];

                var d = table[(15 * 256) + ((byte)crc ^ buffer[offset])]
                    ^ table[(14 * 256) + ((byte)(crc >> 8) ^ buffer[offset + 1])]
                    ^ table[(13 * 256) + ((byte)(crc >> 16) ^ buffer[offset + 2])]
                    ^ table[(12 * 256) + ((crc >> 24) ^ buffer[offset + 3])];

                crc = d ^ c ^ b ^ a;
                offset += 16;
                length -= 16;
            }

            while (--length >= 0)
                crc = table[(byte)(crc ^ buffer[offset++])] ^ crc >> 8;

            value = crc ^ uint.MaxValue;
        }

        public void CopyTo(byte[] array, int index = 0)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            if (index < 0)
                throw new ArgumentNullException(nameof(index));

            if (index > (array.Length - Size))
                throw new ArgumentException(string.Format(Resources.GetString(Strings.IndexOutOfRangeOrLengthIsGreaterThanNumberOfElements), nameof(index), $"{nameof(Crc32C)}.{nameof(Size)} ({Size})", nameof(array)), nameof(array));

            array[index + 0] = (byte)(value);
            array[index + 1] = (byte)(value >> 8);
            array[index + 2] = (byte)(value >> 16);
            array[index + 3] = (byte)(value >> 24);
        }

        public byte[] ToArray()
        {
            var a = new byte[Size];
            CopyTo(a);
            return a;
        }

        public bool Equals(Crc32C other) => Equals(this, other);

        public bool Equals(in Crc32C other) => Equals(in this, in other);

        public override bool Equals(object obj) => obj is Crc32C other && Equals(in this, in other);

        public override int GetHashCode() => (int)value;

        public override string ToString() => $"{value:X8}";

        public static explicit operator uint(Crc32C a) => a.value;
        public static implicit operator Crc32C(uint value) => new Crc32C(value);

        public static bool operator ==(in Crc32C a, in Crc32C b) => Equals(in a, in b);
        public static bool operator !=(in Crc32C a, in Crc32C b) => !Equals(in a, in b);

        private static bool Equals(in Crc32C a, in Crc32C b) => a.value == b.value;
               
        public static Crc32C Compute(byte[] buffer, int offset, int length)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (offset > (buffer.Length - length))
                throw new ArgumentException(string.Format(Resources.GetString(Strings.IndexOutOfRangeOrLengthIsGreaterThanNumberOfElements), nameof(offset), nameof(length), nameof(buffer)), nameof(buffer));

            return new Crc32C(buffer, offset, length);
        }

        public static void ComputeAndAppend(byte[] buffer, int offset, int length)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (offset > (buffer.Length - length - Size))
                throw new ArgumentException(string.Format(Resources.GetString(Strings.IndexOutOfRangeOrLengthIsGreaterThanNumberOfElements), nameof(offset), $"{nameof(length)} + {nameof(Crc32C)}.{nameof(Size)} ({Size})", nameof(buffer)), nameof(buffer));

            var crc = new Crc32C(buffer, offset, length);
            crc.CopyTo(buffer, length);            
        }

        /// <summary>
        /// Verify a buffer with the crc appended as produced by <see cref="ComputeAndAppend(byte[], int, int)"/>
        /// </summary>
        public static bool Verify(byte[] buffer, int offset, int length) => Compute(buffer, offset, length) == 0x48674BC7;        
    }
}