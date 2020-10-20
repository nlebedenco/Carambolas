using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using UnityEngine;

namespace Carambolas.UnityEngine
{
    /// <summary>
    /// A GUID that can be serialized in Unity.
    /// </summary>    
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct Guid: IComparable, IComparable<Guid>, IEquatable<Guid>
    {
        public static Guid Empty = default;

        public static Guid NewGuid() => new Guid(System.Guid.NewGuid());

        [HideInInspector, SerializeField]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]        
        private ulong msb;

        [HideInInspector, SerializeField]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]        
        private ulong lsb;

        public bool IsEmpty => (msb | lsb ) == 0;

        public Guid(in System.Guid guid)
        {
            var converter = new GuidConverter { Guid = guid };
            this.msb = converter.MSB;
            this.lsb = converter.LSB;
        }

        public Guid(uint a, ushort b, ushort c, ulong d)
        {
            this.msb = (ulong)a << 32 | (ulong)b << 16 | c;
            this.lsb = d;
        }

        public Guid(int a, short b, short c, long d) : this((uint)a, (ushort)b, (ushort)c, (ulong)d) { }

        public Guid(byte[] b) : this(new System.Guid(b)) { }

        public Guid(string s) : this(new System.Guid(s)) { }

        public Guid(int a, short b, short c, byte[] d) : this(new System.Guid(a, b, c, d)) { }

        public Guid(int a, short b, short c, byte d, byte e, byte f, byte g, byte h, byte i, byte j, byte k) : this(new System.Guid(a, b, c, d, e, f, g, h, i, j, k)) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Guid Parse(string input) => new Guid(System.Guid.Parse(input));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Guid ParseExact(string input, string format) => new Guid(System.Guid.ParseExact(input, format));

        public static bool TryParse(string input, out Guid result)
        {
            if (System.Guid.TryParse(input, out System.Guid guid))
            {
                result = new Guid(guid);
                return true;
            }

            result = default;
            return false;
        }

        public static bool TryParseExact(string input, string format, out Guid result)
        {
            if (System.Guid.TryParseExact(input, format, out System.Guid guid))
            {
                result = new Guid(guid);
                return true;
            }

            result = default;
            return false;
        }

        #region Operators

        public static bool operator ==(Guid x, Guid y) => x.msb == y.msb && x.lsb == y.lsb;
        public static bool operator !=(Guid x, Guid y) => !(x == y);
        public static bool operator <(Guid x, Guid y) => x.msb < y.msb || (x.msb == y.msb && x.lsb < y.lsb);
        public static bool operator >(Guid x, Guid y) => x.msb > y.msb || (x.msb == y.msb && x.lsb > y.lsb);

        public static implicit operator System.Guid(Guid x) => new GuidConverter { MSB = x.msb, LSB = x.lsb }.Guid;

        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Compare(in Guid a, in Guid b)
        {
            var value = a.msb.CompareTo(b.msb);
            return value == 0 ? a.lsb.CompareTo(b.lsb) : value;
        }

        public int CompareTo(Guid other) => Compare(in this, in other);

        public int CompareTo(object obj) => obj is Guid other ? Compare(in this, in other) : throw new ArgumentException(string.Format(SR.ArgumentMustBeOfType, GetType().FullName), nameof(obj));

        public override bool Equals(object obj) => obj is Guid other && Compare(in this, in other) == 0;

        public bool Equals(Guid other) => Compare(in this, in other) == 0;

        public override int GetHashCode() => ((System.Guid)this).GetHashCode();

        public override string ToString() => ((System.Guid)this).ToString();

        public string ToString(string format) => ((System.Guid)this).ToString(format);

        public string ToString(string format, IFormatProvider provider) => ((System.Guid)this).ToString(format, provider);

        public byte[] ToByteArray() => ((System.Guid)this).ToByteArray();

        public sealed class Comparer: IEqualityComparer<Guid>
        {
            public readonly static Comparer Default = new Comparer();

            bool IEqualityComparer<Guid>.Equals(Guid x, Guid y) => Compare(x, y) == 0;

            int IEqualityComparer<Guid>.GetHashCode(Guid obj) => obj.GetHashCode();
        }
    }
}

