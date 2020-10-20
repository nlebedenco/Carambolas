using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Carambolas
{
    [StructLayout(LayoutKind.Auto)]
    public struct Range<T>: IEquatable<Range<T>> where T : struct, IEquatable<T>, IComparable<T>
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private T min;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private T max;

        public T Min
        {
            get => min;
            set
            {
                min = value;
                if (max.CompareTo(value) < 0)
                    max = value;
            }
        }

        public T Max
        {
            get => max;
            set
            {
                max = value;
                if (min.CompareTo(value) > 0)
                    min = value;
            }
        }

        public T Clamp(T value)
        {
            if (min.CompareTo(value) > 0)
                return min;
            if (max.CompareTo(value) < 0)
                return max;
            return value;
        }

        public bool Contains(T value)
        {
            if (min.CompareTo(value) > 0)
                return false;
            if (max.CompareTo(value) < 0)
                return false;
            return true;
        }

        private static bool Equals(in Range<T> a, in Range<T> b) => a.min.CompareTo(b.min) == 0 && a.max.CompareTo(b.max) == 0;

        public bool Equals(Range<T> other) => Equals(in this, in other);

        public override bool Equals(object obj) => obj is Range<T> other && Equals(in this, in other);

        public override int GetHashCode()
        {
            var h1 = min.GetHashCode();
            var h2 = max.GetHashCode();
            return (int)((uint)(h1 << 5) | (uint)h1 >> 27) + h1 ^ h2;
        }

        public override string ToString() => $"[{min}, {max}]";

        public Range(in T min, in T max)
        {
            this.min = (min.CompareTo(max) < 0) ? min : max;
            this.max = max;
        }

        public static bool operator ==(in Range<T> a, in Range<T> b) => Equals(in a, in b);
        public static bool operator !=(in Range<T> a, in Range<T> b) => !Equals(in a, in b);
    }
}
