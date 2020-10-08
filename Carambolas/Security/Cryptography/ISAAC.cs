using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Carambolas.Security.Cryptography
{
    /// <summary>
    /// A thread-safe fast cryptographic random number generator designed by Robert J. Jenkins Jr.
    /// More info at https://burtleburtle.net/bob/rand/isaac.html.
    /// </summary>
    public sealed class ISAAC: IRandomNumberGenerator
    {
        public const int Size = 1 << SizeLog2;
        private const int SizeLog2 = 8;
        
        /// <summary>
        /// For pseudorandom lookup */
        /// </summary>
        private const int Mask = (Size - 1) << 2;

        private readonly int[] result = new int[Size];
        private readonly int[] state = new int[Size];

        /// <summary>
        /// Next random integer in the result vector to return.
        /// </summary>
        private int next;

        private int accumulator;

        /// <summary>
        /// Last random integer generated.
        /// </summary>
        private int last;

        /// <summary>
        /// Cycles counter, guarantees cycle is at least 2^^40
        /// </summary>
        private int cycles;

        public ISAAC() => Initialize(false);

        public ISAAC(int[] seed)
        {
            Array.Copy(seed, result, Math.Min(result.Length, seed.Length));
            Initialize(true);
        }

        public ISAAC(int seed)
        {
            result[0] = seed;
            Initialize(true);
        }

        public ISAAC(long seed)
        {
            result[0] = (int)(seed >> 32);
            result[1] = (int)seed;
            Initialize(true);
        }

        private void Initialize(bool seeded)
        {
            int a, b, c, d, e, f, g, h;
            a = b = c = d = e = f = g = h = unchecked((int)0x9e3779b9);

            for (int i = 0; i < 4; ++i)
            {
                a ^= b << 11;
                d += a;
                b += c;
                b ^= (int)((uint)c >> 2);
                e += b;
                c += d;
                c ^= d << 8;
                f += c;
                d += e;
                d ^= (int)((uint)e >> 16);
                g += d;
                e += f;
                e ^= f << 10;
                h += e;
                f += g;
                f ^= (int)((uint)g >> 4);
                a += f;
                g += h;
                g ^= h << 8;
                b += g;
                h += a;
                h ^= (int)((uint)a >> 9);
                c += h;
                a += b;
            }

            for (int i = 0; i < Size; i += 8)
            {
                if (seeded)
                {
                    a += result[i];
                    b += result[i + 1];
                    c += result[i + 2];
                    d += result[i + 3];
                    e += result[i + 4];
                    f += result[i + 5];
                    g += result[i + 6];
                    h += result[i + 7];
                }

                a ^= b << 11;
                d += a;
                b += c;
                b ^= (int)((uint)c >> 2);
                e += b;
                c += d;
                c ^= d << 8;
                f += c;
                d += e;
                d ^= (int)((uint)e >> 16);
                g += d;
                e += f;
                e ^= f << 10;
                h += e;
                f += g;
                f ^= (int)((uint)g >> 4);
                a += f;
                g += h;
                g ^= h << 8;
                b += g;
                h += a;
                h ^= (int)((uint)a >> 9);
                c += h;
                a += b;

                state[i] = a;
                state[i + 1] = b;
                state[i + 2] = c;
                state[i + 3] = d;
                state[i + 4] = e;
                state[i + 5] = f;
                state[i + 6] = g;
                state[i + 7] = h;
            }

            if (seeded)
            {
                for (var i = 0; i < Size; i += 8)
                {
                    a += state[i];
                    b += state[i + 1];
                    c += state[i + 2];
                    d += state[i + 3];
                    e += state[i + 4];
                    f += state[i + 5];
                    g += state[i + 6];
                    h += state[i + 7];
                    a ^= b << 11;
                    d += a;
                    b += c;
                    b ^= (int)((uint)c >> 2);
                    e += b;
                    c += d;
                    c ^= d << 8;
                    f += c;
                    d += e;
                    d ^= (int)((uint)e >> 16);
                    g += d;
                    e += f;
                    e ^= f << 10;
                    h += e;
                    f += g;
                    f ^= (int)((uint)g >> 4);
                    a += f;
                    g += h;
                    g ^= h << 8;
                    b += g;
                    h += a;
                    h ^= (int)((uint)a >> 9);
                    c += h;
                    a += b;
                    state[i] = a;
                    state[i + 1] = b;
                    state[i + 2] = c;
                    state[i + 3] = d;
                    state[i + 4] = e;
                    state[i + 5] = f;
                    state[i + 6] = g;
                    state[i + 7] = h;
                }
            }

            Generate();
        }

        /// <summary>
        /// Generates <see cref="Size"/> integers.
        /// </summary>
        private void Generate()
        {
            int i, j, x, y;

            last += ++cycles;
            for (i = 0, j = Size / 2; i < Size / 2;)
            {
                x = state[i];
                accumulator ^= accumulator << 13;
                accumulator += state[j++];
                state[i] = y = state[(x & Mask) >> 2] + accumulator + last;
                result[i++] = last = state[((y >> SizeLog2) & Mask) >> 2] + x;

                x = state[i];
                accumulator ^= (int)((uint)accumulator >> 6);
                accumulator += state[j++];
                state[i] = y = state[(x & Mask) >> 2] + accumulator + last;
                result[i++] = last = state[((y >> SizeLog2) & Mask) >> 2] + x;

                x = state[i];
                accumulator ^= accumulator << 2;
                accumulator += state[j++];
                state[i] = y = state[(x & Mask) >> 2] + accumulator + last;
                result[i++] = last = state[((y >> SizeLog2) & Mask) >> 2] + x;

                x = state[i];
                accumulator ^= (int)((uint)accumulator >> 16);
                accumulator += state[j++];
                state[i] = y = state[(x & Mask) >> 2] + accumulator + last;
                result[i++] = last = state[((y >> SizeLog2) & Mask) >> 2] + x;
            }

            for (j = 0; j < Size / 2;)
            {
                x = state[i];
                accumulator ^= accumulator << 13;
                accumulator += state[j++];
                state[i] = y = state[(x & Mask) >> 2] + accumulator + last;
                result[i++] = last = state[((y >> SizeLog2) & Mask) >> 2] + x;

                x = state[i];
                accumulator ^= (int)((uint)accumulator >> 6);
                accumulator += state[j++];
                state[i] = y = state[(x & Mask) >> 2] + accumulator + last;
                result[i++] = last = state[((y >> SizeLog2) & Mask) >> 2] + x;

                x = state[i];
                accumulator ^= accumulator << 2;
                accumulator += state[j++];
                state[i] = y = state[(x & Mask) >> 2] + accumulator + last;
                result[i++] = last = state[((y >> SizeLog2) & Mask) >> 2] + x;

                x = state[i];
                accumulator ^= (int)((uint)accumulator >> 16);
                accumulator += state[j++];
                state[i] = y = state[(x & Mask) >> 2] + accumulator + last;
                result[i++] = last = state[((y >> SizeLog2) & Mask) >> 2] + x;
            }

            next = 0;
        }

        private SpinLock resultLock;

        public int GetValue()
        {
            var locked = false;
            try
            {
                resultLock.Enter(ref locked);

                if (next == Size)
                    Generate();

                return result[next++];
            }
            finally
            {
                if (locked)
                    resultLock.Exit(false);
            }
        }

        public Key GetKey()
        {
            var locked = false;
            try
            {
                resultLock.Enter(ref locked);
                Span<uint> buffer = stackalloc uint[8];

                var n = Size - next;
                if (n < 8)
                {
                    for (int i = 0; i < n; ++i)
                        buffer[i] = (uint)result[next++];

                    Generate();
                    for (int i = n; i < 8; ++i)
                        buffer[i] = (uint)result[next++];
                }
                else
                {
                    for (int i = 0; i < 8; ++i)
                        buffer[i] = (uint)result[next++];
                }

                return new Key(buffer[0], buffer[1], buffer[2], buffer[3], buffer[4], buffer[5], buffer[6], buffer[7]);
            }
            finally
            {
                if (locked)
                    resultLock.Exit(false);
            }
        }
    }
}

