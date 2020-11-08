using System;
using System.Runtime.CompilerServices;

namespace Carambolas
{
    public static class ByteArrayExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToHex(this byte[] array) => BitConverter.ToString(array);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToHex(this byte[] array, int startIndex, int length) => BitConverter.ToString(array, startIndex, length);
    }
}
