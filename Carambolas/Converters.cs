using System;
using System.Runtime.InteropServices;

namespace Carambolas
{
    [StructLayout(LayoutKind.Explicit)]
    internal struct FloatConverter
    {
        [FieldOffset(0)]
        public float AsFloat;

        [FieldOffset(0)]
        public int AsInt32;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct DoubleConverter
    {
        [FieldOffset(0)]
        public double AsDouble;

        [FieldOffset(0)]
        public long AsInt64;
    }

    /// <summary>
    /// Relies on <see cref="System.Guid"/> having an internal representation of 11 fields: 1 int, 2 shorts and 8 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct GuidConverter
    {
        [FieldOffset(0)]
        public System.Guid Guid;

        [FieldOffset(0)]
        public ulong MSB;

        [FieldOffset(8)]
        public ulong LSB;
    }   
}
