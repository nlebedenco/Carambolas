using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Carambolas
{
    public static class Converter
    {
        [StructLayout(LayoutKind.Explicit)]
        public struct Single
        {
            [FieldOffset(0)]
            public float AsFloat;

            [FieldOffset(0)]
            public int AsInt32;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct Double
        {
            [FieldOffset(0)]
            public double AsDouble;

            [FieldOffset(0)]
            public long AsInt64;
        }

        /// <summary>
        /// Relies on <see cref="System.Guid"/> having an internal representation of 11 members: 1 int, 2 shorts and 8 bytes.
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        public struct Guid
        {
            [FieldOffset(0)]
            public System.Guid AsGuid;

            [FieldOffset(0)]
            private uint a;

            [FieldOffset(4)]
            private ushort b;

            [FieldOffset(6)]
            private ushort c;

            [FieldOffset(8)]
            private ulong d;

            public (uint A, ushort B, ushort C, ulong D)  AsTuple
            {
                get => BitConverter.IsLittleEndian ? (a, b, c, ReverseBytes(d)) : (a, b, c, d);

                set => (a, b, c, d) = BitConverter.IsLittleEndian ? (value.A, value.B, value.C, ReverseBytes(value.D)) : value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReverseBytes(ushort value) => (ushort)((value & 0xFFU) << 8 | (value & 0xFF00U) >> 8);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReverseBytes(uint value) 
            => (value & 0x000000FFU) << 24 | (value & 0x0000FF00U) << 8 | (value & 0x00FF0000U) >> 8 | (value & 0xFF000000U) >> 24;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ReverseBytes(ulong value) 
            => (value & 0x00000000000000FFUL) << 56 | (value & 0x000000000000FF00UL) << 40 |
               (value & 0x0000000000FF0000UL) << 24 | (value & 0x00000000FF000000UL) << 8 |
               (value & 0x000000FF00000000UL) >> 8 | (value & 0x0000FF0000000000UL) >> 24 |
               (value & 0x00FF000000000000UL) >> 40 | (value & 0xFF00000000000000UL) >> 56;
    }
}
