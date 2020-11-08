using System;
using System.Runtime.CompilerServices;

namespace Carambolas
{
    public static class HashCode
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Combine(int h1, int h2) => unchecked((int)((uint)(h1 << 5) | (uint)(h1 >> 27)) + (h1 ^ h2));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Combine(int h1, int h2, int h3) => Combine(h1, Combine(h2, h3));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Combine(int h1, int h2, int h3, int h4) => Combine(h1, Combine(h2, Combine(h3, h4)));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Combine(int h1, int h2, int h3, int h4, int h5) => Combine(h1, Combine(h2, Combine(h3, Combine(h4, h5))));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Combine(int h1, int h2, int h3, int h4, int h5, int h6) => Combine(h1, Combine(h2, Combine(h3, Combine(h4, Combine(h5, h6)))));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Combine(int h1, int h2, int h3, int h4, int h5, int h6, int h7) => Combine(h1, Combine(h2, Combine(h3, Combine(h4, Combine(h5, Combine(h6, h7))))));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Combine(int h1, int h2, int h3, int h4, int h5, int h6, int h7, int h8) => Combine(h1, Combine(h2, Combine(h3, Combine(h4, Combine(h5, Combine(h6, Combine(h7, h8)))))));
    }
}
