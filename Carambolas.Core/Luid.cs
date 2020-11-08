using System;
using System.Collections.Generic;
using System.Diagnostics;

using Carambolas.Internal;

namespace Carambolas
{
    /// <summary>
    /// A locally unique identifier that can be used to identify entities within a single application instance. Not supposed to be persisted
    /// or used to reference entities across different application instances or domains (i.e. different machines).
    /// For a global unique identifier that can be persisted and shared with multiple systems refer to <see cref="System.Guid"/>.
    /// </summary>
    public readonly struct Luid: IFormattable, IComparable, IComparable<Luid>, IEquatable<Luid>
    {
        public static Luid Zero = default;

        private static ulong seed = 0;

        public static Luid NewLuid() => new Luid(++seed);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly ulong value;

        public bool IsEmpty => this.value == 0;

        public Luid(ulong value) => this.value = value;

        public static implicit operator ulong(Luid a) => a.value;
        public static implicit operator long(Luid a) => (long)a.value;

        public static bool operator ==(Luid x, Luid y) => x.value == y.value;
        public static bool operator !=(Luid x, Luid y) => x.value != y.value;

        public int CompareTo(Luid other) => value.CompareTo(other.value);

        public int CompareTo(object obj) => obj is Luid other ? value.CompareTo(other.value) 
            : throw new ArgumentException(string.Format(Resources.GetString(Strings.ArgumentMustBeOfType), GetType().FullName), nameof(obj));

        public override bool Equals(object obj) => (obj is Luid s) && Equals(s);

        public bool Equals(Luid other) => value == other.value;

        public override int GetHashCode() => HashCode.Combine((int)(value >> 32), (int)value);

        public override string ToString() => value.ToString();

        public string ToString(string format) => value.ToString(format);

        public string ToString(string format, IFormatProvider provider) => value.ToString(format, provider);

        public sealed class Comparer: IEqualityComparer<Luid>
        {
            public readonly static Comparer Default = new Comparer();

            bool IEqualityComparer<Luid>.Equals(Luid x, Luid y) => x.value == y.value;

            int IEqualityComparer<Luid>.GetHashCode(Luid obj) => obj.GetHashCode();
        }        
    }
}
