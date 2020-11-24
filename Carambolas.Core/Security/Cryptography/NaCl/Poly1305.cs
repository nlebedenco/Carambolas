using System;
using System.Runtime.CompilerServices;

using Carambolas.Internal;

namespace Carambolas.Security.Cryptography.NaCl
{
    /// <summary>
    /// Poly1305 one-time MAC based on RFC 7539.
    /// Based on an implementation from https://github.com/daviddesmet/NaCl.Core
    /// released under the MIT license which in turn was based on a poly1305 implementation by 
    /// Andrew Moon (https://github.com/floodyberry/poly1305-donna) released as public domain.
    /// </summary>
    public static class Poly1305
    {
        private ref struct Accumulator
        {
            private uint u0;
            private uint u1;
            private uint u2;
            private uint u3;
            private uint u4;

            private uint b;

            private uint t0;
            private uint t1;
            private uint t2;
            private uint t3;

            private readonly uint t4;
            private readonly uint t5;
            private readonly uint t6;
            private readonly uint t7;

            private readonly uint r0;
            private readonly uint r1;
            private readonly uint r2;
            private readonly uint r3;
            private readonly uint r4;

            private readonly uint s1;
            private readonly uint s2;
            private readonly uint s3;
            private readonly uint s4;

            public Accumulator(in Key key)
            {
                u0 = 0;
                u1 = 0;
                u2 = 0;
                u3 = 0;
                u4 = 0;

                b = 0;

                (t0, t1, t2, t3, t4, t5, t6, t7) = key;

                // Precompute multipliers
                r0 = t0 & 0x3ffffff;
                t0 >>= 26;
                t0 |= t1 << 6;
                r1 = t0 & 0x3ffff03;
                t1 >>= 20;
                t1 |= t2 << 12;
                r2 = t1 & 0x3ffc0ff;
                t2 >>= 14;
                t2 |= t3 << 18;
                r3 = t2 & 0x3f03fff;
                t3 >>= 8;
                r4 = t3 & 0x00fffff;

                s1 = r1 * 5;
                s2 = r2 * 5;
                s3 = r3 * 5;
                s4 = r4 * 5;
            }

            public void Push(byte[] buffer, int offset, int length, byte terminator = 1)
            {
                terminator &= 1;

                if (length >= Mac.Size)
                {
                    t0 = LoadUInt32LittleEndian(buffer, offset + 0);
                    t1 = LoadUInt32LittleEndian(buffer, offset + 4);
                    t2 = LoadUInt32LittleEndian(buffer, offset + 8);
                    t3 = LoadUInt32LittleEndian(buffer, offset + 12);

                    u0 += t0 & 0x3ffffff;
                    u1 += (uint)(((((ulong)t1 << 32) | t0) >> 26) & 0x3ffffff);
                    u2 += (uint)(((((ulong)t2 << 32) | t1) >> 20) & 0x3ffffff);
                    u3 += (uint)(((((ulong)t3 << 32) | t2) >> 14) & 0x3ffffff);
                    u4 += (t3 >> 8) | (1 << 24);
                }
                else // incomplete last block
                {
                    Span<byte> block = stackalloc byte[Mac.Size];
                    for (int j = 0; j < length; ++j, ++offset)
                        block[j] = buffer[offset];

                    block[length] = terminator;

                    for (int j = length + 1; j < Mac.Size; ++j)
                        block[j] = 0;

                    t0 = LoadUInt32LittleEndian(block, 0);
                    t1 = LoadUInt32LittleEndian(block, 4);
                    t2 = LoadUInt32LittleEndian(block, 8);
                    t3 = LoadUInt32LittleEndian(block, 12);

                    for (int j = 0; j < Mac.Size; ++j)
                        block[j] = 0;

                    u0 += t0 & 0x3ffffff;
                    u1 += (uint)(((((ulong)t1 << 32) | t0) >> 26) & 0x3ffffff);
                    u2 += (uint)(((((ulong)t2 << 32) | t1) >> 20) & 0x3ffffff);
                    u3 += (uint)(((((ulong)t3 << 32) | t2) >> 14) & 0x3ffffff);
                    u4 += (t3 >> 8) | (((uint)terminator ^ 1) << 24);
                }

                // d = r * h
                var tt0 = (ulong)u0 * r0 + (ulong)u1 * s4 + (ulong)u2 * s3 + (ulong)u3 * s2 + (ulong)u4 * s1;
                var tt1 = (ulong)u0 * r1 + (ulong)u1 * r0 + (ulong)u2 * s4 + (ulong)u3 * s3 + (ulong)u4 * s2;
                var tt2 = (ulong)u0 * r2 + (ulong)u1 * r1 + (ulong)u2 * r0 + (ulong)u3 * s4 + (ulong)u4 * s3;
                var tt3 = (ulong)u0 * r3 + (ulong)u1 * r2 + (ulong)u2 * r1 + (ulong)u3 * r0 + (ulong)u4 * s4;
                var tt4 = (ulong)u0 * r4 + (ulong)u1 * r3 + (ulong)u2 * r2 + (ulong)u3 * r1 + (ulong)u4 * r0;

                // Partial reduction mod 2^130-5
                unchecked
                {
                    u0 = (uint)tt0 & 0x3ffffff;
                    var c = (tt0 >> 26);
                    tt1 += c;
                    u1 = (uint)tt1 & 0x3ffffff;
                    b = (uint)(tt1 >> 26);
                    tt2 += b;
                    u2 = (uint)tt2 & 0x3ffffff;
                    b = (uint)(tt2 >> 26);
                    tt3 += b;
                    u3 = (uint)tt3 & 0x3ffffff;
                    b = (uint)(tt3 >> 26);
                    tt4 += b;
                    u4 = (uint)tt4 & 0x3ffffff;
                    b = (uint)(tt4 >> 26);
                }

                u0 += b * 5;
            }

            public Mac Calculate()
            {
                // Do final reduction mod 2^130-5
                var b = u0 >> 26;
                var h0 = u0 & 0x3ffffff;
                var h1 = u1 + b;
                b = h1 >> 26;
                h1 &= 0x3ffffff;
                var h2 = u2 + b;
                b = h2 >> 26;
                h2 &= 0x3ffffff;
                var h3 = u3 + b;
                b = h3 >> 26;
                h3 &= 0x3ffffff;
                var h4 = u4 + b;
                b = h4 >> 26;
                h4 &= 0x3ffffff;
                h0 += b * 5;

                // Compute h - p
                var g0 = h0 + 5;
                b = g0 >> 26;
                g0 &= 0x3ffffff;
                var g1 = h1 + b;
                b = g1 >> 26;
                g1 &= 0x3ffffff;
                var g2 = h2 + b;
                b = g2 >> 26;
                g2 &= 0x3ffffff;
                var g3 = h3 + b;
                b = g3 >> 26;
                g3 &= 0x3ffffff;
                var g4 = unchecked(h4 + b - (1 << 26));

                // Select h if h < p, or h - p if h >= p
                b = (g4 >> 31) - 1; // mask is either 0 (h >= p) or -1 (h < p)
                var nb = ~b;
                h0 = (h0 & nb) | (g0 & b);
                h1 = (h1 & nb) | (g1 & b);
                h2 = (h2 & nb) | (g2 & b);
                h3 = (h3 & nb) | (g3 & b);
                h4 = (h4 & nb) | (g4 & b);

                // h = h % (2^128)
                var f0 = ((h0) | (h1 << 26)) + (ulong)t4;
                var f1 = ((h1 >> 6) | (h2 << 20)) + (ulong)t5;
                var f2 = ((h2 >> 12) | (h3 << 14)) + (ulong)t6;
                var f3 = ((h3 >> 18) | (h4 << 8)) + (ulong)t7;

                // mac = (h + pad) % (2^128)            
                f1 += (f0 >> 32);
                f2 += (f1 >> 32);
                f3 += (f2 >> 32);

                return new Mac((uint)f0, (uint)f1, (uint)f2, (uint)f3);
            }
        }

