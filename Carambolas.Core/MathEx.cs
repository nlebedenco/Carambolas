using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Carambolas
{
    public static class MathEx
    {
        /// <summary>
        /// A significantly faster alternative to <see cref="Math.DivRem(int, int, out int)"/>.
        /// (see https://github.com/dotnet/runtime/issues/5213)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint DivRem(uint a, uint b, out uint result)
        {
            var div = a / b;
            result = a - (div * b);
            return div;
        }

        /// <summary>
        /// A significantly faster alternative to <see cref="Math.DivRem(long, long, out long)"/>.
        /// (see https://github.com/dotnet/runtime/issues/5213)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong DivRem(ulong a, ulong b, out ulong result)
        {
            var div = a / b;
            result = a - (div * b);
            return div;
        }

        /// <summary>
        /// A significantly faster alternative to <see cref="Math.DivRem(int, int, out int)"/>.
        /// (see https://github.com/dotnet/runtime/issues/5213)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DivRem(int a, int b, out int result)
        {
            var div = a / b;
            result = a - (div * b);
            return div;
        }

        /// <summary>
        /// A significantly faster alternative to <see cref="Math.DivRem(long, long, out long)"/>.
        /// (see https://github.com/dotnet/runtime/issues/5213)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long DivRem(long a, long b, out long result)
        {
            var div = a / b;
            result = a - (div * b);
            return div;
        }

        /// <summary>
        /// Returns the integer (floor) log of the specified value, base 2.
        /// Note that by convention, input value 0 returns 0 since Log(0) is undefined.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Log2(ulong value)
        {
            uint hi = (uint)(value >> 32);

            if (hi == 0)
            {
                return Log2((uint)value);
            }

            return 32 + Log2(hi);
        }

        private static readonly byte[] Log2DeBruijn = new byte[32]
        {
            00, 09, 01, 10, 13, 21, 02, 29,
            11, 14, 16, 18, 22, 25, 03, 30,
            08, 12, 20, 28, 15, 17, 24, 07,
            19, 27, 23, 06, 26, 05, 04, 31
        };

        /// <summary>
        /// Returns the integer (floor) log of the specified value, base 2.
        /// Note that by convention, input value 0 returns 0 since Log(0) is undefined.
        /// Does not directly use any hardware intrinsics, nor does it incur branching.
        /// </summary>
        /// <param name="value">The value.</param>
        private static int Log2(uint value)
        {
            // No AggressiveInlining due to large method size
            // Has conventional contract 0->0 (Log(0) is undefined)

            // Fill trailing zeros with ones, eg 00010010 becomes 00011111
            value |= value >> 01;
            value |= value >> 02;
            value |= value >> 04;
            value |= value >> 08;
            value |= value >> 16;

            // uint.MaxValue >> 27 is always in range [0 - 31]
            return Log2DeBruijn[(value * 0x07C4ACDD) >> 27];
        }

        /// <summary>
        /// Count the number of leading zero bits in a mask.
        /// Similar in behavior to the x86 instruction LZCNT.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LeadingZeroCount(uint value) => value == 0 ? 32 : (31 - Log2(value));

        /// <summary>
        /// Count the number of leading zero bits in a mask.
        /// Similar in behavior to the x86 instruction LZCNT.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LeadingZeroCount(ulong value)
        {
            var hi = (uint)(value >> 32);
            return hi == 0 ? (32 + LeadingZeroCount((uint)value)) : LeadingZeroCount(hi);
        }
    }
}
