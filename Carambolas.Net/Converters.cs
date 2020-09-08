using System;
using System.Runtime.InteropServices;

namespace Carambolas.Net
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

    [StructLayout(LayoutKind.Explicit)]
    public struct GuidConverter
    {
        [FieldOffset(0)]
        public Guid Guid;

        [FieldOffset(0)]
        public ulong MSB;

        [FieldOffset(8)]
        public ulong LSB;
    }   
}