        public static void Sign(byte[] buffer, int offset, int length, in Key key, out Mac mac)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (offset > buffer.Length - length)
                throw new ArgumentException(string.Format(Resources.GetString(Strings.IndexOutOfRangeOrLengthIsGreaterThanNumberOfElements), nameof(offset), nameof(length), nameof(buffer)), nameof(buffer));

            var accumulator = new Accumulator(in key);
            for (var i = 0; i < length; i += Mac.Size)
                accumulator.Push(buffer, offset + i, length - i);

            mac = accumulator.Calculate();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Verify(byte[] buffer, int offset, int length, in Key key, in Mac mac)
        {
            Sign(buffer, offset, length, in key, out Mac calculated);
            return calculated == mac;
        }

        /// <summary>
        /// Loads 4 bytes of the input buffer into an unsigned 32-bit integer, beginning at the input offset.
        /// </summary>
        /// <param name="buf">The input buffer.</param>
        /// <param name="offset">The input offset.</param>
        /// <returns>System.UInt32.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint LoadUInt32LittleEndian(byte[] buf, int offset) => ((uint)buf[offset]) | ((uint)buf[offset + 1] << 8) | ((uint)buf[offset + 2] << 16) | ((uint)buf[offset + 3] << 24);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint LoadUInt32LittleEndian(in Span<byte> buf, int offset) => ((uint)buf[offset]) | ((uint)buf[offset + 1] << 8) | ((uint)buf[offset + 2] << 16) | ((uint)buf[offset + 3] << 24);

        public static class AEAD
        {
            private static void Accumulate(in ArraySegment<byte> data, ref Accumulator accumulator)
            {
                var (buffer, offset, length) = (data.Array, data.Offset, data.Count);
                for (var i = 0; i < length; i += Mac.Size)
                    accumulator.Push(buffer, offset + i, length - i, 0);
            }

            public static void Sign(in ArraySegment<byte> authdata, in ArraySegment<byte> ciphertext, in Key key, out Mac mac)
            {
                var accumulator = new Accumulator(in key);
                Accumulate(authdata, ref accumulator);
                Accumulate(ciphertext, ref accumulator);

                mac = accumulator.Calculate();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool Verify(in ArraySegment<byte> authdata, in ArraySegment<byte> ciphertext, in Key key, in Mac mac)
            {
                Sign(in authdata, in ciphertext, in key, out Mac calculated);
                return calculated == mac;
            }
        }
    }
}
