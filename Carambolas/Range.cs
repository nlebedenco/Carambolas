using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Carambolas
{
    [StructLayout(LayoutKind.Auto)]
    public struct Range<T>: IEquatable<Range<T>> where T : struct, IEquatable<T>, IComparable<T>
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private T minValue;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private T maxValue;

        public T MinValue
        {
            get => minValue;
            set
            {
                minValue = value;
                if (maxValue.CompareTo(value) < 0)
                    maxValue = value;
            }
        }

        public T MaxValue
        {
            get => maxValue;
            set
            {
                maxValue = value;
                if (minValue.CompareTo(value) > 0)
                    minValue = value;
            }
        }

        public T Clamp(T value)
        {
            if (minValue.CompareTo(value) > 0)
                return minValue;
            if (maxValue.CompareTo(value) < 0)
                return maxValue;
            return value;
        }

        public bool Contains(T value)
        {
            if (minValue.CompareTo(value) > 0)
                return false;
            if (maxValue.CompareTo(value) < 0)
                return false;
            return true;
        }

        private static bool Equals(in Range<T> a, in Range<T> b) => a.minValue.CompareTo(b.minValue) == 0 && a.maxValue.CompareTo(b.maxValue) == 0;

        public bool Equals(Range<T> other) => Equals(in this, in other);

        public override bool Equals(object obj) => obj is Range<T> other && Equals(in this, in other);

        public override int GetHashCode()
        {
            var h1 = minValue.GetHashCode();
            var h2 = maxValue.GetHashCode();
            return (int)((uint)(h1 << 5) | (uint)h1 >> 27) + h1 ^ h2;
        }

        public override string ToString() => $"[{minValue}, {maxValue}]";

        public Range(in T min, in T max)
        {
            minValue = (min.CompareTo(max) < 0) ? min : max;
            maxValue = max;
        }

        public static bool operator ==(in Range<T> a, in Range<T> b) => Equals(in a, in b);
        public static bool operator !=(in Range<T> a, in Range<T> b) => !Equals(in a, in b);
    }
}
