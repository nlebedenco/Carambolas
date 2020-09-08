using System;
using System.Runtime.CompilerServices;

// Based on https://github.com/daviddesmet/NaCl.Core

namespace Carambolas.Security.Cryptography.NaCl
{  
    internal static class BitUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint RotateLeft(uint value, int offset) => (value << offset) | (value >> (32 - offset));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong RotateLeft(ulong value, int offset) => (value << offset) | (value >> (64 - offset));
    }
}
