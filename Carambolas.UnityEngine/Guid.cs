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
        private uint a;

        [HideInInspector, SerializeField]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ushort b;

        [HideInInspector, SerializeField]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ushort c;

        [HideInInspector, SerializeField]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ulong d;

        public bool IsEmpty => (a | b | c | d) == 0;

        public Guid(uint a, ushort b, ushort c, ulong d) => (this.a, this.b, this.c, this.d) = (a, b, c, d);

        public Guid(int a, short b, short c, long d) => (this.a, this.b, this.c, this.d) = ((uint)a, (ushort)b, (ushort)c, (ushort)d);

        public Guid(in System.Guid guid) => (a, b, c, d) = new Converter.Guid { AsGuid = guid }.AsTuple;

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

        public static bool operator ==(Guid x, Guid y) => ((x.a ^ y.a) | (ushort)(x.b ^ y.b) | (ushort)(x.c ^ y.c) | (x.d ^ y.d)) == 0;
        public static bool operator !=(Guid x, Guid y) => !(x == y);
        public static bool operator <(Guid x, Guid y) => Compare(in x, in y) < 0;
        public static bool operator >(Guid x, Guid y) => Compare(in x, in y) > 0;

        public static explicit operator System.Guid(in Guid guid) => new Converter.Guid { AsTuple = (guid.a, guid.b, guid.c, guid.d) }.AsGuid;

        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Compare(in Guid x, in Guid y)
        {
            var msbx = ((ulong)x.a << 32) | ((uint)(x.b << 16) | x.c);
            var msby = ((ulong)y.a << 32) | ((uint)(y.b << 16) | y.c);

            var value = msbx.CompareTo(msby);
            return value == 0 ? x.d.CompareTo(y.d) : value;
        }

        public int CompareTo(Guid other) => Compare(in this, in other);

        public int CompareTo(object obj) => obj is Guid other ? Compare(in this, in other) 
            : throw new ArgumentException(string.Format(Resources.GetString(Strings.ArgumentMustBeOfType), GetType().FullName), nameof(obj));

        public override bool Equals(object obj) => obj is Guid other && Compare(in this, in other) == 0;

        public bool Equals(Guid other) => Compare(in this, in other) == 0;

        public override int GetHashCode() => ((System.Guid)this).GetHashCode();

        public override string ToString() => ((System.Guid)this).ToString();

        public string ToString(string format) => ((System.Guid)this).ToString(format);

        public string ToString(string format, IFormatProvider provider) => ((System.Guid)this).ToString(format, provider);

        public byte[] ToByteArray() => ((System.Guid)this).ToByteArray();

        public void Deconstruct(out uint a, out ushort b, out ushort c, out ulong d) => (a, b, c, d) = (this.a, this.b, this.c, this.d);

        public sealed class Comparer: IEqualityComparer<Guid>
        {
            public readonly static Comparer Default = new Comparer();

            bool IEqualityComparer<Guid>.Equals(Guid x, Guid y) => Compare(x, y) == 0;

            int IEqualityComparer<Guid>.GetHashCode(Guid obj) => obj.GetHashCode();
        }
    }
}

