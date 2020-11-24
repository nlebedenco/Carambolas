using System;
using System.Runtime.InteropServices;

namespace Carambolas
{ 
    internal static class DecimalExtensions
    {
        [StructLayout(LayoutKind.Explicit)]
        private struct DecimalBits
        {
            [FieldOffset(0)]
            public decimal AsDecimal;

            [FieldOffset(0)]
            public int Flags;
            [FieldOffset(4)]
            public int Hi;
            [FieldOffset(8)]
            public int Lo;
            [FieldOffset(12)]
            public int Mid;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct DecCalc
        {
            private const uint TenToPowerNine = 1000000000;

            [FieldOffset(0)]
            public decimal AsDecimal;

            // NOTE: Do not change the offsets of these fields. This structure must have the same layout as Decimal.
            [FieldOffset(0)]
            public uint UFlags;
            [FieldOffset(4)]
            public uint UHi;
            [FieldOffset(8)]
            public uint ULo;
            [FieldOffset(12)]
            public uint UMid;

            /// <summary>
            /// The low and mid fields combined in little-endian order
            /// </summary>
            [FieldOffset(8)]
            private ulong ulomidLE;

            internal static uint DecDivMod1E9(ref DecCalc value)
            {
                ulong high64 = ((ulong)value.UHi << 32) + value.UMid;
                ulong div64 = high64 / TenToPowerNine;
                value.UHi = (uint)(div64 >> 32);
                value.UMid = (uint)div64;

                ulong num = ((high64 - (uint)div64 * TenToPowerNine) << 32) + value.ULo;
                uint div = (uint)(num / TenToPowerNine);
                value.ULo = div;
                return (uint)num - div * TenToPowerNine;
            }
        }

        private const int ScaleShift = 16;

        internal static uint High(this ref decimal value) => new DecCalc { AsDecimal = value }.UHi;

        internal static uint Low(this ref decimal value) => new DecCalc { AsDecimal = value }.ULo;

        internal static uint Mid(this ref decimal value) => new DecCalc { AsDecimal = value }.UMid;

        internal static bool IsNegative(this ref decimal value) => new DecimalBits { AsDecimal = value }.Flags < 0;

        internal static int Scale(this ref decimal value) => (byte)(new DecimalBits { AsDecimal = value }.Flags >> ScaleShift);

        internal static uint DecDivMod1E9(this ref decimal value)
        {
            var union = new DecCalc { AsDecimal = value };
            var r = DecCalc.DecDivMod1E9(ref union);
            value = union.AsDecimal;
            return r;
        }
    }
}
