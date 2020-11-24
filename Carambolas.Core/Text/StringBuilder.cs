using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

using Carambolas.Internal;

namespace Carambolas.Text
{
    // TODO: add extensions classes for TextMeshPro in unity (condition TMP is used, is there a define for that?)
    //      public static void SetText(this TextMeshPro self, ArraySegment<char> arraySegment) => self.SetCharArray(arraySegment.Array, arraySegmemnt.Offset, arraySegment.Count);
    //      public static void SetText(this TextMeshPro self, StringBuilder sb) => SetText(self, sb.AsArraySegment());
    //      public static void SetText(this TextMeshPro self, StringBuilderStruct sb) => SetText(self, sb.AsArraySegment());
    //
    //      A package that depends on both Carambolas.Unity and TextMeshPro will have to be created (Carambolas.Unity.TextMeshPro) for code like this 
    //      to avoid having the whole Carambolas.Unity depending on TextMeshPro
    //

    public interface IFormattableToStringBuilder
    {
        void FormatInto(ref StringBuilder.Buffer destination, IFormatProvider formatProvider, ReadOnlySpan<char> format, int alignment);
    }

    /// <summary>
    /// A memory efficient alternative to <see cref="System.Text.StringBuilder"/> built on top of <see cref="System.Buffers.ArrayPool{T}"/>
    /// with optimized formatter methods. Immediate string operations such as join, concat and format are also available as 
    /// static methods that generate minimal garbage. 
    /// </summary>
    /// <remarks>
    /// Based on https://github.com/Cysharp/ZString v2.2.0 under the MIT License.
    /// Maximum pooled array length is 1048576 chars. Absolute maximum length supported is 2146435071 chars. 
    /// Floating point formating conforms to .Net 3.0 so the default format as well as specifiers "G", and "R" will produce 
    /// the shortest "roundtrippable" string. Enphasis on "roundtrippable".
    /// </remarks>
    public sealed class StringBuilder: IDisposable
    {
        /// <summary>
        /// A value-type substitute for <see cref="System.Text.StringBuilder"/> with limited functionality. 
        /// <para/>
        /// USE WITH CAUTION. Copies must be avoided at all costs. Modifying multiple copies of 
        /// the same struct may result in corruption of the global shared pool of char arrays 
        /// (<see cref="ArrayPool{T}" langword=" where T: char"/>).
        /// </summary>
        public struct Buffer: IDisposable
        {
            internal const int DefaultCapacity = 32;

            private char[] buffer;
            private int position;
            private bool disposed;

            public int Length => position;

            public int Capacity => buffer?.Length ?? 0;

            public char this[int index]
            {
                get
                {
                    if (index < 0 || index >= position)
                        throw new IndexOutOfRangeException();

                    return buffer[index];
                }

                set
                {
                    if (index < 0 || index >= position)
                        throw new ArgumentOutOfRangeException(nameof(index));

                    buffer[index] = value;
                }
            }

            public Buffer(int capacity)
            {
                buffer = capacity > 0 ? ArrayPool<char>.Shared.Rent(capacity) : Array.Empty<char>();
                position = 0;
                disposed = false;
            }

            /// <summary>
            /// Snapshot of the internal buffer.
            /// <para/>
            /// USE WITH CAUTION. The array returned is part of a shared pool and is valid only in the scope of the call. 
            /// Further operations on the <see cref="Buffer"/> itself may result in the underlying memory being modified or 
            /// returned to the shared pool.
            /// </summary>
            public ArraySegment<char> AsArraySegment()
            {
                if (disposed)
                    throw new ObjectDisposedException(GetType().FullName);

                return new ArraySegment<char>(buffer ?? Array.Empty<char>(), 0, position);
            }

            /// <summary>
            /// Snapshot of the internal buffer.
            /// <para/>
            /// USE WITH CAUTION. The span returned is part of a shared pool and is valid only in the scope of the call. 
            /// Further operations on the <see cref="Buffer"/> itself may result in the underlying memory being modified or 
            /// returned to the shared pool.
            /// </summary>
            public ReadOnlySpan<char> AsSpan() => buffer.AsSpan(0, position);

            /// <summary>
            /// Snapshot of the internal buffer.
            /// <para/>
            /// USE WITH CAUTION. The span returned is part of a shared pool and is valid only in the scope of the call. 
            /// Further operations on the <see cref="Buffer"/> itself may result in the underlying memory being modified or 
            /// returned to the shared pool.
            /// </summary>
            public ReadOnlyMemory<char> AsMemory() => buffer.AsMemory(0, position);

            public void EnsureCapacity(int value)
            {
                if (disposed)
                    throw new ObjectDisposedException(GetType().FullName);

                var current = buffer ?? Array.Empty<char>();
                if (current.Length < value)
                {
                    var rented = ArrayPool<char>.Shared.Rent(value);
                    Array.Copy(current, rented, position);
                    if (Interlocked.Exchange(ref buffer, rented) == current && current.Length > 0)
                        ArrayPool<char>.Shared.Return(current);
                }
            }

            public void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count)
            {
                if (disposed)
                    throw new ObjectDisposedException(GetType().FullName);

                var n = position;

                if (sourceIndex < 0 || sourceIndex > n)
                    throw new ArgumentOutOfRangeException(nameof(sourceIndex));

                if (count < 0)
                    throw new ArgumentOutOfRangeException(nameof(count));

                if (sourceIndex > n - count)
                    throw new ArgumentException(string.Format(Resources.GetString(Strings.IndexOutOfRangeOrLengthIsGreaterThanBuffer), nameof(sourceIndex), nameof(count)), nameof(count));

                if (count > 0)
                    Array.Copy(buffer, 0, destination, destinationIndex, count);
            }

            public void CopyTo(int sourceIndex, Span<char> destination, int count)
            {
                if (disposed)
                    throw new ObjectDisposedException(GetType().FullName);

                if (count < 0)
                    throw new ArgumentOutOfRangeException(nameof(count));

                if (sourceIndex < 0 || sourceIndex > position)
                    throw new ArgumentOutOfRangeException(nameof(sourceIndex));
                
                if (sourceIndex > position - count)
                    throw new ArgumentException(string.Format(Resources.GetString(Strings.IndexOutOfRangeOrLengthIsGreaterThanBuffer), nameof(sourceIndex), nameof(count)), nameof(count));

                if (count > 0)
                    buffer.AsSpan(sourceIndex, count).CopyTo(destination);
            }

            public bool TryCopyTo(Span<char> destination)
            {
                if (disposed)
                    throw new ObjectDisposedException(GetType().FullName);

                return (buffer ?? Array.Empty<char>()).AsSpan(0, position).TryCopyTo(destination);
            }

            public Span<char> GetSpan(int length)
            {
                if (length <= 0)
                    return Span<char>.Empty;
                 
                var i = position;
                var n = position + length;
                RequireCapacity(n);
                position = n;
                return buffer.AsSpan(i, length);
            }

            public void Clear() => position = 0;

            public void Reset()
            {
                position = 0;
                Release();
            }

            public override string ToString() => ToString(0, position);

            public string ToString(int startIndex, int length)
            {
                if (disposed)
                    throw new ObjectDisposedException(GetType().FullName);

                if (startIndex < 0 || startIndex > position)
                    throw new ArgumentOutOfRangeException(nameof(startIndex));

                if (length < 0)
                    throw new ArgumentOutOfRangeException(nameof(length));

                if (startIndex > position - length)
                    throw new ArgumentException(string.Format(Resources.GetString(Strings.IndexOutOfRangeOrLengthIsGreaterThanBuffer), nameof(startIndex), nameof(length)), nameof(length));

                return length > 0 ? new string(buffer, startIndex, length) : string.Empty;
            }

            public bool Equals(Buffer other)
            {
                if (disposed || other.disposed || position != other.position)
                    return false;

                if (buffer == other.buffer)
                    return true;

                if (buffer == null || other.buffer == null)
                    return false;

                for (int i = 0; i < position; ++i)
                    if (buffer[i] != other.buffer[i])
                        return false;

                return true;
            }

            public void Dispose()
            {
                disposed = true;
                if (buffer != null)
                    Release();
            }

            public void Append(char c)
            {
                var n = position + 1;
                RequireCapacity(n);
                buffer[position] = c;
                position = n;
            }

            // This is to prevent the compiler from using the generic Append for strings.
            public void Append(string value) => Append(value.AsSpan());

            public void Append(string value, int start, int count)
            {
                if (value == null && start != 0 && count != 0)
                    throw new ArgumentNullException(nameof(value));
                Append(value.AsSpan(start, count));
            }

            public void Append(Buffer value) => Append(value.AsSpan());

            public void Append(ReadOnlySpan<char> value)
            {
                if (value.IsEmpty)
                    return;

                var n = position + value.Length;
                RequireCapacity(n);
                value.CopyTo(buffer.AsSpan(position));
                position = n;
            }

            public void Append(char c, int count)
            {
                if (count < 0)
                    throw new ArgumentOutOfRangeException(nameof(count));

                if (count > 0)
                {
                    var n = position + count;
                    RequireCapacity(n);
                    buffer.AsSpan(position, count).Fill(c);
                    position = n;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void Append<T>(T value) => Formatter<T>.Format(value, ref this, CultureInfo.CurrentCulture, default, 0);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void Append<T>(T value, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                // First check if the format provider has a custom formatter.
                // This will produce allocations and possibly boxing of the generic value but the user is in control here.
                if (formatProvider != null && formatProvider.GetFormat(typeof(ICustomFormatter)) is ICustomFormatter cf)
                {
                    Append(cf.Format(formatString.ToString(), value, formatProvider));
                    return;
                }

                Formatter<T>.Format(value, ref this, formatProvider, formatString, alignment);
            }

            #region Append Format

            private readonly ref struct ArgumentList<T>
            {
                private readonly IList<T> list;
                private readonly IReadOnlyList<T> rolist;
                private readonly ReadOnlySpan<T> span;

                public readonly int Count;

                public ArgumentList(IList<T> args)
                {
                    list = args ?? throw new ArgumentNullException(nameof(args));
                    rolist = default;
                    span = default;
                    Count = args.Count;
                }

                public ArgumentList(IReadOnlyList<T> args)
                {
                    this.list = default;
                    rolist = args ?? throw new ArgumentNullException(nameof(args));
                    span = default;
                    Count = args.Count;
                }

                public ArgumentList(ReadOnlySpan<T> args)
                {
                    list = default;
                    rolist = default;
                    this.span = args;
                    Count = span.Length;
                }

                public T this[int index]
                {
                    get
                    {
                        if (list != null)
                            return list[index];

                        if (rolist != null)
                            return rolist[index];

                        return span[index];
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void AppendFormat<T>(IFormatProvider formatProvider, string format, T[] args) => AppendFormat(formatProvider, format ?? throw new ArgumentNullException(nameof(format)), new ArgumentList<T>((IList<T>)args));

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void AppendFormat<T>(IFormatProvider formatProvider, string format, IList<T> args) => AppendFormat(formatProvider, format ?? throw new ArgumentNullException(nameof(format)), new ArgumentList<T>(args));

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void AppendFormat<T>(IFormatProvider formatProvider, string format, IReadOnlyList<T> args) => AppendFormat(formatProvider, format ?? throw new ArgumentNullException(nameof(format)), new ArgumentList<T>(args));

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void AppendFormat<T>(IFormatProvider formatProvider, string format, ReadOnlySpan<T> args) => AppendFormat(formatProvider, format ?? throw new ArgumentNullException(nameof(format)), new ArgumentList<T>(args));

            private void AppendFormat<T>(IFormatProvider formatProvider, string format, in ArgumentList<T> args)
            {
                // Use a temporary to avoid damaging this instance in case either format string or args are invalid.
                using (var temporary = new Buffer(format.Length))
                {
                    var n = format.Length;
                    var open = false;
                    var k = 0; // start of ordinary chars to copy
                    var p = 0; // start of format string macro                    
                    var i = 0;

                    for (i = 0; i < n; ++i)
                    {
                        var c = format[i];
                        switch (c)
                        {
                            case '{':
                                if (open)
                                    throw new FormatException();

                                if (i < n - 1 && format[i + 1] == '{') // escaped
                                {
                                    temporary.Append(format.AsSpan(k, i - k));
                                    temporary.Append('{');
                                    i++;
                                    k = i + 1;
                                    continue;
                                }

                                temporary.Append(format.AsSpan(k, i - k));
                                open = true;
                                p = i + 1;
                                break;
                            case '}':
                                if (open)
                                {
                                    open = false;
                                    var parsed = FormatParser.Parse(format.AsSpan(p, i - p));
                                    {
                                        if (parsed.Index >= args.Count)
                                            throw new FormatException(Resources.GetString(Strings.NotEnoughArguments));

                                        temporary.Append(args[parsed.Index], formatProvider, parsed.FormatString, parsed.Alignment);
                                    }
                                    k = i + 1;
                                    continue;
                                }

                                if (i < n - 1 && format[i + 1] == '}') // escaped
                                {
                                    temporary.Append(format.AsSpan(k, i - k));
                                    temporary.Append('}');
                                    i++;
                                    k = i + 1;
                                    continue;
                                }

                                throw new FormatException();                                
                            default:
                                break;
                        }
                    }

                    if (open)
                        throw new FormatException();

                    Append(temporary);
                    if (k < i)
                        Append(format.AsSpan(k, i - k));
                }             
            }

            internal void AppendFormat<T1>(IFormatProvider formatProvider, string format, T1 arg1)
            {
                if (format == null)
                    throw new ArgumentNullException(nameof(format));

                // Use a temporary to avoid damaging this instance in case either format string or args are invalid.
                using (var temporary = new Buffer(format.Length))
                {
                    var n = format.Length;
                    var open = false;
                    var k = 0; // start of ordinary chars to copy
                    var p = 0; // start of format string macro                    
                    var i = 0;

                    for (i = 0; i < n; ++i)
                    {
                        var c = format[i];
                        switch (c)
                        {
                            case '{':
                                if (open)
                                    throw new FormatException();

                                if (i < n - 1 && format[i + 1] == '{') // escaped
                                {
                                    temporary.Append(format.AsSpan(k, i - k));
                                    temporary.Append('{');
                                    i++;
                                    k = i + 1;
                                    continue;
                                }

                                temporary.Append(format.AsSpan(k, i - k));
                                open = true;
                                p = i + 1;
                                break;
                            case '}':
                                if (open)
                                {
                                    open = false;
                                    var parsed = FormatParser.Parse(format.AsSpan(p, i - p));
                                    switch (parsed.Index)
                                    {
                                        case 0:
                                            temporary.Append(arg1, formatProvider, parsed.FormatString, parsed.Alignment);
                                            break;
                                        default:
                                            throw new FormatException(Resources.GetString(Strings.NotEnoughArguments));
                                    }
                                    k = i + 1;
                                    continue;
                                }

                                if (i < n - 1 && format[i + 1] == '}') // escaped
                                {
                                    temporary.Append(format.AsSpan(k, i - k));
                                    temporary.Append('}');
                                    i++;
                                    k = i + 1;
                                    continue;
                                }

                                throw new FormatException();
                            default:
                                break;
                        }
                    }

                    if (open)
                        throw new FormatException();

                    Append(temporary);
                    if (k < i)
                        Append(format.AsSpan(k, i - k));
                }
            }

            internal void AppendFormat<T1, T2>(IFormatProvider formatProvider, string format, T1 arg1, T2 arg2)
            {
                if (format == null)
                    throw new ArgumentNullException(nameof(format));

                // Use a temporary to avoid damaging this instance in case either format string or args are invalid.
                using (var temporary = new Buffer(format.Length))
                {
                    var n = format.Length;
                    var open = false;
                    var k = 0; // start of ordinary chars to copy
                    var p = 0; // start of format string macro                    
                    var i = 0;

                    for (i = 0; i < n; ++i)
                    {
                        var c = format[i];
                        switch (c)
                        {
                            case '{':
                                if (open)
                                    throw new FormatException();

                                if (i < n - 1 && format[i + 1] == '{') // escaped
                                {
                                    temporary.Append(format.AsSpan(k, i - k));
                                    temporary.Append('{');
                                    i++;
                                    k = i + 1;
                                    continue;
                                }

                                temporary.Append(format.AsSpan(k, i - k));
                                open = true;
                                p = i + 1;
                                break;
                            case '}':
                                if (open)
                                {
                                    open = false;
                                    var parsed = FormatParser.Parse(format.AsSpan(p, i - p));
                                    switch (parsed.Index)
                                    {
                                        case 0:
                                            temporary.Append(arg1, formatProvider, parsed.FormatString, parsed.Alignment);
                                            break;
                                        case 1:
                                            temporary.Append(arg2, formatProvider, parsed.FormatString, parsed.Alignment);
                                            break;
                                        default:
                                            throw new FormatException(Resources.GetString(Strings.NotEnoughArguments));
                                    }
                                    k = i + 1;
                                    continue;
                                }

                                if (i < n - 1 && format[i + 1] == '}') // escaped
                                {
                                    temporary.Append(format.AsSpan(k, i - k));
                                    temporary.Append('}');
                                    i++;
                                    k = i + 1;
                                    continue;
                                }

                                throw new FormatException();
                            default:
                                break;
                        }
                    }

                    if (open)
                        throw new FormatException();

                    Append(temporary);
                    if (k < i)
                        Append(format.AsSpan(k, i - k));
                }
            }

            internal void AppendFormat<T1, T2, T3>(IFormatProvider formatProvider, string format, T1 arg1, T2 arg2, T3 arg3)
            {
                if (format == null)
                    throw new ArgumentNullException(nameof(format));

                // Use a temporary to avoid damaging this instance in case either format string or args are invalid.
                using (var temporary = new Buffer(format.Length))
                {
                    var n = format.Length;
                    var open = false;
                    var k = 0; // start of ordinary chars to copy
                    var p = 0; // start of format string macro                    
                    var i = 0;

                    for (i = 0; i < n; ++i)
                    {
                        var c = format[i];
                        switch (c)
                        {
                            case '{':
                                if (open)
                                    throw new FormatException();

                                if (i < n - 1 && format[i + 1] == '{') // escaped
                                {
                                    temporary.Append(format.AsSpan(k, i - k));
                                    temporary.Append('{');
                                    i++;
                                    k = i + 1;
                                    continue;
                                }

                                temporary.Append(format.AsSpan(k, i - k));
                                open = true;
                                p = i + 1;
                                break;
                            case '}':
                                if (open)
                                {
                                    open = false;
                                    var parsed = FormatParser.Parse(format.AsSpan(p, i - p));
                                    switch (parsed.Index)
                                    {
                                        case 0:
                                            temporary.Append(arg1, formatProvider, parsed.FormatString, parsed.Alignment);
                                            break;
                                        case 1:
                                            temporary.Append(arg2, formatProvider, parsed.FormatString, parsed.Alignment);
                                            break;
                                        case 2:
                                            temporary.Append(arg3, formatProvider, parsed.FormatString, parsed.Alignment);
                                            break;
                                        default:
                                            throw new FormatException(Resources.GetString(Strings.NotEnoughArguments));
                                    }
                                    k = i + 1;
                                    continue;
                                }

                                if (i < n - 1 && format[i + 1] == '}') // escaped
                                {
                                    temporary.Append(format.AsSpan(k, i - k));
                                    temporary.Append('}');
                                    i++;
                                    k = i + 1;
                                    continue;
                                }

                                throw new FormatException();
                            default:
                                break;
                        }
                    }

                    if (open)
                        throw new FormatException();

                    Append(temporary);
                    if (k < i)
                        Append(format.AsSpan(k, i - k));
                }
            }

            internal void AppendFormat<T1, T2, T3, T4>(IFormatProvider formatProvider, string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            {
                if (format == null)
                    throw new ArgumentNullException(nameof(format));

                // Use a temporary to avoid damaging this instance in case either format string or args are invalid.
                using (var temporary = new Buffer(format.Length))
                {
                    var n = format.Length;
                    var open = false;
                    var k = 0; // start of ordinary chars to copy
                    var p = 0; // start of format string macro                    
                    var i = 0;

                    for (i = 0; i < n; ++i)
                    {
                        var c = format[i];
                        switch (c)
                        {
                            case '{':
                                if (open)
                                    throw new FormatException();

                                if (i < n - 1 && format[i + 1] == '{') // escaped
                                {
                                    temporary.Append(format.AsSpan(k, i - k));
                                    temporary.Append('{');
                                    i++;
                                    k = i + 1;
                                    continue;
                                }

                                temporary.Append(format.AsSpan(k, i - k));
                                open = true;
                                p = i + 1;
                                break;
                            case '}':
                                if (open)
                                {
                                    open = false;
                                    var parsed = FormatParser.Parse(format.AsSpan(p, i - p));
                                    switch (parsed.Index)
                                    {
                                        case 0:
                                            temporary.Append(arg1, formatProvider, parsed.FormatString, parsed.Alignment);
                                            break;
                                        case 1:
                                            temporary.Append(arg2, formatProvider, parsed.FormatString, parsed.Alignment);
                                            break;
                                        case 2:
                                            temporary.Append(arg3, formatProvider, parsed.FormatString, parsed.Alignment);
                                            break;
                                        case 3:
                                            temporary.Append(arg4, formatProvider, parsed.FormatString, parsed.Alignment);
                                            break;
                                        default:
                                            throw new FormatException(Resources.GetString(Strings.NotEnoughArguments));
                                    }
                                    k = i + 1;
                                    continue;
                                }

                                if (i < n - 1 && format[i + 1] == '}') // escaped
                                {
                                    temporary.Append(format.AsSpan(k, i - k));
                                    temporary.Append('}');
                                    i++;
                                    k = i + 1;
                                    continue;
                                }

                                throw new FormatException();
                            default:
                                break;
                        }
                    }

                    if (open)
                        throw new FormatException();

                    Append(temporary);
                    if (k < i)
                        Append(format.AsSpan(k, i - k));
                }
            }

            #endregion

            public void Insert(int index, ReadOnlySpan<char> value, int count = 1)
            {
                if (disposed)
                    throw new ObjectDisposedException(GetType().FullName);

                if (index < 0 || index > position)
                    throw new ArgumentOutOfRangeException(nameof(index));

                if (count < 0)
                    throw new ArgumentOutOfRangeException(nameof(count));

                if (value.Length == 0 || count == 0)
                    return;

                var current = buffer ?? Array.Empty<char>();
                var required = position + (value.Length * count);
                if (current.Length < required)
                {
                    var rented = ArrayPool<char>.Shared.Rent(RecommendedCapacity(current.Length, required));
                    // Copy first half
                    current.AsSpan(0, index).CopyTo(rented);
                    // Copy value multiple times
                    var j = index;
                    for (int i = 0; i < count; ++i, j += value.Length)
                        value.CopyTo(rented.AsSpan(j));
                    // Copy second half
                    current.AsSpan(index, position - index).CopyTo(rented.AsSpan(j));
                    // Replace the buffer reference
                    if (Interlocked.Exchange(ref buffer, rented) == current && current.Length > 0)
                    {
                        position = required;
                        ArrayPool<char>.Shared.Return(current);
                    }
                }
                else
                {
                    // Move second half to the right
                    current.AsSpan(index, position - index).CopyTo(current.AsSpan(index + (value.Length * count)));
                    // Copy value multiple times
                    for (int i = 0, j = index; i < count; ++i, j += value.Length)
                        value.CopyTo(current.AsSpan(j));

                    position = required;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Replace(char oldChar, char newChar) => Replace(oldChar, newChar, 0, position);

            public void Replace(char oldChar, char newChar, int startIndex, int count)
            {
                if (disposed)
                    throw new ObjectDisposedException(GetType().FullName);

                var n = position;

                if (startIndex < 0 || startIndex > n)
                    throw new ArgumentOutOfRangeException(nameof(startIndex));

                if (count < 0)
                    throw new ArgumentOutOfRangeException(nameof(count));

                if (startIndex > n - count)
                    throw new ArgumentException(string.Format(Resources.GetString(Strings.IndexOutOfRangeOrLengthIsGreaterThanBuffer), nameof(startIndex), nameof(count)), nameof(count));

                var current = buffer;
                int endIndex = startIndex + count;
                for (int i = startIndex; i < endIndex; i++)
                    if (current[i] == oldChar)
                        current[i] = newChar;
            }

            public void Replace(ReadOnlySpan<char> oldValue, ReadOnlySpan<char> newValue, int startIndex, int count)
            {
                if (disposed)
                    throw new ObjectDisposedException(GetType().FullName);

                if (oldValue.IsEmpty)
                    throw new ArgumentException(Resources.GetString(Strings.ArgumentIsEmpty), nameof(oldValue));

                var n = position;

                if (startIndex < 0 || startIndex > n)
                    throw new ArgumentOutOfRangeException(nameof(startIndex));

                if (count < 0)
                    throw new ArgumentOutOfRangeException(nameof(count));

                if (startIndex > n - count)
                    throw new ArgumentException(string.Format(Resources.GetString(Strings.IndexOutOfRangeOrLengthIsGreaterThanBuffer), nameof(startIndex), nameof(count)), nameof(count));

                if (oldValue.Length == 0)
                    return;

                if (count > 0)
                {
                    var current = buffer;
                    var readOnlySpan = new ReadOnlySpan<char>(current, 0, n);

                    var endIndex = startIndex + count;
                    var matchCount = 0;

                    for (int i = startIndex; i < endIndex; i += oldValue.Length)
                    {
                        var span = readOnlySpan.Slice(i, endIndex - i);
                        var pos = span.IndexOf(oldValue, StringComparison.Ordinal);
                        if (pos == -1)
                            break;

                        i += pos;
                        matchCount++;
                    }

                    if (matchCount > 0)
                    {
                        var m = n + ((newValue.Length - oldValue.Length) * matchCount);
                        if (m > position)
                        {
                            var rented = ArrayPool<char>.Shared.Rent(RecommendedCapacity(current.Length, m));
                            CopyAndReplace(readOnlySpan, rented.AsSpan(0, m), oldValue, newValue, startIndex, count);
                            if (Interlocked.Exchange(ref buffer, rented) == current && current.Length > 0)
                                ArrayPool<char>.Shared.Return(current);

                        }
                        else // no need to allocate a new buffer
                        {
                            CopyAndReplace(readOnlySpan, current.AsSpan(0, m), oldValue, newValue, startIndex, count);
                        }

                        position = m;
                    }
                }
            }

            private static void CopyAndReplace(ReadOnlySpan<char> source, Span<char> destination, ReadOnlySpan<char> oldValue, ReadOnlySpan<char> newValue, int sourceIndex, int count)
            {
                // If newValue.Length <= oldValue.Length then source and destination may point to the same memory 
                // but it's ok because the source piece copied is larger so we'll never overwrite non-matching chars.
                // Otherwise newValue.Length > oldValue.Length so source and destination must point to different 
                // memory buffers.

                if (sourceIndex > 0)
                    source.Slice(0, sourceIndex).CopyTo(destination);

                var endIndex = sourceIndex + count;
                var destinationIndex = sourceIndex;
                for (; sourceIndex < endIndex; sourceIndex += oldValue.Length)
                {
                    var span = source.Slice(sourceIndex, endIndex - sourceIndex);
                    var pos = span.IndexOf(oldValue, StringComparison.Ordinal);
                    if (pos == -1)
                    {
                        // Copy remaining bytes and finish
                        span.CopyTo(destination.Slice(destinationIndex));
                        destinationIndex += span.Length;
                        break;
                    }

                    // Match found

                    // Copy chars before the match, if any
                    if (pos > 0)
                    {
                        span.Slice(0, pos).CopyTo(destination.Slice(destinationIndex));
                        destinationIndex += pos;
                    }

                    // Copy new value
                    newValue.CopyTo(destination.Slice(destinationIndex));
                    destinationIndex += newValue.Length;
                    sourceIndex += pos;
                }

                if (endIndex < source.Length)
                    source.Slice(endIndex).CopyTo(destination.Slice(destinationIndex));
            }

            public void Remove(int startIndex, int length)
            {
                if (disposed)
                    throw new ObjectDisposedException(GetType().FullName);

                var n = position;

                if (startIndex < 0 || startIndex > position)
                    throw new ArgumentOutOfRangeException(nameof(startIndex));

                if (length < 0)
                    throw new ArgumentOutOfRangeException(nameof(length));

                if (startIndex > n - length)
                    throw new ArgumentException(string.Format(Resources.GetString(Strings.IndexOutOfRangeOrLengthIsGreaterThanBuffer), nameof(startIndex), nameof(length)), nameof(length));

                if (length > 0)
                {
                    var current = buffer;
                    var remainder = startIndex + length;
                    current.AsSpan(remainder, n - remainder).CopyTo(current.AsSpan(startIndex));
                    position = n - length;
                }
            }

            private static int RecommendedCapacity(int current, int required)
            {
                // 2146435071 is the maximum size allowed for a single array dimmension in .NET
                // according to https://docs.microsoft.com/en-us/dotnet/api/system.array?redirectedfrom=MSDN&view=netstandard-2.0)
                // Even if gcAllowVeryLargeObjects we would have to provide overloads supporting long for every method that may index 
                // the internal char buffer array and some operations such as remove, replace and insert would take a considerable 
                // amount of time uder such large arrays.

                if (required > 2146435071)
                    throw new ArgumentOutOfRangeException(nameof(required));

                int value;
                if (current == 0)
                    value = DefaultCapacity;
                else if (current < (1 << 19)) // maxArrayLength of ArrayPool<T>.Shared is 2^20 
                    value = current << 1;
                else
                    value = (1 << 20);
                
                if (value < required)
                    value = required;

                return value;
            }

            private void RequireCapacity(int min)
            {
                if (disposed)
                    throw new ObjectDisposedException(GetType().FullName);

                var current = buffer ?? Array.Empty<char>();
                if (current.Length < min)
                {
                    var rented = ArrayPool<char>.Shared.Rent(RecommendedCapacity(current.Length, min));
                    Array.Copy(current, rented, position);
                    if (Interlocked.Exchange(ref buffer, rented) == current && current.Length > 0)
                        ArrayPool<char>.Shared.Return(current);
                }
            }

            private void Release()
            {
                // Use Interlocked.Exchange to prevent double return to the pool 
                // in case multiple threads trying to Insert or Dispose.
                var released = Interlocked.Exchange(ref buffer, null);
                if (released != null && released.Length > 0)
                    ArrayPool<char>.Shared.Return(released);
            }
        }

        private static class FormatParser
        {
            // Syntax: 

            public readonly ref struct ParseResult
            {
                public readonly int Index;
                public readonly int Alignment;
                public readonly ReadOnlySpan<char> FormatString;

                public ParseResult(int index, int alignment, ReadOnlySpan<char> formatString)
                {
                    Index = index;
                    Alignment = alignment;
                    FormatString = formatString;
                }
            }

            private static int ParseValue(ReadOnlySpan<char> span, ref int j)
            {
                var m = span.Length;
                if (j >= m)
                    throw new FormatException();

                var value = 0;
                do
                {
                    var c = span[j];
                    if (!char.IsDigit(c))
                        break;

                    value = (value * 10) + c - '0'; // make sure we don't accept an arbitrarily long number that wraps around causing havoc
                    if (value > 9999999) // max valid alignment is 9999999
                        throw new FormatException();

                    j++;
                }
                while (j < m);

                return value;
            }

            private static void SkipWhiteSpace(ReadOnlySpan<char> span, ref int j)
            {
                var m = span.Length;
                while (j < m && span[j] == ' ')
                    j++;
            }

            /// <summary>
            /// Parse a format string macro in the form: argindex[,alignment][:formatString]
            /// </summary>
            public static ParseResult Parse(ReadOnlySpan<char> format)
            {
                var n = format.Length;
                var i = 0;
                
                SkipWhiteSpace(format, ref i);
                var argindex = ParseValue(format, ref i);
                SkipWhiteSpace(format, ref i);

                var argalign = 0;
                if (i == n)
                    return new ParseResult(argindex, argalign, ReadOnlySpan<char>.Empty);

                if (format[i] == ',')
                {
                    i++;
                    SkipWhiteSpace(format, ref i);
                    int sign;
                    if (format[i] == '-')
                    {
                        sign = -1;
                        i++;
                    }
                    else
                    {
                        sign = 1;
                    }
                    argalign = ParseValue(format, ref i) * sign;
                    SkipWhiteSpace(format, ref i);
                    if (i == n)
                        return new ParseResult(argindex, argalign, ReadOnlySpan<char>.Empty);
                }

                if (format[i] == ':')
                {
                    i++;
                    return new ParseResult(argindex, argalign, i < n ? format.Slice(i) : ReadOnlySpan<char>.Empty);
                }

                throw new FormatException();
            }
        }

        private Buffer buffer;

        private string eol = Environment.NewLine;

        public string NewLine
        {
            get => eol;
            set => eol = value ?? throw new ArgumentNullException(nameof(value));
        }

        public int Length => buffer.Length;

        public int Capacity => buffer.Capacity;

        public char this[int index]
        {
            get => buffer[index];

            set => buffer[index] = value;
        }

        public StringBuilder() : this(0) { }

        public StringBuilder(int capacity) => buffer = new Buffer(capacity);

        /// <summary>
        /// Snapshot of the internal buffer.
        /// <para/>
        /// USE WITH CAUTION. The array returned is part of a shared pool and is valid only in the scope of the call. 
        /// Further operations on the <see cref="Buffer"/> itself may result in the underlying memory being modified or 
        /// returned to the shared pool.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArraySegment<char> AsArraySegment() => buffer.AsArraySegment();

        /// <summary>
        /// Snapshot of the internal buffer.
        /// <para/>
        /// USE WITH CAUTION. The span returned is part of a shared pool and is valid only in the scope of the call. 
        /// Further operations on the <see cref="Buffer"/> itself may result in the underlying memory being modified or 
        /// returned to the shared pool.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<char> AsSpan() => buffer.AsSpan();

        /// <summary>
        /// Snapshot of the internal buffer.
        /// <para/>
        /// USE WITH CAUTION. The span returned is part of a shared pool and is valid only in the scope of the call. 
        /// Further operations on the <see cref="Buffer"/> itself may result in the underlying memory being modified or 
        /// returned to the shared pool.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyMemory<char> AsMemory() => buffer.AsMemory();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureCapacity(int value) => buffer.EnsureCapacity(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count) => buffer.CopyTo(sourceIndex, destination, destinationIndex, count);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(int sourceIndex, Span<char> destination, int count) => buffer.CopyTo(sourceIndex, destination, count);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryCopyTo(Span<char> destination) => buffer.TryCopyTo(destination);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() => buffer.Clear();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset() => buffer.Reset();

        public override string ToString() => buffer.ToString();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(int startIndex, int count) => buffer.ToString(startIndex, count);

        public bool Equals(StringBuilder other) => buffer.Equals(other.buffer);

        public void Dispose() => buffer.Dispose();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder Append(char c)
        {
            buffer.Append(c);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder Append(char[] value)
        {
            buffer.Append(value.AsSpan());
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder Append(char[] value, int start, int count)
        {
            buffer.Append(value.AsSpan(start, count));
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder Append(string value)
        {
            buffer.Append(value);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder Append(string value, int start, int count)
        {
            buffer.Append(value, start, count);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder Append(ReadOnlySpan<char> value)
        {
            buffer.Append(value);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder Append(StringBuilder value)
        {
            buffer.Append(value.buffer);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder Append(char c, int count)
        {
            buffer.Append(c, count);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder Append<T>(T value)
        {
            buffer.Append(value);
            return this;
        }

        #region Append Line

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendLine() => Append(eol);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendLine(char value) => Append(value).Append(eol);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendLine(char[] value) => Append(value).Append(eol);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendLine(char[] value, int start, int count) => Append(value, start, count).Append(eol);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendLine(string value) => Append(value).Append(eol);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendLine(string value, int start, int count) => Append(value, start, count).Append(eol);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendLine(ReadOnlySpan<char> span) => Append(span).Append(eol);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendLine<T>(T value) => Append(value).Append(eol);

        #endregion 

        #region Append Join 

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin(char separator, params char[] values) => AppendJoin(separator, (IList<char>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin(char separator, List<char> values) => AppendJoin(separator, (IList<char>)values);

        public StringBuilder AppendJoin(char separator, IList<char> values)
        {
            ReadOnlySpan<char> s = stackalloc char[1] { separator };
            return AppendJoin(s, values);
        }

        public StringBuilder AppendJoin(char separator, IReadOnlyList<char> values)
        {
            ReadOnlySpan<char> s = stackalloc char[1] { separator };
            return AppendJoin(s, values);
        }

        public StringBuilder AppendJoin(char separator, IEnumerable<char> values)
        {
            ReadOnlySpan<char> s = stackalloc char[1] { separator };
            return AppendJoin(s, values);
        }

        public StringBuilder AppendJoin(char separator, ReadOnlySpan<char> values)
        {
            ReadOnlySpan<char> s = stackalloc char[1] { separator };
            return AppendJoin(s, values);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin(string separator, params char[] values) => AppendJoin(separator.AsSpan(), values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin(string separator, List<char> values) => AppendJoin(separator.AsSpan(), (IList<char>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin(string separator, IList<char> values) => AppendJoin(separator.AsSpan(), values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin(string separator, IReadOnlyList<char> values) => AppendJoin(separator.AsSpan(), values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin(string separator, IEnumerable<char> values) => AppendJoin(separator.AsSpan(), values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin(string separator, ReadOnlySpan<char> values) => AppendJoin(separator.AsSpan(), values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin(ReadOnlySpan<char> separator, params char[] values) => AppendJoin(separator, (IList<char>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin(ReadOnlySpan<char> separator, List<char> values) => AppendJoin(separator, (IList<char>)values);

        public StringBuilder AppendJoin(ReadOnlySpan<char> separator, IList<char> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            if (values.Count == 0)
                return this;

            buffer.Append(values[0]);
            var n = values.Count;
            for (int i = 1; i < n; ++i)
            {
                buffer.Append(separator);
                buffer.Append(values[i]);
            }

            return this;
        }

        public StringBuilder AppendJoin(ReadOnlySpan<char> separator, IReadOnlyList<char> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            if (values.Count == 0)
                return this;

            buffer.Append(values[0]);
            var n = values.Count;
            for (int i = 1; i < n; ++i)
            {
                buffer.Append(separator);
                buffer.Append(values[i]);
            }

            return this;
        }

        public StringBuilder AppendJoin(ReadOnlySpan<char> separator, IEnumerable<char> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            using (var enumerator = values.GetEnumerator())
            {
                if (!enumerator.MoveNext())
                    return this;

                buffer.Append(enumerator.Current);
                while (enumerator.MoveNext())
                {
                    buffer.Append(separator);
                    buffer.Append(enumerator.Current);
                }
            }

            return this;
        }

        public StringBuilder AppendJoin(ReadOnlySpan<char> separator, ReadOnlySpan<char> values)
        {
            if (values.Length == 0)
                return this;

            buffer.Append(values[0]);
            for (int i = 1; i < values.Length; ++i)
            {
                buffer.Append(separator);
                buffer.Append(values[i]);
            }

            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin(char separator, params string[] values) => AppendJoin(separator, (IList<string>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin(char separator, List<string> values) => AppendJoin(separator, (IList<string>)values);

        public StringBuilder AppendJoin(char separator, IList<string> values)
        {
            ReadOnlySpan<char> s = stackalloc char[1] { separator };
            return AppendJoin(s, values);
        }

        public StringBuilder AppendJoin(char separator, IReadOnlyList<string> values)
        {
            ReadOnlySpan<char> s = stackalloc char[1] { separator };
            return AppendJoin(s, values);
        }

        public StringBuilder AppendJoin(char separator, IEnumerable<string> values)
        {
            ReadOnlySpan<char> s = stackalloc char[1] { separator };
            return AppendJoin(s, values);
        }

        public StringBuilder AppendJoin(char separator, ReadOnlySpan<string> values)
        {
            ReadOnlySpan<char> s = stackalloc char[1] { separator };
            return AppendJoin(s, values);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin(string separator, params string[] values) => AppendJoin(separator, (IList<string>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin(string separator, List<string> values) => AppendJoin(separator, (IList<string>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin(string separator, IList<string> values) => AppendJoin(separator.AsSpan(), values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin(string separator, IReadOnlyList<string> values) => AppendJoin(separator.AsSpan(), values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin(string separator, IEnumerable<string> values) => AppendJoin(separator.AsSpan(), values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin(string separator, ReadOnlySpan<string> values) => AppendJoin(separator.AsSpan(), values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin(ReadOnlySpan<char> separator, params string[] values) => AppendJoin(separator, (IList<string>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin(ReadOnlySpan<char> separator, List<string> values) => AppendJoin(separator, (IList<string>)values);

        public StringBuilder AppendJoin(ReadOnlySpan<char> separator, IList<string> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            if (values.Count == 0)
                return this;

            buffer.Append(values[0]);
            var n = values.Count;
            for (int i = 1; i < n; ++i)
            {
                buffer.Append(separator);
                buffer.Append(values[i]);
            }

            return this;
        }

        public StringBuilder AppendJoin(ReadOnlySpan<char> separator, IReadOnlyList<string> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            if (values.Count == 0)
                return this;

            buffer.Append(values[0]);
            var n = values.Count;
            for (int i = 1; i < n; ++i)
            {
                buffer.Append(separator);
                buffer.Append(values[i]);
            }

            return this;
        }

        public StringBuilder AppendJoin(ReadOnlySpan<char> separator, IEnumerable<string> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            using (var enumerator = values.GetEnumerator())
            {
                if (!enumerator.MoveNext())
                    return this;

                buffer.Append(enumerator.Current);
                while (enumerator.MoveNext())
                {
                    buffer.Append(separator);
                    buffer.Append(enumerator.Current);
                }
            }

            return this;
        }

        public StringBuilder AppendJoin(ReadOnlySpan<char> separator, ReadOnlySpan<string> values)
        {
            if (values.Length == 0)
                return this;

            buffer.Append(values[0]);
            for (int i = 1; i < values.Length; ++i)
            {
                buffer.Append(separator);
                buffer.Append(values[i]);
            }

            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin(char separator, params object[] values) => AppendJoin(separator, (IList<object>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin<T>(char separator, T[] values) => AppendJoin(separator, (IList<T>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin<T>(char separator, List<T> values) => AppendJoin(separator, (IList<T>)values);

        public StringBuilder AppendJoin<T>(char separator, IList<T> values)
        {
            ReadOnlySpan<char> s = stackalloc char[1] { separator };
            return AppendJoin(s, values);
        }

        public StringBuilder AppendJoin<T>(char separator, IReadOnlyList<T> values)
        {
            ReadOnlySpan<char> s = stackalloc char[1] { separator };
            return AppendJoin(s, values);
        }

        public StringBuilder AppendJoin<T>(char separator, IEnumerable<T> values)
        {
            ReadOnlySpan<char> s = stackalloc char[1] { separator };
            return AppendJoin(s, values);
        }

        public StringBuilder AppendJoin<T>(char separator, ReadOnlySpan<T> values)
        {
            ReadOnlySpan<char> s = stackalloc char[1] { separator };
            return AppendJoin(s, values);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin(string separator, params object[] values) => AppendJoin(separator, (IList<object>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin<T>(string separator, T[] values) => AppendJoin(separator, (IList<T>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin<T>(string separator, List<T> values) => AppendJoin(separator, (IList<T>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin<T>(string separator, IList<T> values) => AppendJoin(separator.AsSpan(), values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin<T>(string separator, IReadOnlyList<T> values) => AppendJoin(separator.AsSpan(), values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin<T>(string separator, IEnumerable<T> values) => AppendJoin(separator.AsSpan(), values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin<T>(string separator, ReadOnlySpan<T> values) => AppendJoin(separator.AsSpan(), values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin(ReadOnlySpan<char> separator, params object[] values) => AppendJoin(separator, (IList<object>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin<T>(ReadOnlySpan<char> separator, T[] values) => AppendJoin(separator, (IList<T>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendJoin<T>(ReadOnlySpan<char> separator, List<T> values) => AppendJoin(separator, (IList<T>)values);

        public StringBuilder AppendJoin<T>(ReadOnlySpan<char> separator, IList<T> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            if (values.Count == 0)
                return this;

            buffer.Append(values[0]);
            var n = values.Count;
            for (int i = 1; i < n; ++i)
            {
                buffer.Append(separator);
                buffer.Append(values[i]);
            }

            return this;
        }

        public StringBuilder AppendJoin<T>(ReadOnlySpan<char> separator, IReadOnlyList<T> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            if (values.Count == 0)
                return this;

            buffer.Append(values[0]);
            var n = values.Count;
            for (int i = 1; i < n; ++i)
            {
                buffer.Append(separator);
                buffer.Append(values[i]);
            }

            return this;
        }

        public StringBuilder AppendJoin<T>(ReadOnlySpan<char> separator, IEnumerable<T> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            using (var enumerator = values.GetEnumerator())
            {
                if (!enumerator.MoveNext())
                    return this;

                buffer.Append(enumerator.Current);
                while (enumerator.MoveNext())
                {
                    buffer.Append(separator);
                    buffer.Append(enumerator.Current);
                }
            }

            return this;
        }

        public StringBuilder AppendJoin<T>(ReadOnlySpan<char> separator, ReadOnlySpan<T> values)
        {
            if (values.Length == 0)
                return this;

            buffer.Append(values[0]);
            for (int i = 1; i < values.Length; ++i)
            {
                buffer.Append(separator);
                buffer.Append(values[i]);
            }

            return this;
        }

        #endregion

        #region Append Format

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendFormat(string format, params object[] args) => AppendFormat(CultureInfo.CurrentCulture, format, args);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendFormat(IFormatProvider formatProvider, string format, params object[] args) => AppendFormat(formatProvider, format, (IList<object>)args);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendFormat<T>(string format, T[] args) => AppendFormat(CultureInfo.CurrentCulture, format, args);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendFormat<T>(IFormatProvider formatProvider, string format, T[] args) => AppendFormat(formatProvider, format, (IList<T>)args);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendFormat<T>(string format, IList<T> args) => AppendFormat(CultureInfo.CurrentCulture, format, args);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendFormat<T>(IFormatProvider formatProvider, string format, IList<T> args)
        {
            buffer.AppendFormat(formatProvider, format, args);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendFormat<T>(string format, IReadOnlyList<T> args) => AppendFormat(CultureInfo.CurrentCulture, format, args);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendFormat<T>(IFormatProvider formatProvider, string format, IReadOnlyList<T> args)
        {
            buffer.AppendFormat(formatProvider, format, args);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendFormat<T>(string format, ReadOnlySpan<T> args) => AppendFormat(CultureInfo.CurrentCulture, format, args);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendFormat<T>(IFormatProvider formatProvider, string format, ReadOnlySpan<T> args)
        {
            buffer.AppendFormat(formatProvider, format, args);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendFormat<T1>(string format, T1 arg1)
        {
            buffer.AppendFormat(CultureInfo.CurrentCulture, format, arg1);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendFormat<T1>(IFormatProvider formatProvider, string format, T1 arg1)
        {
            buffer.AppendFormat(formatProvider, format, arg1);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendFormat<T1, T2>(string format, T1 arg1, T2 arg2)
        {
            buffer.AppendFormat(CultureInfo.CurrentCulture, format, arg1, arg2);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendFormat<T1, T2>(IFormatProvider formatProvider, string format, T1 arg1, T2 arg2)
        {
            buffer.AppendFormat(formatProvider, format, arg1, arg2);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendFormat<T1, T2, T3>(string format, T1 arg1, T2 arg2, T3 arg3)
        {
            buffer.AppendFormat(CultureInfo.CurrentCulture, format, arg1, arg2, arg3);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendFormat<T1, T2, T3>(IFormatProvider formatProvider, string format, T1 arg1, T2 arg2, T3 arg3)
        {
            buffer.AppendFormat(formatProvider, format, arg1, arg2, arg3);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendFormat<T1, T2, T3, T4>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            buffer.AppendFormat(CultureInfo.CurrentCulture, format, arg1, arg2, arg3, arg4);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder AppendFormat<T1, T2, T3, T4>(IFormatProvider formatProvider, string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            buffer.AppendFormat(formatProvider, format, arg1, arg2, arg3, arg4);
            return this;
        }

        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder Insert(int index, char[] value, int startIndex, int charCount)
        {
            buffer.Insert(index, value.AsSpan(startIndex, charCount), 1);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder Insert(int index, string value) => Insert(index, value, 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder Insert(int index, string value, int count)
        {
            buffer.Insert(index, value.AsSpan(), count);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder Insert(int index, char value) => Insert(index, value, 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder Insert(int index, char value, int count)
        {
            ReadOnlySpan<char> span = stackalloc char[] { value };
            buffer.Insert(index, span, count);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder Insert(int index, ReadOnlySpan<char> value) => Insert(index, value, 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder Insert(int index, ReadOnlySpan<char> value, int count)
        {
            buffer.Insert(index, value, count);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder Replace(char oldChar, char newChar) => Replace(oldChar, newChar);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder Replace(char oldChar, char newChar, int startIndex, int count)
        {
            buffer.Replace(oldChar, newChar, startIndex, count);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder Replace(string oldValue, string newValue) => Replace(oldValue, newValue, 0, buffer.Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder Replace(string oldValue, string newValue, int startIndex, int count)
        {
            buffer.Replace((oldValue ?? throw new ArgumentNullException(nameof(oldValue))).AsSpan(), newValue.AsSpan(), startIndex, count);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder Replace(ReadOnlySpan<char> oldValue, ReadOnlySpan<char> newValue) => Replace(oldValue, newValue, 0, buffer.Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder Replace(ReadOnlySpan<char> oldValue, ReadOnlySpan<char> newValue, int startIndex, int count)
        {
            buffer.Replace(oldValue, newValue, startIndex, count);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder Remove(int startIndex, int length)
        {
            buffer.Remove(startIndex, length);
            return this;
        }

        #region Static Join

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(char separator, params char[] values) => Join(separator, (IList<char>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(char separator, List<char> values) => Join(separator, (IList<char>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(char separator, IList<char> values)
        {
            ReadOnlySpan<char> s = stackalloc char[1] { separator };
            return Join(s, values);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(char separator, IReadOnlyList<char> values)
        {
            ReadOnlySpan<char> s = stackalloc char[1] { separator };
            return Join(s, values);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(char separator, IEnumerable<char> values)
        {
            ReadOnlySpan<char> s = stackalloc char[1] { separator };
            return Join(s, values);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(char separator, ReadOnlySpan<char> values)
        {
            ReadOnlySpan<char> s = stackalloc char[1] { separator };
            return Join(s, values);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(string separator, params char[] values) => Join(separator, (IList<char>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(string separator, List<char> values) => Join(separator, (IList<char>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(string separator, IList<char> values) => Join(separator.AsSpan(), values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(string separator, IReadOnlyList<char> values) => Join(separator.AsSpan(), values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(string separator, IEnumerable<char> values) => Join(separator.AsSpan(), values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(string separator, ReadOnlySpan<char> values) => Join(separator.AsSpan(), values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(ReadOnlySpan<char> separator, params char[] values) => Join(separator, (IList<char>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(ReadOnlySpan<char> separator, List<char> values) => Join(separator, (IList<char>)values);

        public static string Join(ReadOnlySpan<char> separator, IList<char> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            if (values.Count == 0)
                return string.Empty;

            using (var sb = new Buffer())
            {
                sb.Append(values[0]);
                var n = values.Count;
                for (int i = 1; i < n; ++i)
                {
                    sb.Append(separator);
                    sb.Append(values[i]);
                }

                return sb.ToString();
            }
        }

        public static string Join(ReadOnlySpan<char> separator, IReadOnlyList<char> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            if (values.Count == 0)
                return string.Empty;

            using (var sb = new Buffer())
            {
                sb.Append(values[0]);
                var n = values.Count;
                for (int i = 1; i < n; ++i)
                {
                    sb.Append(separator);
                    sb.Append(values[i]);
                }

                return sb.ToString();
            }
        }

        public static string Join(ReadOnlySpan<char> separator, IEnumerable<char> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            using (var enumerator = values.GetEnumerator())
            {
                if (!enumerator.MoveNext())
                    return string.Empty;

                using (var sb = new Buffer())
                {
                    sb.Append(enumerator.Current);
                    while (enumerator.MoveNext())
                    {
                        sb.Append(separator);
                        sb.Append(enumerator.Current);
                    }

                    return sb.ToString();
                }
            }
        }

        public static string Join(ReadOnlySpan<char> separator, ReadOnlySpan<char> values)
        {
            if (values.Length == 0)
                return string.Empty;

            using (var sb = new Buffer())
            {
                sb.Append(values[0]);
                for (int i = 1; i < values.Length; ++i)
                {
                    sb.Append(separator);
                    sb.Append(values[i]);
                }

                return sb.ToString();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(char separator, params string[] values) => Join(separator, (IList<string>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(char separator, List<string> values) => Join(separator, (IList<string>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(char separator, IList<string> values)
        {
            ReadOnlySpan<char> s = stackalloc char[1] { separator };
            return Join(s, values);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(char separator, IReadOnlyList<string> values)
        {
            ReadOnlySpan<char> s = stackalloc char[1] { separator };
            return Join(s, values);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(char separator, IEnumerable<string> values)
        {
            ReadOnlySpan<char> s = stackalloc char[1] { separator };
            return Join(s, values);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(char separator, ReadOnlySpan<string> values)
        {
            ReadOnlySpan<char> s = stackalloc char[1] { separator };
            return Join(s, values);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(string separator, params string[] values) => Join(separator, (IList<string>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(string separator, List<string> values) => Join(separator, (IList<string>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(string separator, IList<string> values) => Join(separator.AsSpan(), values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(string separator, IReadOnlyList<string> values) => Join(separator.AsSpan(), values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(string separator, IEnumerable<string> values) => Join(separator.AsSpan(), values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(string separator, ReadOnlySpan<string> values) => Join(separator.AsSpan(), values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(ReadOnlySpan<char> separator, params string[] values) => Join(separator, (IList<string>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(ReadOnlySpan<char> separator, List<string> values) => Join(separator, (IList<string>)values);

        public static string Join(ReadOnlySpan<char> separator, IList<string> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            if (values.Count == 0)
                return string.Empty;

            using (var sb = new Buffer())
            {
                sb.Append(values[0]);
                var n = values.Count;
                for (int i = 1; i < n; ++i)
                {
                    sb.Append(separator);
                    sb.Append(values[i]);
                }

                return sb.ToString();
            }
        }

        public static string Join(ReadOnlySpan<char> separator, IReadOnlyList<string> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            if (values.Count == 0)
                return string.Empty;

            using (var sb = new Buffer())
            {
                sb.Append(values[0]);
                var n = values.Count;
                for (int i = 1; i < n; ++i)
                {
                    sb.Append(separator);
                    sb.Append(values[i]);
                }

                return sb.ToString();
            }
        }

        public static string Join(ReadOnlySpan<char> separator, IEnumerable<string> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            using (var enumerator = values.GetEnumerator())
            {
                if (!enumerator.MoveNext())
                    return string.Empty;

                using (var sb = new Buffer())
                {
                    sb.Append(enumerator.Current);
                    while (enumerator.MoveNext())
                    {
                        sb.Append(separator);
                        sb.Append(enumerator.Current);
                    }

                    return sb.ToString();
                }
            }
        }

        public static string Join(ReadOnlySpan<char> separator, ReadOnlySpan<string> values)
        {
            if (values.Length == 0)
                return string.Empty;

            using (var sb = new Buffer())
            {
                sb.Append(values[0]);
                for (int i = 1; i < values.Length; ++i)
                {
                    sb.Append(separator);
                    sb.Append(values[i]);
                }

                return sb.ToString();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(char separator, params object[] values) => Join(separator, (IList<object>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join<T>(char separator, T[] values) => Join(separator, (IList<T>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join<T>(char separator, List<T> values) => Join(separator, (IList<T>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join<T>(char separator, IList<T> values)
        {
            ReadOnlySpan<char> s = stackalloc char[1] { separator };
            return Join(s, values);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join<T>(char separator, IReadOnlyList<T> values)
        {
            ReadOnlySpan<char> s = stackalloc char[1] { separator };
            return Join(s, values);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join<T>(char separator, IEnumerable<T> values)
        {
            ReadOnlySpan<char> s = stackalloc char[1] { separator };
            return Join(s, values);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join<T>(char separator, ReadOnlySpan<T> values)
        {
            ReadOnlySpan<char> s = stackalloc char[1] { separator };
            return Join(s, values);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(string separator, params object[] values) => Join(separator, (IList<object>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join<T>(string separator, T[] values) => Join(separator, (IList<T>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join<T>(string separator, List<T> values) => Join(separator, (IList<T>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join<T>(string separator, IList<T> values) => Join(separator.AsSpan(), values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join<T>(string separator, IReadOnlyList<T> values) => Join(separator.AsSpan(), values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join<T>(string separator, IEnumerable<T> values) => Join(separator.AsSpan(), values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join<T>(string separator, ReadOnlySpan<T> values) => Join(separator.AsSpan(), values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join(ReadOnlySpan<char> separator, params object[] values) => Join(separator, (IList<object>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join<T>(ReadOnlySpan<char> separator, T[] values) => Join(separator, (IList<T>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join<T>(ReadOnlySpan<char> separator, List<T> values) => Join(separator, (IList<T>)values);

        public static string Join<T>(ReadOnlySpan<char> separator, IList<T> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            if (values.Count == 0)
                return string.Empty;

            using (var sb = new Buffer())
            {
                sb.Append(values[0]);
                var n = values.Count;
                for (int i = 1; i < n; ++i)
                {
                    sb.Append(separator);
                    sb.Append(values[i]);
                }

                return sb.ToString();
            }
        }

        public static string Join<T>(ReadOnlySpan<char> separator, IReadOnlyList<T> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            if (values.Count == 0)
                return string.Empty;

            using (var sb = new Buffer())
            {
                sb.Append(values[0]);
                var n = values.Count;
                for (int i = 1; i < n; ++i)
                {
                    sb.Append(separator);
                    sb.Append(values[i]);
                }

                return sb.ToString();
            }
        }

        public static string Join<T>(ReadOnlySpan<char> separator, IEnumerable<T> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            using (var enumerator = values.GetEnumerator())
            {
                if (!enumerator.MoveNext())
                    return string.Empty;

                using (var sb = new Buffer())
                {
                    sb.Append(enumerator.Current);
                    while (enumerator.MoveNext())
                    {
                        sb.Append(separator);
                        sb.Append(enumerator.Current);
                    }

                    return sb.ToString();
                }
            }
        }

        public static string Join<T>(ReadOnlySpan<char> separator, ReadOnlySpan<T> values)
        {
            if (values.Length == 0)
                return string.Empty;

            using (var sb = new Buffer())
            {
                sb.Append(values[0]);
                for (int i = 1; i < values.Length; ++i)
                {
                    sb.Append(separator);
                    sb.Append(values[i]);
                }

                return sb.ToString();
            }
        }

        #endregion

        #region Static Concat

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Concat(params char[] values) => Concat((IList<char>)values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Concat(List<char> values) => Concat((IList<char>)values);

        public static string Concat(IList<char> values)
        {
            using (var sb = new Buffer())
            {
                for (int i = 0; i < values.Count; ++i)
                    sb.Append(values[i]);

                return sb.ToString();
            }
        }

        public static string Concat(IReadOnlyList<char> values)
        {
            using (var sb = new Buffer())
            {
                for (int i = 0; i < values.Count; ++i)
                    sb.Append(values[i]);

                return sb.ToString();
            }
        }

        public static string Concat(IEnumerable<char> values)
        {
            using (var sb = new Buffer())
            {
                foreach (var value in values)
                    sb.Append(value);

                return sb.ToString();
            }
        }

        public static string Concat(ReadOnlySpan<char> values)
        {
            using (var sb = new Buffer())
            {
                for (int i = 0; i < values.Length; ++i)
                    sb.Append(values[i]);

                return sb.ToString();
            }
        }

        public static string Concat(params string[] values) => Concat((IList<string>)values);

        public static string Concat(List<string> values) => Concat((IList<string>)values);

        public static string Concat(IList<string> values)
        {
            using (var sb = new Buffer())
            {
                for (int i = 0; i < values.Count; ++i)
                    sb.Append(values[i]);

                return sb.ToString();
            }
        }

        public static string Concat(IReadOnlyList<string> values)
        {
            using (var sb = new Buffer())
            {
                for (int i = 0; i < values.Count; ++i)
                    sb.Append(values[i]);

                return sb.ToString();
            }
        }

        public static string Concat(IEnumerable<string> values)
        {
            using (var sb = new Buffer())
            {
                foreach (var value in values)
                    sb.Append(value);

                return sb.ToString();
            }
        }

        public static string Concat(ReadOnlySpan<string> values)
        {
            using (var sb = new Buffer())
            {
                for (int i = 0; i < values.Length; ++i)
                    sb.Append(values[i]);

                return sb.ToString();
            }
        }

        public static string Concat(params object[] values) => Concat((IList<object>)values);

        public static string Concat<T>(T[] values) => Concat((IList<T>)values);

        public static string Concat<T>(List<T> values) => Concat((IList<T>)values);

        public static string Concat<T>(IList<T> values)
        {
            using (var sb = new Buffer())
            {
                for (int i = 0; i < values.Count; ++i)
                    sb.Append(values[i]);

                return sb.ToString();
            }
        }

        public static string Concat<T>(IReadOnlyList<T> values)
        {
            using (var sb = new Buffer())
            {
                for (int i = 0; i < values.Count; ++i)
                    sb.Append(values[i]);

                return sb.ToString();
            }
        }

        public static string Concat<T>(IEnumerable<T> values)
        {
            using (var sb = new Buffer())
            {
                foreach (var value in values)
                    sb.Append(value);

                return sb.ToString();
            }
        }

        public static string Concat<T>(ReadOnlySpan<T> values)
        {
            using (var sb = new Buffer())
            {
                for (int i = 0; i < values.Length; ++i)
                    sb.Append(values[i]);

                return sb.ToString();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Concat<T1, T2>(T1 arg1, T2 arg2)
        {
            using (var sb = new Buffer())
            {
                sb.Append(arg1);
                sb.Append(arg2);

                return sb.ToString();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Concat<T1, T2, T3>(T1 arg1, T2 arg2, T3 arg3)
        {
            using (var sb = new Buffer())
            {
                sb.Append(arg1);
                sb.Append(arg2);
                sb.Append(arg3);

                return sb.ToString();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Concat<T1, T2, T3, T4>(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            using (var sb = new Buffer())
            {
                sb.Append(arg1);
                sb.Append(arg2);
                sb.Append(arg3);
                sb.Append(arg4);

                return sb.ToString();
            }
        }

        #endregion

        #region Static Format

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Format(string format, params object[] args) => Format(CultureInfo.CurrentCulture, format, args);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Format(IFormatProvider formatProvider, string format, params object[] args) => Format(formatProvider, format, (IList<object>)args);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Format<T>(string format, T[] args) => Format(CultureInfo.CurrentCulture, format, args);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Format<T>(IFormatProvider formatProvider, string format, T[] args) => Format(formatProvider, format, (IList<T>)args);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Format<T>(string format, List<T> args) => Format(CultureInfo.CurrentCulture, format, args);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Format<T>(IFormatProvider formatProvider, string format, List<T> args) => Format(formatProvider, format, (IList<T>)args);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Format<T>(string format, IList<T> args) => Format(CultureInfo.CurrentCulture, format, args);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Format<T>(IFormatProvider formatProvider, string format, IList<T> args)
        {
            using (var sb = new Buffer())
            {
                sb.AppendFormat(formatProvider, format, args);
                return sb.ToString();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Format<T>(string format, IReadOnlyList<T> args) => Format(CultureInfo.CurrentCulture, format, args);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Format<T>(IFormatProvider formatProvider, string format, IReadOnlyList<T> args)
        {
            using (var sb = new Buffer())
            {
                sb.AppendFormat(formatProvider, format, args);
                return sb.ToString();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Format<T>(string format, IEnumerable<T> args) => Format(CultureInfo.CurrentCulture, format, args);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Format<T>(IFormatProvider formatProvider, string format, IEnumerable<T> args)
        {
            using (var sb = new Buffer())
            {
                sb.AppendFormat(formatProvider, format, args);
                return sb.ToString();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Format<T>(string format, ReadOnlySpan<T> args) => Format(CultureInfo.CurrentCulture, format, args);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Format<T>(IFormatProvider formatProvider, string format, ReadOnlySpan<T> args)
        {
            using (var sb = new Buffer())
            {
                sb.AppendFormat(formatProvider, format, args);
                return sb.ToString();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Format<T1>(string format, T1 arg1)
        {
            using (var sb = new Buffer())
            {
                sb.AppendFormat(CultureInfo.CurrentCulture, format, arg1);
                return sb.ToString();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Format<T1>(IFormatProvider formatProvider, string format, T1 arg1)
        {
            using (var sb = new Buffer())
            {
                sb.AppendFormat(formatProvider, format, arg1);
                return sb.ToString();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Format<T1, T2>(string format, T1 arg1, T2 arg2)
        {
            using (var sb = new Buffer())
            {
                sb.AppendFormat(CultureInfo.CurrentCulture, format, arg1, arg2);
                return sb.ToString();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Format<T1, T2>(IFormatProvider formatProvider, string format, T1 arg1, T2 arg2)
        {
            using (var sb = new Buffer())
            {
                sb.AppendFormat(formatProvider, format, arg1, arg2);
                return sb.ToString();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Format<T1, T2, T3>(string format, T1 arg1, T2 arg2, T3 arg3)
        {
            using (var sb = new Buffer())
            {
                sb.AppendFormat(CultureInfo.CurrentCulture, format, arg1, arg2, arg3);
                return sb.ToString();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Format<T1, T2, T3>(IFormatProvider formatProvider, string format, T1 arg1, T2 arg2, T3 arg3)
        {
            using (var sb = new Buffer())
            {
                sb.AppendFormat(formatProvider, format, arg1, arg2, arg3);
                return sb.ToString();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Format<T1, T2, T3, T4>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            using (var sb = new Buffer())
            {
                sb.AppendFormat(CultureInfo.CurrentCulture, format, arg1, arg2, arg3, arg4);
                return sb.ToString();
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Format<T1, T2, T3, T4>(IFormatProvider formatProvider, string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            using (var sb = new Buffer())
            {
                sb.AppendFormat(formatProvider, format, arg1, arg2, arg3, arg4);
                return sb.ToString();
            }
        }

        #endregion

        private delegate void FormatDelegate<T>(T value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> format, int alignment);

        private static class Formatter<T>
        {
            public readonly static FormatDelegate<T> Format;

            static Formatter()
            {
                var type = typeof(T);
                if (type == typeof(string))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<string>(Formatter.Format);
                else if (type == typeof(char[]))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<char[]>(Formatter.Format);
                else if (type == typeof(char))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<char>(Formatter.Format);
                else if (type == typeof(StringBuilder))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<StringBuilder>(Formatter.Format);
                else if (type == typeof(Buffer))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<Buffer>(Formatter.Format);
                else if (type.IsEnum)
                    Format = Enum.Format;
                else if (type == typeof(sbyte))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<sbyte>(Formatter.Format);
                else if (type == typeof(short))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<short>(Formatter.Format);
                else if (type == typeof(int))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<int>(Formatter.Format);
                else if (type == typeof(long))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<long>(Formatter.Format);
                else if (type == typeof(byte))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<byte>(Formatter.Format);
                else if (type == typeof(ushort))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<ushort>(Formatter.Format);
                else if (type == typeof(uint))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<uint>(Formatter.Format);
                else if (type == typeof(ulong))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<ulong>(Formatter.Format);
                else if (type == typeof(float))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<float>(Formatter.Format);
                else if (type == typeof(double))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<double>(Formatter.Format);
                else if (type == typeof(decimal))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<decimal>(Formatter.Format);
                else if (type == typeof(bool))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<bool>(Formatter.Format);
                else if (type == typeof(TimeSpan))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<TimeSpan>(Formatter.Format);
                else if (type == typeof(DateTime))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<DateTime>(Formatter.Format);
                else if (type == typeof(DateTimeOffset))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<DateTimeOffset>(Formatter.Format);
                else if (type == typeof(Guid))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<Guid>(Formatter.Format);
                else if (type == typeof(char?))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<char?>(Formatter.Format);
                else if (type == typeof(Buffer?))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<Buffer?>(Formatter.Format);
                else if (type == typeof(sbyte?))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<sbyte?>(Formatter.Format);
                else if (type == typeof(short?))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<short?>(Formatter.Format);
                else if (type == typeof(int?))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<int?>(Formatter.Format);
                else if (type == typeof(long?))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<long?>(Formatter.Format);
                else if (type == typeof(byte?))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<byte?>(Formatter.Format);
                else if (type == typeof(ushort?))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<ushort?>(Formatter.Format);
                else if (type == typeof(uint?))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<uint?>(Formatter.Format);
                else if (type == typeof(ulong?))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<ulong?>(Formatter.Format);
                else if (type == typeof(float?))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<float?>(Formatter.Format);
                else if (type == typeof(double?))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<double?>(Formatter.Format);
                else if (type == typeof(decimal?))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<decimal?>(Formatter.Format);
                else if (type == typeof(bool?))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<bool?>(Formatter.Format);
                else if (type == typeof(TimeSpan?))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<TimeSpan?>(Formatter.Format);
                else if (type == typeof(DateTime?))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<DateTime?>(Formatter.Format);
                else if (type == typeof(DateTimeOffset?))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<DateTimeOffset?>(Formatter.Format);
                else if (type == typeof(Guid?))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<Guid?>(Formatter.Format);
                else if (type == typeof(IntPtr))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<IntPtr>(Formatter.Format);
                else if (type == typeof(UIntPtr))
                    Format = (FormatDelegate<T>)(object)new FormatDelegate<UIntPtr>(Formatter.Format);
                else
                    Format = Formatter.Format;
            }

            public static class Enum
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public static void Format(T value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> format, int alignment) 
                    => Formatter.Format(Enum<T>.GetName(value), ref buffer, formatProvider, format, alignment);
            }
        }

        private static class Formatter
        {
            private const int UInt32NumberBufferLength = 10 + 1;        // 10 for the longest input: 4,294,967,295
            private const int UInt64NumberBufferLength = 20 + 1;        // 20 for the longest input: 18,446,744,073,709,551,615
            private const int SingleNumberBufferLength = 112 + 1 + 1;   // 112 for the longest input + 1 for rounding: 1.40129846E-45
            private const int DoubleNumberBufferLength = 767 + 1 + 1;   // 767 for the longest input + 1 for rounding: 4.9406564584124654E-324
            private const int DecimalNumberBufferLength = 29 + 1 + 1;   // 29 for the longest input + 1 for rounding

            private const int Int32Precision = 10;
            private const int UInt32Precision = 10;
            private const int Int64Precision = 19;
            private const int UInt64Precision = 20;

            // SinglePrecisionCustomFormat and DoublePrecisionCustomFormat are used to ensure that
            // custom format strings return the same string as in previous releases when the format
            // would return x digits or less (where x is the value of the corresponding constant).
            // In order to support more digits, we would need to update ParseFormatSpecifier to pre-parse
            // the format and determine exactly how many digits are being requested and whether they
            // represent "significant digits" or "digits after the decimal point".
            private const int SinglePrecisionForCustomFormat = 7;
            private const int DoublePrecisionForCustomFormat = 15;

            // SinglePrecision and DoublePrecision represent the maximum number of digits required
            // to guarantee that any given Single or Double can roundtrip. Some numbers may require
            // less, but none will require more.
            private const int SingleRoundTripPrecision = 9;
            private const int DoubleRoundTripPrecision = 17;

            private const int DecimalPrecision = 29;

            private const int DefaultPrecisionForExponentialFormat = 6;

            private const string PositiveNumberFormat = "#";

            private static readonly string[] NegativeNumberFormats = 
            {
                "(#)", "-#", "- #", "#-", "# -",
            };

            private static readonly string[] PositiveCurrencyFormats = 
            {
                "$#", "#$", "$ #", "# $"
            };

            private static readonly string[] NegativeCurrencyFormats =
            {
                "($#)", "-$#", "$-#", "$#-",
                "(#$)", "-#$", "#-$", "#$-",
                "-# $", "-$ #", "# $-", "$ #-",
                "$ -#", "#- $", "($ #)", "(# $)"
            };

            private static readonly string[] PositivePercentFormats =
            {
                "# %", "#%", "%#", "% #"
            };

            private static readonly string[] NegativePercentFormats =
            {
                "-# %", "-#%", "-%#",
                "%-#", "%#-",
                "#-%", "#%-",
                "-% #", "# %-", "% #-",
                "% -#", "#- %"
            };

            private enum NumberBufferKind: byte
            {
                Unknown = 0,
                Integer = 1,
                Decimal = 2,
                FloatingPoint = 3,
            }

            private static char ParseFormatSpecifier(ReadOnlySpan<char> format, out int precision)
            {
                var c = default(char);
                if (format.Length > 0)
                {
                    // If the format begins with a symbol, see if it's a standard format
                    // with or without a specified number of digits.
                    c = format[0];
                    if ((uint)(c - 'A') <= 'Z' - 'A' || (uint)(c - 'a') <= 'z' - 'a')
                    {
                        // Fast path for sole symbol, e.g. "D"
                        if (format.Length == 1)
                        {
                            precision = -1;
                            return c;
                        }

                        if (format.Length == 2)
                        {
                            // Fast path for symbol and single digit, e.g. "X4"
                            var d = format[1] - '0';
                            if ((uint)d < 10)
                            {
                                precision = d;
                                return c;
                            }
                        }
                        else if (format.Length == 3)
                        {
                            // Fast path for symbol and double digit, e.g. "F12"
                            var d1 = format[1] - '0';
                            var d2 = format[2] - '0';
                            if ((uint)d1 < 10 && (uint)d2 < 10)
                            {
                                precision = d1 * 10 + d2;
                                return c;
                            }
                        }

                        // Fallback for symbol and any length digits.  The digits value must be >= 0 && <= 99,
                        // but it can begin with any number of 0s, and thus we may need to check more than two
                        // digits.  Further, for compat, we need to stop when we hit a null char.
                        var n = 0;
                        var i = 1;
                        while (i < format.Length && (((uint)format[i] - '0') < 10) && n < 10)
                            n = (n * 10) + format[i++] - '0';

                        // If we're at the end of the digits rather than having stopped because we hit something
                        // other than a digit or overflowed, return the standard format info.
                        if (i == format.Length || format[i] == '\0')
                        {
                            precision = n;
                            return c;
                        }
                    }
                }

                // Default empty format to be "G"; custom format is signified with '\0'.
                precision = -1;
                // For compatibility, treat '\0' as the end of the specifier, even if the specifier extends beyond it.
                return (format.Length == 0 || c == '\0') ? 'G' : '\0';
            }

            private static int FindSection(ReadOnlySpan<char> format, int section)
            {
                if (section == 0)
                    return 0;

                var src = 0;
                while (true)
                {
                    if (src >= format.Length)
                        return 0;

                    var ch = format[src++];
                    switch (ch)
                    {
                        case '\'':
                        case '"':
                            while (src < format.Length && format[src] != 0 && format[src++] != ch)
                                ;
                            break;
                        case '\\':
                            if (src < format.Length && format[src] != 0)
                                src++;
                            break;
                        case ';':
                            if (--section != 0)
                                break;
                            if (src < format.Length && format[src] != 0 && format[src] != ';')
                                return src;
                            goto case '\0';
                        case '\0':
                            return 0;
                    }
                }
            }

            private static int Digits(uint value)
            {
                var digits = 1;
                if (value >= 100000)
                {
                    value /= 100000;
                    digits += 5;
                }

                if (value < 10)
                {
                    // do nothing
                }
                else if (value < 100)
                    digits++;
                else if (value < 1000)
                    digits += 2;
                else if (value < 10000)
                    digits += 3;
                else
                    digits += 4;

                return digits;
            }

            private static int Digits(ulong value)
            {
                var digits = 1;
                uint part;
                if (value >= 10000000)
                {
                    if (value >= 100000000000000)
                    {
                        part = (uint)(value / 100000000000000);
                        digits += 14;
                    }
                    else
                    {
                        part = (uint)(value / 10000000);
                        digits += 7;
                    }
                }
                else
                {
                    part = (uint)value;
                }

                if (part < 10)
                {
                    // do nothing
                }
                else if (part < 100)
                    digits++;
                else if (part < 1000)
                    digits += 2;
                else if (part < 10000)
                    digits += 3;
                else if (part < 100000)
                    digits += 4;
                else if (part < 1000000)
                    digits += 5;
                else
                    digits += 6;

                return digits;
            }

            private static int HexDigits(uint value)
            {
                var digits = 1;
                if (value >= 0x10000)
                {
                    value /= 0x10000;
                    digits += 4;
                }

                if (value < 0x10)
                {
                    // do nothing
                }
                else if (value < 0x100)
                    digits++;
                else if (value < 0x1000)
                    digits += 2;
                else
                    digits += 3;

                return digits;
            }

            private static int HexDigits(ulong value)
            {
                var digits = 1;
                uint part;

                if (value >= 0x1000000)
                {
                    if (value >= 0x10000000000)
                    {
                        part = (uint)(value / 0x10000000000);
                        digits += 10;
                    }
                    else
                    {
                        part = (uint)(value / 0x1000000);
                        digits += 6;
                    }
                }
                else
                {
                    part = (uint)value;
                }

                if (part < 0x10)
                {
                    // do nothing
                }
                else if (part < 0x100)
                    digits++;
                else if (part < 0x1000)
                    digits += 2;
                else if (part < 0x10000)
                    digits += 3;
                else if (part < 0x100000)
                    digits += 4;
                else
                    digits += 5;

                return digits;
            }

            private static void IntToDecString(uint value, Span<char> destination)
            {
                var i = destination.Length - 1;
                do
                {
                    value = MathEx.DivRem(value, 10, out var digit);
                    destination[i--] = (char)('0' + digit);
                    
                } while (value > 0);

                for (; i >= 0; --i)
                    destination[i] = '0';
            }

            private static void IntToDecString(ulong value, Span<char> destination)
            {
                var i = destination.Length - 1;
                do
                {
                    value = MathEx.DivRem(value, 10, out var digit);
                    destination[i--] = (char)('0' + digit);
                } while (value > 0);

                for (; i >= 0; --i)
                    destination[i] = '0';
            }

            private static void IntToHexString(uint value, Span<char> destination, char alpha = (char)('A' - 10))
            {
                var i = destination.Length - 1;
                do
                {
                    value = MathEx.DivRem(value, 16, out var digit);
                    destination[i--] = (char)((digit < 10 ? '0' : alpha) + digit);                    
                } while (value > 0);

                for (; i >= 0; --i)
                    destination[i] = '0';
            }

            private static void IntToHexString(ulong value, Span<char> destination, char alpha = (char)('A' - 10))
            {
                var i = destination.Length - 1;
                do
                {
                    value = MathEx.DivRem(value, 16, out var digit);
                    destination[i--] = (char)((digit < 10 ? '0' : alpha) + digit);
                } while (value > 0);

                for (; i >= 0; --i)
                    destination[i] = '0';
            }

            private static void Write(ReadOnlySpan<char> value, ref Buffer buffer, int alignment)
            {
                var n = value.Length;
                if (alignment > n)
                    buffer.Append(' ', alignment - n);

                buffer.Append(value);

                if (alignment < -n)
                    buffer.Append(' ', -alignment - n);
            }

            private static void Write(Span<char> value, ref Buffer buffer, int alignment)
            {
                var n = value.Length;
                if (alignment > n)
                    buffer.Append(' ', alignment - n);

                buffer.Append(value);

                if (alignment < -n)
                    buffer.Append(' ', -alignment - n);
            }
          
            private static void Write(char value, ref Buffer buffer, int alignment)
            {
                var n = 1;
                if (alignment > n)
                    buffer.Append(' ', alignment - n);

                buffer.Append(value);

                if (alignment < -n)
                    buffer.Append(' ', -alignment - n);
            }

            private static void Write(char[] value, ref Buffer buffer, int alignment)
                => Write(value.AsSpan(), ref buffer, alignment);

            private static void Write(string value, ref Buffer buffer, int alignment)
                => Write(value.AsSpan(), ref buffer, alignment);

            private static void Write(uint value, ref Buffer buffer, int minLength, int alignment)
            {
                var n = Digits(value);
                if (n < minLength)
                    n = minLength;

                if (alignment > n)
                    buffer.Append(' ', alignment - n);

                IntToDecString(value, buffer.GetSpan(n));

                if (alignment < -n)
                    buffer.Append(' ', -alignment - n);
            }

            private static void Write(ulong value, ref Buffer buffer, int minLength, int alignment)
            {
                var n = Digits(value);
                if (n < minLength)
                    n = minLength;

                if (alignment > n)
                    buffer.Append(' ', alignment - n);

                IntToDecString(value, buffer.GetSpan(n));

                if (alignment < -n)
                    buffer.Append(' ', -alignment - n);
            }

            private static void Write(int value, ref Buffer buffer, NumberFormatInfo info, int minLength, int alignment)
            {
                var unsigned = (uint)(value < 0 ? -value : value);
                var n = Digits(unsigned);
                if (n < minLength)
                    n = minLength;

                var negativeSign = info.NegativeSign;
                var m = value < 0 ? negativeSign.Length + n : n;

                if (alignment > m)
                    buffer.Append(' ', alignment - m);

                if (value < 0)
                    buffer.Append(negativeSign);

                IntToDecString(unsigned, buffer.GetSpan(n));

                if (alignment < -m)
                    buffer.Append(' ', -alignment - m);
            }

            private static void Write(long value, ref Buffer buffer, NumberFormatInfo info, int minLength, int alignment)
            {
                var unsigned = (ulong)(value < 0 ? -value : value);
                var n = Digits(unsigned);
                if (n < minLength)
                    n = minLength;

                var negativeSign = info.NegativeSign;
                var m = value < 0 ? negativeSign.Length + n : n;

                if (alignment > m)
                    buffer.Append(' ', alignment - m);

                if (value < 0)
                    buffer.Append(negativeSign);

                IntToDecString(unsigned, buffer.GetSpan(n));

                if (alignment < -m)
                    buffer.Append(' ', -alignment - m);
            }

            private static void WriteHex(uint value, ref Buffer buffer, char alpha, int minLength, int alignment)
            {
                var n = HexDigits(value);
                if (n < minLength)
                    n = minLength;

                if (alignment < -n)
                    buffer.Append(' ', -alignment - n);

                IntToHexString(value, buffer.GetSpan(n));

                if (alignment > n)
                    buffer.Append(' ', alignment - n);
            }

            private static void WriteHex(ulong value, ref Buffer buffer, char alpha, int minLength, int alignment)
            {
                var n = HexDigits(value);
                if (n < minLength)
                    n = minLength;

                if (alignment > n)
                    buffer.Append(' ', alignment - n);

                IntToHexString(value, buffer.GetSpan(n));

                if (alignment < -n)
                    buffer.Append(' ', -alignment - n);
            }
          

            private static void WriteExponent(int value, ref Buffer buffer, int digits, char exponentialSymbol, NumberFormatInfo info, bool includePositiveSign = true)
            {
                buffer.Append(exponentialSymbol);
                if (value < 0)
                {
                    value = -value;
                    buffer.Append(info.NegativeSign);                    
                }
                else if (includePositiveSign)
                {
                    buffer.Append(info.PositiveSign);
                }

                Write((uint)value, ref buffer, digits, 0);
            }

            private static void WriteFixed(ref NumberBuffer number, ref Buffer buffer, int decimalPlaces, int[] groupSizes, string decimalSeparator, string groupSeparator)
            {
                var i = 0;
                var digits = number.Digits;
                var digitPos = number.Scale;

                if (digitPos <= 0)
                {
                    buffer.Append('0');
                }
                else if (groupSizes != null)
                {
                    var resultSize = digitPos;  // Length of the resulting string.
                    var groupSizesIndex = 0;    // Index into the groupDigits array.                                            
                    var groupSize = 0;          // The current group size.

                    var groupSizesLength = groupSizes.Length;
                    var groupSeparatorLength = groupSeparator.Length;

                    // Figure out the size of the result. One can pass an empty array.
                    if (groupSizesLength != 0)
                    {
                        var groupSizesTotal = groupSizes[groupSizesIndex];
                        while (digitPos > groupSizesTotal)
                        {
                            groupSize = groupSizes[groupSizesIndex];
                            if (groupSize == 0)
                                break;

                            resultSize += groupSeparatorLength;
                            if (groupSizesIndex < groupSizesLength - 1)
                                groupSizesIndex++;

                            groupSizesTotal += groupSizes[groupSizesIndex];
                            if (groupSizesTotal < 0 || resultSize < 0)
                                throw new ArgumentOutOfRangeException(nameof(groupSizes));
                        }

                        // If you passed in an array with one entry as 0, groupSizesTotal == 0
                        groupSize = groupSizesTotal == 0 ? 0 : groupSizes[0];
                    }

                    groupSizesIndex = 0;
                    var digitCount = 0;
                    var digitLength = digits.IndexOf((byte)'\0');
                    var digitStart = digitPos < digitLength ? digitPos : digitLength;

                    var span = buffer.GetSpan(resultSize);
                    var k = resultSize - 1;
                    for (i = digitPos - 1; i >= 0; --i)
                    {
                        span[k--] = i < digitStart ? (char)digits[i] : '0';
                        if (groupSize > 0)
                        {
                            digitCount++;
                            if ((digitCount == groupSize) && (i != 0))
                            {
                                for (int j = groupSeparator.Length - 1; j >= 0; --j)
                                    span[k--] = groupSeparator[j];

                                if (groupSizesIndex < groupSizes.Length - 1)
                                {
                                    groupSizesIndex++;
                                    groupSize = groupSizes[groupSizesIndex];
                                }

                                digitCount = 0;
                            }
                        }
                    }

                    i = digitStart;
                }
                else
                {
                    do
                    {
                        buffer.Append(digits[i] != 0 ? (char)digits[i++] : '0');
                    }
                    while (--digitPos > 0);
                }

                if (decimalPlaces > 0)
                {
                    buffer.Append(decimalSeparator);
                    while (digitPos < 0 && decimalPlaces > 0)
                    {
                        buffer.Append('0');
                        digitPos++;
                        decimalPlaces--;
                    }

                    while (decimalPlaces > 0)
                    {
                        buffer.Append(digits[i] != 0 ? (char)digits[i++] : '0');
                        decimalPlaces--;
                    }
                }
            }

            private static void WriteFixed(ref NumberBuffer number, ref Buffer buffer, int decimalPlaces, NumberFormatInfo info, int alignment)
            {
                if (alignment == 0)
                {
                    if (number.IsNegative)
                        buffer.Append(info.NegativeSign);

                    WriteFixed(ref number, ref buffer, decimalPlaces, null, info.NumberDecimalSeparator, null);
                }
                else
                {
                    // There's no way to know ahead of time how many characters a NumberBuffer will require to enforce alignment so we have to resort to a temporary
                    var temporary = new Buffer(number.DigitsCount);
                    try
                    {
                        if (number.IsNegative)
                            temporary.Append(info.NegativeSign);

                        WriteFixed(ref number, ref temporary, decimalPlaces, null, info.NumberDecimalSeparator, null);

                        var n = temporary.Length;
                        if (alignment > n)
                            buffer.Append(' ', alignment - n);

                        buffer.Append(temporary);

                        if (alignment < -n)
                            buffer.Append(' ', -alignment - n);
                    }
                    finally
                    {
                        temporary.Dispose();
                    }
                }
            }

            private static void WriteCurrency(ref NumberBuffer number, ref Buffer buffer, int decimalPlaces, NumberFormatInfo info)
            {
                var format = number.IsNegative ? NegativeCurrencyFormats[info.CurrencyNegativePattern] : PositiveCurrencyFormats[info.CurrencyPositivePattern];
                foreach (char ch in format)
                {
                    switch (ch)
                    {
                        case '#':
                            WriteFixed(ref number, ref buffer, decimalPlaces, info.CurrencyGroupSizes, info.CurrencyDecimalSeparator, info.CurrencyGroupSeparator);
                            break;
                        case '-':
                            buffer.Append(info.NegativeSign);
                            break;
                        case '$':
                            buffer.Append(info.CurrencySymbol);
                            break;
                        default:
                            buffer.Append(ch);
                            break;
                    }
                }
            }

            private static void WriteCurrency(ref NumberBuffer number, ref Buffer buffer, int decimalPlaces, NumberFormatInfo info, int alignment)
            {
                if (alignment == 0)
                    WriteCurrency(ref number, ref buffer, decimalPlaces, info);
                else
                {
                    // There's no way to know ahead of time how many characters a NumberBuffer will require to enforce alignment so we have to resort to a temporary
                    var temporary = new Buffer(number.DigitsCount);
                    try
                    {
                        WriteCurrency(ref number, ref temporary, decimalPlaces, info);

                        var n = temporary.Length;
                        if (alignment > n)
                            buffer.Append(' ', alignment - n);

                        buffer.Append(temporary);

                        if (alignment < -n)
                            buffer.Append(' ', -alignment - n);
                    }
                    finally
                    {
                        temporary.Dispose();
                    }
                }
            }

            private static void WriteNumber(ref NumberBuffer number, ref Buffer buffer, int decimalPlaces, NumberFormatInfo info)
            {
                var format = number.IsNegative ? NegativeNumberFormats[info.NumberNegativePattern] : PositiveNumberFormat;
                foreach (char ch in format)
                {
                    switch (ch)
                    {
                        case '#':
                            WriteFixed(ref number, ref buffer, decimalPlaces, info.NumberGroupSizes, info.NumberDecimalSeparator, info.NumberGroupSeparator);
                            break;
                        case '-':
                            buffer.Append(info.NegativeSign);
                            break;
                        default:
                            buffer.Append(ch);
                            break;
                    }
                }
            }

            private static void WriteNumber(ref NumberBuffer number, ref Buffer buffer, int decimalPlaces, NumberFormatInfo info, int alignment)
            {
                if (alignment == 0)
                    WriteNumber(ref number, ref buffer, decimalPlaces, info);
                else
                {
                    // There's no way to know ahead of time how many characters a NumberBuffer will require to enforce alignment so we have to resort to a temporary
                    var temporary = new Buffer(number.DigitsCount);
                    try
                    {
                        WriteNumber(ref number, ref temporary, decimalPlaces, info);

                        var n = temporary.Length;
                        if (alignment > n)
                            buffer.Append(' ', alignment - n);

                        buffer.Append(temporary);

                        if (alignment < -n)
                            buffer.Append(' ', -alignment - n);
                    }
                    finally
                    {
                        temporary.Dispose();
                    }
                }
            }

            private static void WriteScientific(ref NumberBuffer number, ref Buffer buffer, int decimalPlaces, char exponentialSymbol, NumberFormatInfo info)
            {
                var i = 0;
                var dig = number.Digits;

                buffer.Append(dig[i] != 0 ? (char)dig[i++] : '0');

                if (decimalPlaces != 1) // For E0 we would like to suppress the decimal point
                    buffer.Append(info.NumberDecimalSeparator);

                while (--decimalPlaces > 0)
                    buffer.Append(dig[i] != 0 ? (char)dig[i++] : '0');

                var e = dig[0] == 0 ? 0 : number.Scale - 1;
                WriteExponent(e, ref buffer, 3, exponentialSymbol, info);
            }

            private static void WriteScientific(ref NumberBuffer number, ref Buffer buffer, int decimalPlaces, char exponentialSymbol, NumberFormatInfo info, int alignment)
            {
                if (alignment == 0)
                {
                    if (number.IsNegative)
                        buffer.Append(info.NegativeSign);

                    WriteScientific(ref number, ref buffer, decimalPlaces, exponentialSymbol, info);
                }
                else
                {
                    // There's no way to know ahead of time how many characters a NumberBuffer will require to enforce alignment so we have to resort to a temporary
                    var temporary = new Buffer(number.DigitsCount);
                    try
                    {
                        if (number.IsNegative)
                            temporary.Append(info.NegativeSign);

                        WriteScientific(ref number, ref temporary, decimalPlaces, exponentialSymbol, info);

                        var n = temporary.Length;
                        if (alignment > n)
                            buffer.Append(' ', alignment - n);

                        buffer.Append(temporary);

                        if (alignment < -n)
                            buffer.Append(' ', -alignment - n);
                    }
                    finally
                    {
                        temporary.Dispose();
                    }
                }
            }

            private static void WritePercent(ref NumberBuffer number, ref Buffer buffer, int decimalPlaces, NumberFormatInfo info)
            {
                var format = number.IsNegative ? NegativePercentFormats[info.PercentNegativePattern] : PositivePercentFormats[info.PercentPositivePattern];
                foreach (char ch in format)
                {
                    switch (ch)
                    {
                        case '#':
                            WriteFixed(ref number, ref buffer, decimalPlaces, info.PercentGroupSizes, info.PercentDecimalSeparator, info.PercentGroupSeparator);
                            break;
                        case '-':
                            buffer.Append(info.NegativeSign);
                            break;
                        case '%':
                            buffer.Append(info.PercentSymbol);
                            break;
                        default:
                            buffer.Append(ch);
                            break;
                    }
                }
            }

            private static void WritePercent(ref NumberBuffer number, ref Buffer buffer, int decimalPlaces, NumberFormatInfo info, int alignment)
            {
                if (alignment == 0)
                    WritePercent(ref number, ref buffer, decimalPlaces, info);
                else
                {
                    // There's no way to know ahead of time how many characters a NumberBuffer will require to enforce alignment so we have to resort to a temporary
                    var temporary = new Buffer(number.DigitsCount);
                    try
                    {
                        WritePercent(ref number, ref temporary, decimalPlaces, info);

                        var n = temporary.Length;
                        if (alignment > n)
                            buffer.Append(' ', alignment - n);

                        buffer.Append(temporary);

                        if (alignment < -n)
                            buffer.Append(' ', -alignment - n);
                    }
                    finally
                    {
                        temporary.Dispose();
                    }
                }
            }

            private static void WriteGeneral(ref NumberBuffer number, ref Buffer buffer, int decimalPlaces, char exponentialSymbol, NumberFormatInfo info, bool ignoreScientificNotation)
            {
                var digitPos = number.Scale;
                var scientific = false;

                if (!ignoreScientificNotation)
                {
                    // Don't switch to scientific notation
                    if (digitPos > decimalPlaces || digitPos < -3)
                    {
                        digitPos = 1;
                        scientific = true;
                    }
                }

                var i = 0;
                var digits = number.Digits;
                if (digitPos <= 0)
                {
                    buffer.Append('0');
                }
                else
                {
                    do
                    {
                        buffer.Append(digits[i] != 0 ? (char)digits[i++] : '0');
                    } while (--digitPos > 0);
                }

                if (digits[i] != 0 || digitPos < 0)
                {
                    buffer.Append(info.NumberDecimalSeparator);
                    while (digitPos < 0)
                    {
                        buffer.Append('0');
                        digitPos++;
                    }

                    while (digits[i] != 0)
                        buffer.Append((char)digits[i++]);
                }

                if (scientific)
                    WriteExponent(number.Scale - 1, ref buffer, 2, exponentialSymbol, info);
            }

            private static void WriteGeneral(ref NumberBuffer number, ref Buffer buffer, int decimalPlaces, char exponentialSymbol, NumberFormatInfo info, bool ignoreScientificNotation, int alignment)
            {
                if (alignment == 0)
                {
                    if (number.IsNegative)
                    {
                        // -0 should be formatted as 0 for decimal; integers will never be -0; any other case must have the minus.
                        if (number.Kind != NumberBufferKind.Decimal || number.Digits[0] != 0)
                            buffer.Append(info.NegativeSign);
                    }

                    WriteGeneral(ref number, ref buffer, decimalPlaces, exponentialSymbol, info, ignoreScientificNotation);
                }
                else
                {
                    // There's no way to know ahead of time how many characters a NumberBuffer will require to enforce alignment so we have to resort to a temporary
                    var temporary = new Buffer(number.DigitsCount);
                    try
                    {
                        if (number.IsNegative)
                        {
                            // -0 should be formatted as 0 for decimal; integers will never be -0; any other case must have the minus.
                            if (number.Kind != NumberBufferKind.Decimal || number.Digits[0] != 0)
                                temporary.Append(info.NegativeSign);
                        }

                        WriteGeneral(ref number, ref temporary, decimalPlaces, exponentialSymbol, info, ignoreScientificNotation);

                        var n = temporary.Length;
                        if (alignment > n)
                            buffer.Append(' ', alignment - n);

                        buffer.Append(temporary);

                        if (alignment < -n)
                            buffer.Append(' ', -alignment - n);
                    }
                    finally
                    {
                        temporary.Dispose();
                    }
                }
            }


            private static void Write(ref NumberBuffer number, ref Buffer buffer, char specifier, int digits, NumberFormatInfo info, int alignment)
            {
                var isCorrectlyRounded = (number.Kind == NumberBufferKind.FloatingPoint);

                switch (specifier & 0xFFDF) // (bitand 0xFFDF) has the effect of uppercasing the character
                {
                    case 'C':
                        if (digits < 0)
                            digits = info.CurrencyDecimalDigits;

                        number.Round(number.Scale + digits, isCorrectlyRounded);
                        WriteCurrency(ref number, ref buffer, digits, info, alignment);
                        break;
                    case 'F':
                        if (digits < 0)
                            digits = info.NumberDecimalDigits;

                        number.Round(number.Scale + digits, isCorrectlyRounded);
                        WriteFixed(ref number, ref buffer, digits, info, alignment);
                        break;
                    case 'N':
                        if (digits < 0)
                            digits = info.NumberDecimalDigits;

                        number.Round(number.Scale + digits, isCorrectlyRounded);
                        WriteNumber(ref number, ref buffer, digits, info, alignment);
                        break;
                    case 'E':
                        if (digits < 0)
                            digits = DefaultPrecisionForExponentialFormat;

                        digits++;
                        number.Round(digits, isCorrectlyRounded);
                        WriteScientific(ref number, ref buffer, digits, specifier, info, alignment);
                        break;                  
                    case 'G':
                        {
                            bool noRounding = false;
                            if (digits < 1)
                            {
                                if ((number.Kind == NumberBufferKind.Decimal) && (digits == -1))
                                {
                                    // Turn off rounding for ECMA compliance to output trailing 0's after decimal as significant
                                    noRounding = true;
                                    goto SkipRounding;
                                }
                                else
                                {
                                    // This ensures that the PAL code pads out to the correct place even when we use the default precision
                                    digits = number.DigitsCount;
                                }
                            }

                            number.Round(digits, isCorrectlyRounded);

                            SkipRounding:
                            WriteGeneral(ref number, ref buffer, digits, (char)(specifier - ('G' - 'E')), info, noRounding, alignment);
                        }
                        break;
                    case 'P':
                        if (digits < 0)
                            digits = info.PercentDecimalDigits;

                        number.Scale += 2;
                        number.Round(number.Scale + digits, isCorrectlyRounded);
                        WritePercent(ref number, ref buffer, digits, info, alignment);
                        break;
                    default:
                        throw new FormatException(string.Format(Resources.GetString(Strings.InvalidFormatSpecifier), specifier));
                }
            }
            
            private static void Write(ref NumberBuffer number, ref Buffer buffer, ReadOnlySpan<char> formatString, NumberFormatInfo info)
            {
                int digitCount;
                int decimalPos;
                int firstDigit;
                int lastDigit;
                int digPos;
                bool scientific;
                int thousandPos;
                int thousandCount = 0;
                bool thousandSeps;
                int scaleAdjust;
                int adjust;

                int section;
                int src;
                var dig = number.Digits;
                char ch;

                section = FindSection(formatString, dig[0] == 0 ? 2 : number.IsNegative ? 1 : 0);

                while (true)
                {
                    digitCount = 0;
                    decimalPos = -1;
                    firstDigit = 0x7FFFFFFF;
                    lastDigit = 0;
                    scientific = false;
                    thousandPos = -1;
                    thousandSeps = false;
                    scaleAdjust = 0;
                    src = section;

                    while (src < formatString.Length && (ch = formatString[src++]) != 0 && ch != ';')
                    {
                        switch (ch)
                        {
                            case '#':
                                digitCount++;
                                break;
                            case '0':
                                if (firstDigit == 0x7FFFFFFF)
                                    firstDigit = digitCount;
                                digitCount++;
                                lastDigit = digitCount;
                                break;
                            case '.':
                                if (decimalPos < 0)
                                    decimalPos = digitCount;
                                break;
                            case ',':
                                if (digitCount > 0 && decimalPos < 0)
                                {
                                    if (thousandPos >= 0)
                                    {
                                        if (thousandPos == digitCount)
                                        {
                                            thousandCount++;
                                            break;
                                        }
                                        thousandSeps = true;
                                    }
                                    thousandPos = digitCount;
                                    thousandCount = 1;
                                }
                                break;
                            case '%':
                                scaleAdjust += 2;
                                break;
                            case '\x2030':
                                scaleAdjust += 3;
                                break;
                            case '\'':
                            case '"':
                                while (src < formatString.Length && formatString[src] != 0 && formatString[src++] != ch)
                                    ;
                                break;
                            case '\\':
                                if (src < formatString.Length && formatString[src] != 0)
                                    src++;
                                break;
                            case 'E':
                            case 'e':
                                if ((src < formatString.Length && formatString[src] == '0') ||
                                    (src + 1 < formatString.Length && (formatString[src] == '+' || formatString[src] == '-') && formatString[src + 1] == '0'))
                                {
                                    while (++src < formatString.Length && formatString[src] == '0')
                                        ;
                                    scientific = true;
                                }
                                break;
                        }
                    }

                    if (decimalPos < 0)
                        decimalPos = digitCount;

                    if (thousandPos >= 0)
                    {
                        if (thousandPos == decimalPos)
                            scaleAdjust -= thousandCount * 3;
                        else
                            thousandSeps = true;
                    }

                    if (dig[0] != 0)
                    {
                        number.Scale += scaleAdjust;
                        int pos = scientific ? digitCount : number.Scale + digitCount - decimalPos;
                        number.Round(pos, false);
                        if (dig[0] == 0)
                        {
                            src = FindSection(formatString, 2);
                            if (src != section)
                            {
                                section = src;
                                continue;
                            }
                        }
                    }
                    else
                    {
                        if (number.Kind == NumberBufferKind.Integer)
                        {
                            // The integer types don't have a concept of -0 and decimal always format -0 as 0
                            number.IsNegative = false;
                        }
                        number.Scale = 0;      // Decimals with scale ('0.00') should be rounded.
                    }

                    break;
                }

                firstDigit = firstDigit < decimalPos ? decimalPos - firstDigit : 0;
                lastDigit = lastDigit > decimalPos ? decimalPos - lastDigit : 0;
                if (scientific)
                {
                    digPos = decimalPos;
                    adjust = 0;
                }
                else
                {
                    digPos = number.Scale > decimalPos ? number.Scale : decimalPos;
                    adjust = number.Scale - decimalPos;
                }
                src = section;

                // Adjust can be negative, so we make this an int instead of an unsigned int.
                // Adjust represents the number of characters over the formatting e.g. format string is "0000" and you are trying to
                // format 100000 (6 digits). Means adjust will be 2. On the other hand if you are trying to format 10 adjust will be
                // -2 and we'll need to fixup these digits with 0 padding if we have 0 formatting as in this example.
                Span<int> thousandsSepPos = stackalloc int[4];
                int thousandsSepCtr = -1;

                if (thousandSeps)
                {
                    // We need to precompute this outside the number formatting loop
                    if (info.NumberGroupSeparator.Length > 0)
                    {
                        // We need this array to figure out where to insert the thousands separator. We would have to traverse the string
                        // backwards. PIC formatting always traverses forwards. These indices are precomputed to tell us where to insert
                        // the thousands separator so we can get away with traversing forwards. Note we only have to compute up to digPos.
                        // The max is not bound since you can have formatting strings of the form "000,000..", and this
                        // should handle that case too.

                        int[] groupDigits = info.NumberGroupSizes;

                        int groupSizeIndex = 0;     // Index into the groupDigits array.
                        int groupTotalSizeCount = 0;
                        int groupSizeLen = groupDigits.Length;    // The length of groupDigits array.
                        if (groupSizeLen != 0)
                            groupTotalSizeCount = groupDigits[groupSizeIndex];   // The current running total of group size.
                        int groupSize = groupTotalSizeCount;

                        int totalDigits = digPos + ((adjust < 0) ? adjust : 0); // Actual number of digits in o/p
                        int numDigits = (firstDigit > totalDigits) ? firstDigit : totalDigits;
                        while (numDigits > groupTotalSizeCount)
                        {
                            if (groupSize == 0)
                                break;
                            ++thousandsSepCtr;
                            if (thousandsSepCtr >= thousandsSepPos.Length)
                            {
                                var newThousandsSepPos = new int[thousandsSepPos.Length * 2];
                                thousandsSepPos.CopyTo(newThousandsSepPos);
                                thousandsSepPos = newThousandsSepPos;
                            }

                            thousandsSepPos[thousandsSepCtr] = groupTotalSizeCount;
                            if (groupSizeIndex < groupSizeLen - 1)
                            {
                                groupSizeIndex++;
                                groupSize = groupDigits[groupSizeIndex];
                            }
                            groupTotalSizeCount += groupSize;
                        }
                    }
                }

                if (number.IsNegative && (section == 0) && (number.Scale != 0))
                    buffer.Append(info.NegativeSign);

                bool decimalWritten = false;

                var cur = 0;
                while (src < formatString.Length && (ch = formatString[src++]) != 0 && ch != ';')
                {
                    if (adjust > 0)
                    {
                        switch (ch)
                        {
                            case '#':
                            case '0':
                            case '.':
                                while (adjust > 0)
                                {
                                    // digPos will be one greater than thousandsSepPos[thousandsSepCtr] since we are at
                                    // the character after which the groupSeparator needs to be appended.
                                    buffer.Append(dig[cur] != 0 ? (char)dig[cur++] : '0');
                                    if (thousandSeps && digPos > 1 && thousandsSepCtr >= 0)
                                    {
                                        if (digPos == thousandsSepPos[thousandsSepCtr] + 1)
                                        {
                                            buffer.Append(info.NumberGroupSeparator);
                                            thousandsSepCtr--;
                                        }
                                    }
                                    digPos--;
                                    adjust--;
                                }
                                break;
                        }
                    }

                    switch (ch)
                    {
                        case '#':
                        case '0':
                            {
                                if (adjust < 0)
                                {
                                    adjust++;
                                    ch = digPos <= firstDigit ? '0' : '\0';
                                }
                                else
                                {
                                    ch = dig[cur] != 0 ? (char)dig[cur++] : digPos > lastDigit ? '0' : '\0';
                                }
                                if (ch != 0)
                                {
                                    buffer.Append(ch);
                                    if (thousandSeps && digPos > 1 && thousandsSepCtr >= 0)
                                    {
                                        if (digPos == thousandsSepPos[thousandsSepCtr] + 1)
                                        {
                                            buffer.Append(info.NumberGroupSeparator);
                                            thousandsSepCtr--;
                                        }
                                    }
                                }

                                digPos--;
                                break;
                            }
                        case '.':
                            {
                                if (digPos != 0 || decimalWritten)
                                {
                                    // For compatibility, don't echo repeated decimals
                                    break;
                                }
                                // If the format has trailing zeros or the format has a decimal and digits remain
                                if (lastDigit < 0 || (decimalPos < digitCount && dig[cur] != 0))
                                {
                                    buffer.Append(info.NumberDecimalSeparator);
                                    decimalWritten = true;
                                }
                                break;
                            }
                        case '\x2030':
                            buffer.Append(info.PerMilleSymbol);
                            break;
                        case '%':
                            buffer.Append(info.PercentSymbol);
                            break;
                        case ',':
                            break;
                        case '\'':
                        case '"':
                            while (src < formatString.Length && formatString[src] != 0 && formatString[src] != ch)
                                buffer.Append(formatString[src++]);
                            if (src < formatString.Length && formatString[src] != 0)
                                src++;
                            break;
                        case '\\':
                            if (src < formatString.Length && formatString[src] != 0)
                                buffer.Append(formatString[src++]);
                            break;
                        case 'E':
                        case 'e':
                            {
                                bool positiveSign = false;
                                int i = 0;
                                if (scientific)
                                {
                                    if (src < formatString.Length && formatString[src] == '0')
                                    {
                                        // Handles E0, which should format the same as E-0
                                        i++;
                                    }
                                    else if (src + 1 < formatString.Length && formatString[src] == '+' && formatString[src + 1] == '0')
                                    {
                                        // Handles E+0
                                        positiveSign = true;
                                    }
                                    else if (src + 1 < formatString.Length && formatString[src] == '-' && formatString[src + 1] == '0')
                                    {
                                        // Handles E-0
                                        // Do nothing, this is just a place holder s.t. we don't break out of the loop.
                                    }
                                    else
                                    {
                                        buffer.Append(ch);
                                        break;
                                    }

                                    while (++src < formatString.Length && formatString[src] == '0')
                                        i++;
                                    if (i > 10)
                                        i = 10;

                                    int exp = dig[0] == 0 ? 0 : number.Scale - decimalPos;
                                    WriteExponent(exp, ref buffer, i, ch, info, positiveSign);
                                    scientific = false;
                                }
                                else
                                {
                                    buffer.Append(ch); // Copy E or e to output
                                    if (src < formatString.Length)
                                    {
                                        if (formatString[src] == '+' || formatString[src] == '-')
                                            buffer.Append(formatString[src++]);
                                        while (src < formatString.Length && formatString[src] == '0')
                                            buffer.Append(formatString[src++]);
                                    }
                                }
                                break;
                            }
                        default:
                            buffer.Append(ch);
                            break;
                    }
                }

                if (number.IsNegative && (section == 0) && (number.Scale == 0) && (buffer.Length > 0))
                    buffer.Insert(0, info.NegativeSign.AsSpan());
            }

            private static void Write(ref NumberBuffer number, ref Buffer buffer, ReadOnlySpan<char> formatString, NumberFormatInfo info, int alignment)
            {
                if (alignment == 0)
                    Write(ref number, ref buffer, formatString, info);
                else
                {
                    // There's no way to know ahead of time how many characters a NumberBuffer will require to enforce alignment so we have to resort to a temporary
                    var temporary = new Buffer(number.DigitsCount);
                    try
                    {
                        Write(ref number, ref temporary, formatString, info);

                        var n = temporary.Length;
                        if (alignment > n)
                            buffer.Append(' ', alignment - n);

                        buffer.Append(temporary);

                        if (alignment < -n)
                            buffer.Append(' ', -alignment - n);
                    }
                    finally
                    {
                        temporary.Dispose();
                    }
                }
            }



            private static void Write(int value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int aligment)
            {
                var specifier = ParseFormatSpecifier(formatString, out int minDigits);
                var upper = (char)(specifier & 0xFFDF); // upper-case specifier for a single comparison

                if ((upper == 'G' && minDigits < 1) || upper == 'D')
                {
                    Write(value, ref buffer, NumberFormatInfo.GetInstance(formatProvider), minDigits, aligment);
                    return;
                }

                // (specifier - ('X' - 'A' + 10)) returns either 'a' - 10 or 'A' - 10 and has the effect of dictating whether we produce 
                // uppercase or lowercase hex numbers for digits a-f. 'X' as the specifier produces uppercase while 'x' as the specifier 
                // produces lowercase.
                if (upper == 'X')
                {
                    WriteHex((uint)value, ref buffer, (char)(specifier - ('X' - 'A' + 10)), minDigits, aligment);
                    return;
                }

                Span<byte> span = stackalloc byte[UInt32NumberBufferLength];
                var number = new NumberBuffer(span, value);

                if (specifier != 0)
                    Write(ref number, ref buffer, specifier, minDigits, NumberFormatInfo.GetInstance(formatProvider), aligment);
                else
                    Write(ref number, ref buffer, formatString, NumberFormatInfo.GetInstance(formatProvider), aligment);
            }

            private static void Write(uint value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int aligment)
            {
                var specifier = ParseFormatSpecifier(formatString, out int minDigits);
                var upper = (char)(specifier & 0xFFDF); // upper-case specifier for a single comparison

                if ((upper == 'G' && minDigits < 1) || upper == 'D')
                {
                    Write(value, ref buffer, minDigits, aligment);
                    return;
                }

                // (specifier - ('X' - 'A' + 10)) returns either 'a' - 10 or 'A' - 10 and has the effect of dictating whether we produce 
                // uppercase or lowercase hex numbers for digits a-f. 'X' as the specifier produces uppercase while 'x' as the specifier 
                // produces lowercase.
                if (upper == 'X')
                {
                    WriteHex(value, ref buffer, (char)(specifier - ('X' - 'A' + 10)), minDigits, aligment);
                    return;
                }

                Span<byte> span = stackalloc byte[UInt32NumberBufferLength];
                var number = new NumberBuffer(span, value);

                if (specifier != 0)
                    Write(ref number, ref buffer, specifier, minDigits, NumberFormatInfo.GetInstance(formatProvider), aligment);
                else
                    Write(ref number, ref buffer, formatString, NumberFormatInfo.GetInstance(formatProvider), aligment);
            }

            private static void Write(long value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int aligment)
            {
                var specifier = ParseFormatSpecifier(formatString, out int minDigits);
                var upper = (char)(specifier & 0xFFDF); // upper-case specifier for a single comparison

                if ((upper == 'G' && minDigits < 1) || upper == 'D')
                {
                    Write(value, ref buffer, NumberFormatInfo.GetInstance(formatProvider), minDigits, aligment);
                    return;
                }

                // (specifier - ('X' - 'A' + 10)) returns either 'a' - 10 or 'A' - 10 and has the effect of dictating whether we produce 
                // uppercase or lowercase hex numbers for digits a-f. 'X' as the specifier produces uppercase while 'x' as the specifier 
                // produces lowercase.
                if (upper == 'X')
                {
                    WriteHex((ulong)value, ref buffer, (char)(specifier - ('X' - 'A' + 10)), minDigits, aligment);
                    return;
                }

                Span<byte> span = stackalloc byte[UInt32NumberBufferLength];
                var number = new NumberBuffer(span, value);

                if (specifier != 0)
                    Write(ref number, ref buffer, specifier, minDigits, NumberFormatInfo.GetInstance(formatProvider), aligment);
                else
                    Write(ref number, ref buffer, formatString, NumberFormatInfo.GetInstance(formatProvider), aligment);
            }

            private static void Write(ulong value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int aligment)
            {
                var specifier = ParseFormatSpecifier(formatString, out int minDigits);
                var upper = (char)(specifier & 0xFFDF); // upper-case specifier for a single comparison

                if ((upper == 'G' && minDigits < 1) || upper == 'D')
                {
                    Write(value, ref buffer, minDigits, aligment);
                    return;
                }

                // (specifier - ('X' - 'A' + 10)) returns either 'a' - 10 or 'A' - 10 and has the effect of dictating whether we produce 
                // uppercase or lowercase hex numbers for digits a-f. 'X' as the specifier produces uppercase while 'x' as the specifier 
                // produces lowercase.
                if (upper == 'X')
                {
                    WriteHex(value, ref buffer, (char)(specifier - ('X' - 'A' + 10)), minDigits, aligment);
                    return;
                }

                Span<byte> span = stackalloc byte[UInt32NumberBufferLength];
                var number = new NumberBuffer(span, value);

                if (specifier != 0)
                    Write(ref number, ref buffer, specifier, minDigits, NumberFormatInfo.GetInstance(formatProvider), aligment);
                else
                    Write(ref number, ref buffer, formatString, NumberFormatInfo.GetInstance(formatProvider), aligment);
            }

            private static int GetFloatingPointMaxDigitsAndPrecision(char specifier, ref int precision, NumberFormatInfo info, out bool isSignificantDigits)
            {
                if (specifier == '\0')
                {
                    isSignificantDigits = true;
                    return precision;
                }

                var maxDigits = precision;

                switch (specifier & 0xFFDF)
                {
                    case 'C':
                        // The currency format uses the precision specifier to indicate the number of
                        // decimal digits to format. This defaults to NumberFormatInfo.CurrencyDecimalDigits.
                        if (precision == -1)
                            precision = info.CurrencyDecimalDigits;

                        isSignificantDigits = false;
                        break;
                    case 'E':
                        // The exponential format uses the precision specifier to indicate the number of
                        // decimal digits to format. This defaults to 6. However, the exponential format
                        // also always formats a single integral digit, so we need to increase the precision
                        // specifier and treat it as the number of significant digits to account for this.
                        if (precision == -1)
                            precision = DefaultPrecisionForExponentialFormat;

                        precision++;
                        isSignificantDigits = true;
                        break;
                    case 'F':
                    case 'N':
                        // The fixed-point and number formats use the precision specifier to indicate the number
                        // of decimal digits to format. This defaults to NumberFormatInfo.NumberDecimalDigits.
                        if (precision == -1)
                            precision = info.NumberDecimalDigits;

                        isSignificantDigits = false;
                        break;
                    case 'G':
                        // The general format uses the precision specifier to indicate the number of significant
                        // digits to format. This defaults to the shortest roundtrippable string. Additionally,
                        // given that we can't return zero significant digits, we treat 0 as returning the shortest
                        // roundtrippable string as well.
                        if (precision == 0)
                            precision = -1;

                        isSignificantDigits = true;
                        break;
                    case 'P':
                        // The percent format uses the precision specifier to indicate the number of
                        // decimal digits to format. This defaults to NumberFormatInfo.PercentDecimalDigits.
                        // However, the percent format also always multiplies the number by 100, so we need
                        // to increase the precision specifier to ensure we get the appropriate number of digits.
                        if (precision == -1)
                            precision = info.PercentDecimalDigits;

                        precision += 2;
                        isSignificantDigits = false;
                        break;
                    case 'R':
                        // The roundtrip format ignores the precision specifier and always returns the shortest
                        // roundtrippable string.
                        precision = -1;
                        isSignificantDigits = true;
                        break;
                    default:
                        throw new FormatException(string.Format(Resources.GetString(Strings.InvalidFormatSpecifier), specifier));
                }

                return maxDigits;
            }

            private static void Write(float value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                var specifier = ParseFormatSpecifier(formatString, out int precision);

                // For back-compat we currently specially treat the precision for custom
                // format specifiers. The constant has more details as to why.
                if (specifier == '\0')
                    precision = SinglePrecisionForCustomFormat;
               
                var info = NumberFormatInfo.GetInstance(formatProvider);

                // We need to track the original precision requested since some formats
                // accept values like 0 and others may require additional fixups.
                var maxDigits = GetFloatingPointMaxDigitsAndPrecision(specifier, ref precision, info, out bool isSignificantDigits);
                
                Span<byte> span = stackalloc byte[SingleNumberBufferLength];
                var number = new NumberBuffer(span, value, precision, isSignificantDigits);
                if (number.IsNaN)
                {
                    Write(info.NaNSymbol, ref buffer, alignment);
                    return;
                }

                if (number.IsInfinity)
                {
                    if (number.IsNegative)
                        Write(info.NegativeInfinitySymbol, ref buffer, alignment);
                    else
                        Write(info.PositiveInfinitySymbol, ref buffer, alignment);

                    return;
                }

                if (specifier == '\0')
                    Write(ref number, ref buffer, formatString, NumberFormatInfo.GetInstance(formatProvider), alignment);
                else
                {
                    // For the roundtrip and general format specifiers, when returning the shortest roundtrippable
                    // string, we need to update the maximum number of digits to be the greater of number.DigitsCount
                    // or SinglePrecision. This ensures that we continue returning "pretty" strings for values with
                    // less digits. One example this fixes is "-60", which would otherwise be formatted as "-6E+01"
                    // since Digits count would be 1 and the formatter would almost immediately switch to scientific notation.
                    if (precision == -1)                        
                        maxDigits = Math.Max(number.DigitsCount, SingleRoundTripPrecision);

                    Write(ref number, ref buffer, specifier, maxDigits, NumberFormatInfo.GetInstance(formatProvider), alignment);
                }                    
            }

            private static void Write(double value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                var specifier = ParseFormatSpecifier(formatString, out int precision);

                // For back-compat we currently specially treat the precision for custom
                // format specifiers. The constant has more details as to why.
                if (specifier == '\0')
                    precision = DoublePrecisionForCustomFormat;

                var info = NumberFormatInfo.GetInstance(formatProvider);

                // We need to track the original precision requested since some formats
                // accept values like 0 and others may require additional fixups.
                var maxDigits = GetFloatingPointMaxDigitsAndPrecision(specifier, ref precision, info, out bool isSignificantDigits);

                Span<byte> span = stackalloc byte[DoubleNumberBufferLength];
                var number = new NumberBuffer(span, value, precision, isSignificantDigits);
                if (number.IsNaN)
                {
                    Write(info.NaNSymbol, ref buffer, alignment);
                    return;
                }

                if (number.IsInfinity)
                {
                    if (number.IsNegative)
                        Write(info.NegativeInfinitySymbol, ref buffer, alignment);
                    else
                        Write(info.PositiveInfinitySymbol, ref buffer, alignment);

                    return;
                }


                if (specifier == '\0')
                    Write(ref number, ref buffer, formatString, NumberFormatInfo.GetInstance(formatProvider), alignment);
                else
                {
                    // For the roundtrip and general format specifiers, when returning the shortest roundtrippable
                    // string, we need to update the maximum number of digits to be the greater of number.DigitsCount
                    // or DoublePrecision. This ensures that we continue returning "pretty" strings for values with
                    // less digits. One example this fixes is "-60", which would otherwise be formatted as "-6E+01"
                    // since Digits count would be 1 and the formatter would almost immediately switch to scientific notation.
                    if (precision == -1)
                        maxDigits = Math.Max(number.DigitsCount, DoubleRoundTripPrecision);

                    Write(ref number, ref buffer, specifier, maxDigits, NumberFormatInfo.GetInstance(formatProvider), alignment);
                }
            }

            private static void Write(decimal value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int aligment)
            {
                var specifier = ParseFormatSpecifier(formatString, out int decimalPlaces);

                Span<byte> span = stackalloc byte[DecimalNumberBufferLength];
                var number = new NumberBuffer(span, value);

                if (specifier == '\0')
                    Write(ref number, ref buffer, formatString, NumberFormatInfo.GetInstance(formatProvider), aligment);
                else
                    Write(ref number, ref buffer, specifier, decimalPlaces, NumberFormatInfo.GetInstance(formatProvider), aligment);
            }


            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(sbyte value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                if (formatString.IsEmpty)
                    Write(value, ref buffer, NumberFormatInfo.GetInstance(formatProvider ?? CultureInfo.CurrentCulture), 0, alignment);
                else
                    Write(value, ref buffer, formatProvider, formatString, alignment);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(short value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                if (formatString.IsEmpty)
                    Write(value, ref buffer, NumberFormatInfo.GetInstance(formatProvider ?? CultureInfo.CurrentCulture), 0, alignment);
                else
                    Write(value, ref buffer, formatProvider, formatString, alignment);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(int value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                if (formatString.IsEmpty)
                    Write(value, ref buffer, NumberFormatInfo.GetInstance(formatProvider ?? CultureInfo.CurrentCulture), 0, alignment);
                else
                    Write(value, ref buffer, formatProvider, formatString, alignment);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(long value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                if (formatString.IsEmpty)
                    Write(value, ref buffer, NumberFormatInfo.GetInstance(formatProvider ?? CultureInfo.CurrentCulture), 0, alignment);
                else
                    Write(value, ref buffer, formatProvider, formatString, alignment);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(byte value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                if (formatString.IsEmpty)
                    Write(value, ref buffer, 0, alignment);
                else 
                    Write(value, ref buffer, formatProvider, formatString, alignment);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(ushort value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                if (formatString.IsEmpty)
                    Write(value, ref buffer, 0, alignment);
                else
                    Write(value, ref buffer, formatProvider, formatString, alignment);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(uint value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                if (formatString.IsEmpty)
                    Write(value, ref buffer, 0, alignment);
                else
                    Write(value, ref buffer, formatProvider, formatString, alignment);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(ulong value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                if (formatString.IsEmpty)
                    Write(value, ref buffer, 0, alignment);
                else
                    Write(value, ref buffer, formatProvider, formatString, alignment);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(sbyte? value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                if (value.HasValue)
                    Format(value.Value, ref buffer, formatProvider, formatString, alignment);
                else if (alignment != 0)
                    Write(Span<char>.Empty, ref buffer, alignment);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(short? value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                if (value.HasValue)
                    Format(value.Value, ref buffer, formatProvider, formatString, alignment);
                else if (alignment != 0)
                    Write(Span<char>.Empty, ref buffer, alignment);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(int? value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                if (value.HasValue)
                    Format(value.Value, ref buffer, formatProvider, formatString, alignment);
                else if (alignment != 0)
                    Write(Span<char>.Empty, ref buffer, alignment);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(long? value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                if (value.HasValue)
                    Format(value.Value, ref buffer, formatProvider, formatString, alignment);
                else if (alignment != 0)
                    Write(Span<char>.Empty, ref buffer, alignment);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(byte? value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                if (value.HasValue)
                    Format(value.Value, ref buffer, formatProvider, formatString, alignment);
                else if (alignment != 0)
                    Write(Span<char>.Empty, ref buffer, alignment);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(ushort? value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                if (value.HasValue)
                    Format(value.Value, ref buffer, formatProvider, formatString, alignment);
                else if (alignment != 0)
                    Write(Span<char>.Empty, ref buffer, alignment);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(uint? value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                if (value.HasValue)
                    Format(value.Value, ref buffer, formatProvider, formatString, alignment);
                else if (alignment != 0)
                    Write(Span<char>.Empty, ref buffer, alignment);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(ulong? value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                if (value.HasValue)
                    Format(value.Value, ref buffer, formatProvider, formatString, alignment);
                else if (alignment != 0)
                    Write(Span<char>.Empty, ref buffer, alignment);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(float value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment) 
                => Write(value, ref buffer, formatProvider, formatString, alignment);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(double value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
                => Write(value, ref buffer, formatProvider, formatString, alignment);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(decimal value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
                => Write(value, ref buffer, formatProvider, formatString, alignment);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(float? value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                if (value.HasValue)
                    Format(value.Value, ref buffer, formatProvider, formatString, alignment);
                else if (alignment != 0)
                    Write(Span<char>.Empty, ref buffer, alignment);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(double? value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                if (value.HasValue)
                    Format(value.Value, ref buffer, formatProvider, formatString, alignment);
                else if (alignment != 0)
                    Write(Span<char>.Empty, ref buffer, alignment);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(decimal? value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                if (value.HasValue)
                    Format(value.Value, ref buffer, formatProvider, formatString, alignment);
                else if (alignment != 0)
                    Write(Span<char>.Empty, ref buffer, alignment);
            }


            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(bool value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
                => Write(value ? bool.TrueString : bool.FalseString, ref buffer, alignment);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(TimeSpan value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
                => Write(formatString.IsEmpty ? value.ToString() : value.ToString(formatString.ToString(), formatProvider), ref buffer, alignment );

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(DateTime value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
                => Write(formatString.IsEmpty ? value.ToString() : value.ToString(formatString.ToString(), formatProvider), ref buffer, alignment);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(DateTimeOffset value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
                => Write(formatString.IsEmpty ? value.ToString() : value.ToString(formatString.ToString(), formatProvider), ref buffer, alignment);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(Guid value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
                => Write(formatString.IsEmpty ? value.ToString() : value.ToString(formatString.ToString(), formatProvider), ref buffer, alignment);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(bool? value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                if (value.HasValue)
                    Format(value.Value, ref buffer, formatProvider, formatString, alignment);
                else if (alignment != 0)
                    Write(Span<char>.Empty, ref buffer, alignment);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(TimeSpan? value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                if (value.HasValue)
                    Format(value.Value, ref buffer, formatProvider, formatString, alignment);
                else if (alignment != 0)
                    Write(Span<char>.Empty, ref buffer, alignment);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(DateTime? value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                if (value.HasValue)
                    Format(value.Value, ref buffer, formatProvider, formatString, alignment);
                else if (alignment != 0)
                    Write(Span<char>.Empty, ref buffer, alignment);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(DateTimeOffset? value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                if (value.HasValue)
                    Format(value.Value, ref buffer, formatProvider, formatString, alignment);
                else if (alignment != 0)
                    Write(Span<char>.Empty, ref buffer, alignment);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(Guid? value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                if (value.HasValue)
                    Format(value.Value, ref buffer, formatProvider, formatString, alignment);
                else if (alignment != 0)
                    Write(Span<char>.Empty, ref buffer, alignment);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(IntPtr value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                if (IntPtr.Size == sizeof(int))
                    Format(value.ToInt32(), ref buffer, formatProvider, formatString, alignment);
                else
                    Format(value.ToInt64(), ref buffer, formatProvider, formatString, alignment);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(UIntPtr value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                if (IntPtr.Size == sizeof(uint))
                    Format(value.ToUInt32(), ref buffer, formatProvider, formatString, alignment);
                else
                    Format(value.ToUInt64(), ref buffer, formatProvider, formatString, alignment);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(string value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment) 
                => Write(value ?? string.Empty, ref buffer, alignment);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(char value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
                => Write(value, ref buffer, alignment);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(char? value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                if (value != null)
                    Write(value.Value, ref buffer, alignment);
                else
                    Write(Span<char>.Empty, ref buffer, alignment);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(char[] value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
                => Write(value ?? Array.Empty<char>(), ref buffer, alignment);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(StringBuilder value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                if (value != null)
                    Write(value.AsSpan(), ref buffer, alignment);
                else 
                    Write(Span<char>.Empty, ref buffer, alignment);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(Buffer value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
                => Write(value.AsSpan(), ref buffer, alignment);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format(Buffer? value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                if (value.HasValue)
                    Write(value.Value.AsSpan(), ref buffer, alignment);
                else if (alignment != 0)
                    Write(Span<char>.Empty, ref buffer, alignment);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Format<T>(T value, ref Buffer buffer, IFormatProvider formatProvider, ReadOnlySpan<char> formatString, int alignment)
            {
                switch (value)
                {
                    case IFormattableToStringBuilder stringBuilderFormattable:
                        stringBuilderFormattable.FormatInto(ref buffer, formatProvider, formatString, alignment);
                        break;
                    case IFormattable formattable:
                        Write(formattable.ToString(formatString.ToString(), formatProvider), ref buffer, alignment);
                        break;
                    default:
                        Write(value?.ToString() ?? string.Empty, ref buffer, alignment);
                        break;
                }
            }

            private ref partial struct NumberBuffer
            {
                private const int ScaleNaN = unchecked((int)0x80000000);
                private const int ScaleInf = 0x7FFFFFFF;

                public NumberBufferKind Kind;
                public Span<byte> Digits;
                public int DigitsCount;
                public int Scale;
                public bool IsNegative;                                

                public bool IsNaN => Scale == ScaleNaN;

                public bool IsInfinity => Scale == ScaleInf;

                public NumberBuffer(Span<byte> buffer, uint value, bool isNegative = false, int precision = UInt32Precision)
                {
                    Kind = NumberBufferKind.Integer;
                    IsNegative = isNegative;

                    var required = Digits(value);
                    var length = Math.Min(required, buffer.Length - 1);
                    Scale = length;
                    buffer[length] = (byte)'\0';
                    Digits = buffer;
                    DigitsCount = length;
                    var i = length - 1;
                    do
                    {
                        value = MathEx.DivRem(value, 10, out var digit);
                        buffer[i--] = (byte)('0' + digit);
                    } while (value > 0);
                }

                public NumberBuffer(Span<byte> buffer, int value) : this(buffer, (uint)(value < 0 ? -value : value), value < 0, Int32Precision) { }

                public NumberBuffer(Span<byte> buffer, ulong value, bool isNegative = false, int precision = UInt64Precision)
                {
                    Kind = NumberBufferKind.Integer;
                    Digits = buffer;
                    IsNegative = isNegative;

                    var required = Digits(value);
                    var length = Math.Min(required, buffer.Length - 1);
                    Scale = length;
                    buffer[length] = (byte)'\0';
                    Digits = buffer;
                    DigitsCount = length;
                    var i = length - 1;
                    do
                    {
                        value = MathEx.DivRem(value, 10, out var digit);
                        buffer[i--] = (byte)('0' + digit);
                    } while (value > 0);
                }

                public NumberBuffer(Span<byte> buffer, long value) : this(buffer, (ulong)(value < 0 ? -value : value), value < 0, Int64Precision) { }

                public NumberBuffer(Span<byte> buffer, float value, int precision, bool isSignificantDigits)
                {
                    Kind = NumberBufferKind.FloatingPoint;
                    Decompose(value, out bool isNegative, out int exp, out long mantissa);
                    if (exp == 0x7FF)
                    {
                        // Special value handling (infinity and NaNs)
                        Scale = (mantissa != 0) ? ScaleNaN : ScaleInf;
                        IsNegative = isNegative;
                        buffer[0] = (byte)'\0';
                        Digits = buffer;
                        DigitsCount = 0;
                    }
                    else
                    {
                        Scale = default;
                        IsNegative = isNegative;
                        buffer[0] = (byte)'\0';
                        Digits = buffer;
                        DigitsCount = 0;

                        if (isNegative)
                            value = -value;

                        if (value == 0.0f)
                            return;

                        if (!isSignificantDigits || !Grisu3.TryRun(value, precision, ref this))
                            Dragon4(value, precision, isSignificantDigits, ref this);
                    }
                }

                public NumberBuffer(Span<byte> buffer, double value, int precision, bool isSignificantDigits)
                {
                    Kind = NumberBufferKind.FloatingPoint;
                    Decompose(value, out bool isNegative, out int exp, out long mantissa);
                    if (exp == 0x7FF)
                    {
                        // Special value handling (infinity and NaNs)
                        Scale = (mantissa != 0) ? ScaleNaN : ScaleInf;
                        IsNegative = isNegative;
                        buffer[0] = (byte)'\0';
                        Digits = buffer;
                        DigitsCount = 0;
                    }
                    else
                    {
                        Scale = default;
                        IsNegative = isNegative;
                        buffer[0] = (byte)'\0';
                        Digits = buffer;
                        DigitsCount = 0;

                        if (isNegative)
                            value = -value;

                        if (value == 0.0)
                            return;

                        if (!isSignificantDigits || !Grisu3.TryRun(value, precision, ref this))
                            Dragon4(value, precision, isSignificantDigits, ref this);
                    }
                }

                public NumberBuffer(Span<byte> buffer, decimal value)
                {
                    Kind = NumberBufferKind.Decimal;
                    IsNegative = value.IsNegative();

                    var p = DecimalPrecision;
                    while ((value.Mid() | value.High()) != 0)
                        p = IntToDecStringRightToLeft(value.DecDivMod1E9(), buffer, p, 9);

                    p = IntToDecStringRightToLeft(value.Low(), buffer, p, 0);
                    if (p > 0)
                        buffer.Slice(p).CopyTo(buffer);

                    var length = DecimalPrecision - p;
                    Scale = length - value.Scale();
                    buffer[length] = (byte)'\0';
                    Digits = buffer;
                    DigitsCount = length;
                }

                public override string ToString() => Encoding.ASCII.GetString(Digits.Slice(0, DigitsCount).ToArray());

                public void Round(int pos, bool isCorrectlyRounded)
                {
                    var i = 0;
                    var dig = Digits;
                    while (i < pos && dig[i] != '\0')
                        i++;

                    // We only want to round up if the digit is greater than or equal to 5 and we are
                    // not rounding a floating-point number. If we are rounding a floating-point number
                    // we have one of two cases.
                    //
                    // In the case of a standard numeric-format specifier, the exact and correctly rounded
                    // string will have been produced. In this scenario, pos will have pointed to the
                    // terminating null for the buffer and so this will return false.
                    //
                    // However, in the case of a custom numeric-format specifier, we currently fall back
                    // to generating Single/DoublePrecisionCustomFormat digits and then rely on this
                    // function to round correctly instead. This can unfortunately lead to double-rounding
                    // bugs but is the best we have right now due to back-compat concerns.
                    //
                    // Values greater than or equal to 5 should round up, otherwise we round down. The IEEE
                    // 754 spec actually dictates that ties (exactly 5) should round to the nearest even number
                    // but that can have undesired behavior for custom numeric format strings. This probably
                    // needs further thought for .NET 5 so that we can be spec compliant and so that users
                    // can get the desired rounding behavior for their needs.

                    if ((i == pos) && (dig[i] != '\0' && !isCorrectlyRounded && dig[i] >= '5'))
                    {
                        while (i > 0 && dig[i - 1] == '9')
                            i--;

                        if (i > 0)
                        {
                            dig[i - 1]++;
                        }
                        else
                        {
                            Scale++;
                            dig[0] = (byte)('1');
                            i = 1;
                        }
                    }
                    else
                    {
                        while (i > 0 && dig[i - 1] == '0')
                            i--;
                    }

                    if (i == 0)
                    {
                        // The integer types don't have a concept of -0 and decimal always format -0 as 0
                        if (Kind != NumberBufferKind.FloatingPoint)
                            IsNegative = false;

                        Scale = 0; // Decimals with scale ('0.00') should be rounded.
                    }

                    dig[i] = (byte)'\0';
                    DigitsCount = i;
                }

                private static int IntToDecStringRightToLeft(uint value, Span<byte> destination, int destinationLength, int minLength)
                {
                    while (destinationLength > 0 && (value > 0 || minLength > 0))
                    {
                        value = MathEx.DivRem(value, 10, out var digit);
                        destination[--destinationLength] = (byte)('0' + digit);
                        minLength--;
                    }

                    return destinationLength;
                }

                private static void Decompose(double value, out bool isNegative, out int exp, out long mantissa)
                {
                    var bits = new Converter.Double { AsDouble = value }.AsInt64;
                    if (BitConverter.IsLittleEndian)
                    {
                        mantissa = bits & 0x000FFFFFFFFFFFFF;           // bits 0 - 51
                        exp = (int)(bits >> 52) & 0x7FF;                // bits 52 - 62
                        isNegative = (bits & (1L << 63)) != 0;          // bit 63
                    }
                    else
                    {
                        mantissa = (bits >> 12) & 0x000FFFFFFFFFFFFF;   // bits 12 - 63
                        exp = (int)(bits >> 1) & 0x7FF;                 // bits 1 - 11
                        isNegative = (uint)(bits & 0x1) != 0;           // bit 0
                    }
                }

                private static ulong ExtractFractionAndBiasedExponent(double value, out int exponent)
                {
                    ulong bits = (ulong)new Converter.Double { AsDouble = value }.AsInt64;
                    ulong fraction = (bits & 0xFFFFFFFFFFFFF);
                    exponent = ((int)(bits >> 52) & 0x7FF);

                    if (exponent != 0)
                    {
                        // For normalized value, according to https://en.wikipedia.org/wiki/Double-precision_floating-point_format
                        // value = 1.fraction * 2^(exp - 1023)
                        //       = (1 + mantissa / 2^52) * 2^(exp - 1023)
                        //       = (2^52 + mantissa) * 2^(exp - 1023 - 52)
                        //
                        // So f = (2^52 + mantissa), e = exp - 1075;

                        fraction |= (1UL << 52);
                        exponent -= 1075;
                    }
                    else
                    {
                        // For denormalized value, according to https://en.wikipedia.org/wiki/Double-precision_floating-point_format
                        // value = 0.fraction * 2^(1 - 1023)
                        //       = (mantissa / 2^52) * 2^(-1022)
                        //       = mantissa * 2^(-1022 - 52)
                        //       = mantissa * 2^(-1074)
                        // So f = mantissa, e = -1074
                        exponent = -1074;
                    }

                    return fraction;
                }

                private static uint ExtractFractionAndBiasedExponent(float value, out int exponent)
                {
                    uint bits = (uint)new Converter.Single { AsFloat = value }.AsInt32;
                    uint fraction = (bits & 0x7FFFFF);
                    exponent = ((int)(bits >> 23) & 0xFF);

                    if (exponent != 0)
                    {
                        // For normalized value, according to https://en.wikipedia.org/wiki/Single-precision_floating-point_format
                        // value = 1.fraction * 2^(exp - 127)
                        //       = (1 + mantissa / 2^23) * 2^(exp - 127)
                        //       = (2^23 + mantissa) * 2^(exp - 127 - 23)
                        //
                        // So f = (2^23 + mantissa), e = exp - 150;

                        fraction |= (1U << 23);
                        exponent -= 150;
                    }
                    else
                    {
                        // For denormalized value, according to https://en.wikipedia.org/wiki/Single-precision_floating-point_format
                        // value = 0.fraction * 2^(1 - 127)
                        //       = (mantissa / 2^23) * 2^(-126)
                        //       = mantissa * 2^(-126 - 23)
                        //       = mantissa * 2^(-149)
                        // So f = mantissa, e = -149
                        exponent = -149;
                    }

                    return fraction;
                }

                #region Grisu3 

                // Based on https://github.com/dotnet/runtime/blob/master/src/libraries/System.Private.CoreLib/src/System/Number.Grisu3.cs
                // Licensed to the .NET Foundation under one or more agreements and published by the .NET Foundation under the MIT license.
                //
                // This is a port of the `Grisu3` implementation here: https://github.com/google/double-conversion/blob/a711666ddd063eb1e4b181a6cb981d39a1fc8bac/double-conversion/fast-dtoa.cc
                // The backing algorithm and the proofs behind it are described in more detail here: http://www.cs.tufts.edu/~nr/cs257/archive/florian-loitsch/printf.pdf
                // ========================================================================================================================================
                //
                // Overview:
                //
                // The general idea behind Grisu3 is to leverage additional bits and cached powers of ten to generate the correct digits.
                // The algorithm is imprecise for some numbers. Fortunately, the algorithm itself can determine this scenario and gives us
                // a result indicating success or failure. We must fallback to a different algorithm for the failing scenario.
                private static class Grisu3
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    private static bool IsNegative(double d) => BitConverter.DoubleToInt64Bits(d) < 0;

                    private const int CachedPowersDecimalExponentDistance = 8;
                    private const int CachedPowersMinDecimalExponent = -348;
                    private const int CachedPowersPowerMaxDecimalExponent = 340;
                    private const int CachedPowersOffset = -CachedPowersMinDecimalExponent;

                    // 1 / Log2(10)
                    private const double D1Log210 = 0.301029995663981195;

                    // The minimal and maximal target exponents define the range of w's binary exponent,
                    // where w is the result of multiplying the input by a cached power of ten.
                    //
                    // A different range might be chosen on a different platform, to optimize digit generation,
                    // but a smaller range requires more powers of ten to be cached.
                    private const int MaximalTargetExponent = -32;
                    private const int MinimalTargetExponent = -60;

                    private static readonly short[] s_CachedPowersBinaryExponent = new short[]
                    {
                        -1220,
                        -1193,
                        -1166,
                        -1140,
                        -1113,
                        -1087,
                        -1060,
                        -1034,
                        -1007,
                        -980,
                        -954,
                        -927,
                        -901,
                        -874,
                        -847,
                        -821,
                        -794,
                        -768,
                        -741,
                        -715,
                        -688,
                        -661,
                        -635,
                        -608,
                        -582,
                        -555,
                        -529,
                        -502,
                        -475,
                        -449,
                        -422,
                        -396,
                        -369,
                        -343,
                        -316,
                        -289,
                        -263,
                        -236,
                        -210,
                        -183,
                        -157,
                        -130,
                        -103,
                        -77,
                        -50,
                        -24,
                        3,
                        30,
                        56,
                        83,
                        109,
                        136,
                        162,
                        189,
                        216,
                        242,
                        269,
                        295,
                        322,
                        348,
                        375,
                        402,
                        428,
                        455,
                        481,
                        508,
                        534,
                        561,
                        588,
                        614,
                        641,
                        667,
                        694,
                        720,
                        747,
                        774,
                        800,
                        827,
                        853,
                        880,
                        907,
                        933,
                        960,
                        986,
                        1013,
                        1039,
                        1066,
                    };

                    private static readonly short[] s_CachedPowersDecimalExponent = new short[]
                    {
                        CachedPowersMinDecimalExponent,
                        -340,
                        -332,
                        -324,
                        -316,
                        -308,
                        -300,
                        -292,
                        -284,
                        -276,
                        -268,
                        -260,
                        -252,
                        -244,
                        -236,
                        -228,
                        -220,
                        -212,
                        -204,
                        -196,
                        -188,
                        -180,
                        -172,
                        -164,
                        -156,
                        -148,
                        -140,
                        -132,
                        -124,
                        -116,
                        -108,
                        -100,
                        -92,
                        -84,
                        -76,
                        -68,
                        -60,
                        -52,
                        -44,
                        -36,
                        -28,
                        -20,
                        -12,
                        -4,
                        4,
                        12,
                        20,
                        28,
                        36,
                        44,
                        52,
                        60,
                        68,
                        76,
                        84,
                        92,
                        100,
                        108,
                        116,
                        124,
                        132,
                        140,
                        148,
                        156,
                        164,
                        172,
                        180,
                        188,
                        196,
                        204,
                        212,
                        220,
                        228,
                        236,
                        244,
                        252,
                        260,
                        268,
                        276,
                        284,
                        292,
                        300,
                        308,
                        316,
                        324,
                        332,
                        CachedPowersPowerMaxDecimalExponent,
                    };

                    private static readonly ulong[] s_CachedPowersSignificand = new ulong[]
                    {
                        0xFA8FD5A0081C0288,
                        0xBAAEE17FA23EBF76,
                        0x8B16FB203055AC76,
                        0xCF42894A5DCE35EA,
                        0x9A6BB0AA55653B2D,
                        0xE61ACF033D1A45DF,
                        0xAB70FE17C79AC6CA,
                        0xFF77B1FCBEBCDC4F,
                        0xBE5691EF416BD60C,
                        0x8DD01FAD907FFC3C,
                        0xD3515C2831559A83,
                        0x9D71AC8FADA6C9B5,
                        0xEA9C227723EE8BCB,
                        0xAECC49914078536D,
                        0x823C12795DB6CE57,
                        0xC21094364DFB5637,
                        0x9096EA6F3848984F,
                        0xD77485CB25823AC7,
                        0xA086CFCD97BF97F4,
                        0xEF340A98172AACE5,
                        0xB23867FB2A35B28E,
                        0x84C8D4DFD2C63F3B,
                        0xC5DD44271AD3CDBA,
                        0x936B9FCEBB25C996,
                        0xDBAC6C247D62A584,
                        0xA3AB66580D5FDAF6,
                        0xF3E2F893DEC3F126,
                        0xB5B5ADA8AAFF80B8,
                        0x87625F056C7C4A8B,
                        0xC9BCFF6034C13053,
                        0x964E858C91BA2655,
                        0xDFF9772470297EBD,
                        0xA6DFBD9FB8E5B88F,
                        0xF8A95FCF88747D94,
                        0xB94470938FA89BCF,
                        0x8A08F0F8BF0F156B,
                        0xCDB02555653131B6,
                        0x993FE2C6D07B7FAC,
                        0xE45C10C42A2B3B06,
                        0xAA242499697392D3,
                        0xFD87B5F28300CA0E,
                        0xBCE5086492111AEB,
                        0x8CBCCC096F5088CC,
                        0xD1B71758E219652C,
                        0x9C40000000000000,
                        0xE8D4A51000000000,
                        0xAD78EBC5AC620000,
                        0x813F3978F8940984,
                        0xC097CE7BC90715B3,
                        0x8F7E32CE7BEA5C70,
                        0xD5D238A4ABE98068,
                        0x9F4F2726179A2245,
                        0xED63A231D4C4FB27,
                        0xB0DE65388CC8ADA8,
                        0x83C7088E1AAB65DB,
                        0xC45D1DF942711D9A,
                        0x924D692CA61BE758,
                        0xDA01EE641A708DEA,
                        0xA26DA3999AEF774A,
                        0xF209787BB47D6B85,
                        0xB454E4A179DD1877,
                        0x865B86925B9BC5C2,
                        0xC83553C5C8965D3D,
                        0x952AB45CFA97A0B3,
                        0xDE469FBD99A05FE3,
                        0xA59BC234DB398C25,
                        0xF6C69A72A3989F5C,
                        0xB7DCBF5354E9BECE,
                        0x88FCF317F22241E2,
                        0xCC20CE9BD35C78A5,
                        0x98165AF37B2153DF,
                        0xE2A0B5DC971F303A,
                        0xA8D9D1535CE3B396,
                        0xFB9B7CD9A4A7443C,
                        0xBB764C4CA7A44410,
                        0x8BAB8EEFB6409C1A,
                        0xD01FEF10A657842C,
                        0x9B10A4E5E9913129,
                        0xE7109BFBA19C0C9D,
                        0xAC2820D9623BF429,
                        0x80444B5E7AA7CF85,
                        0xBF21E44003ACDD2D,
                        0x8E679C2F5E44FF8F,
                        0xD433179D9C8CB841,
                        0x9E19DB92B4E31BA9,
                        0xEB96BF6EBADF77D9,
                        0xAF87023B9BF0EE6B,
                    };

                    private static readonly uint[] s_SmallPowersOfTen = new uint[]
                    {
                        1,          // 10^0
                        10,         // 10^1
                        100,        // 10^2
                        1000,       // 10^3
                        10000,      // 10^4
                        100000,     // 10^5
                        1000000,    // 10^6
                        10000000,   // 10^7
                        100000000,  // 10^8
                        1000000000, // 10^9
                    };

                    public static bool TryRun(double value, int requestedDigits, ref NumberBuffer number)
                    {
                        Debug.Assert(value > 0);

                        int length;
                        int decimalExponent;
                        bool result;

                        if (requestedDigits == -1)
                        {
                            DiyFp w = DiyFp.CreateAndGetBoundaries(value, out DiyFp boundaryMinus, out DiyFp boundaryPlus).Normalize();
                            result = TryRunShortest(in boundaryMinus, in w, in boundaryPlus, number.Digits, out length, out decimalExponent);
                        }
                        else
                        {
                            DiyFp w = new DiyFp(value).Normalize();
                            result = TryRunCounted(in w, requestedDigits, number.Digits, out length, out decimalExponent);
                        }

                        if (result)
                        {
                            Debug.Assert((requestedDigits == -1) || (length == requestedDigits));

                            number.Scale = length + decimalExponent;
                            number.Digits[length] = (byte)'\0';
                            number.DigitsCount = length;                            
                        }

                        return result;
                    }

                    public static bool TryRun(float value, int requestedDigits, ref NumberBuffer number)
                    {
                        Debug.Assert(value> 0);

                        int length;
                        int decimalExponent;
                        bool result;

                        if (requestedDigits == -1)
                        {
                            DiyFp w = DiyFp.CreateAndGetBoundaries(value, out DiyFp boundaryMinus, out DiyFp boundaryPlus).Normalize();
                            result = TryRunShortest(in boundaryMinus, in w, in boundaryPlus, number.Digits, out length, out decimalExponent);
                        }
                        else
                        {
                            DiyFp w = new DiyFp(value).Normalize();
                            result = TryRunCounted(in w, requestedDigits, number.Digits, out length, out decimalExponent);
                        }

                        if (result)
                        {
                            Debug.Assert((requestedDigits == -1) || (length == requestedDigits));

                            number.Scale = length + decimalExponent;
                            number.Digits[length] = (byte)'\0';
                            number.DigitsCount = length;                            
                        }

                        return result;
                    }

                    // The counted version of Grisu3 only generates requestedDigits number of digits.
                    // This version does not generate the shortest representation, and with enough requested digits 0.1 will at some point print as 0.9999999...
                    // Grisu3 is too imprecise for real halfway cases (1.5 will not work) and therefore the rounding strategy for halfway cases is irrelevant.
                    private static bool TryRunCounted(in DiyFp w, int requestedDigits, Span<byte> buffer, out int length, out int decimalExponent)
                    {
                        Debug.Assert(requestedDigits > 0);

                        int tenMkMinimalBinaryExponent = MinimalTargetExponent - (w.e + DiyFp.SignificandSize);
                        int tenMkMaximalBinaryExponent = MaximalTargetExponent - (w.e + DiyFp.SignificandSize);

                        DiyFp tenMk = GetCachedPowerForBinaryExponentRange(tenMkMinimalBinaryExponent, tenMkMaximalBinaryExponent, out int mk);

                        Debug.Assert(MinimalTargetExponent <= (w.e + tenMk.e + DiyFp.SignificandSize));
                        Debug.Assert(MaximalTargetExponent >= (w.e + tenMk.e + DiyFp.SignificandSize));

                        // Note that tenMk is only an approximation of 10^-k.
                        // A DiyFp only contains a 64-bit significand and tenMk is thus only precise up to 64-bits.

                        // The DiyFp.Multiply procedure rounds its result and tenMk is approximated too.
                        // The variable scaledW (as well as scaledBoundaryMinus/Plus) are now off by a small amount.
                        //
                        // In fact, scaledW - (w * 10^k) < 1ulp (unit in last place) of scaledW.
                        // In other words, let f = scaledW.f and e = scaledW.e, then:
                        //      (f - 1) * 2^e < (w * 10^k) < (f + 1) * 2^e

                        DiyFp scaledW = w.Multiply(in tenMk);

                        // We now have (double)(scaledW * 10^-mk).
                        //
                        // DigitGenCounted will generate the first requestedDigits of scaledW and return together with a kappa such that:
                        //      scaledW ~= buffer * 10^kappa.
                        //
                        // It will not always be exactly the same since DigitGenCounted only produces a limited number of digits.

                        bool result = TryDigitGenCounted(in scaledW, requestedDigits, buffer, out length, out int kappa);
                        decimalExponent = -mk + kappa;
                        return result;
                    }

                    // Provides a decimal representation of v.
                    // Returns true if it succeeds; otherwise, the result cannot be trusted.
                    //
                    // There will be length digits inside the buffer (not null-terminated).
                    // If the function returns true then:
                    //      v == (double)(buffer * 10^decimalExponent)
                    //
                    // The digits in the buffer are the shortest represenation possible (no 0.09999999999999999 instead of 0.1).
                    // The shorter representation will even be chosen if the longer one would be closer to v.
                    //
                    // The last digit will be closest to the actual v.
                    // That is, even if several digits might correctly yield 'v' when read again, the closest will be computed.
                    private static bool TryRunShortest(in DiyFp boundaryMinus, in DiyFp w, in DiyFp boundaryPlus, Span<byte> buffer, out int length, out int decimalExponent)
                    {
                        // boundaryMinus and boundaryPlus are the boundaries between v and its closest floating-point neighbors.
                        // Any number strictly between boundaryMinus and boundaryPlus will round to v when converted to a double.
                        // Grisu3 will never output representations that lie exactly on a boundary.

                        Debug.Assert(boundaryPlus.e == w.e);

                        int tenMkMinimalBinaryExponent = MinimalTargetExponent - (w.e + DiyFp.SignificandSize);
                        int tenMkMaximalBinaryExponent = MaximalTargetExponent - (w.e + DiyFp.SignificandSize);

                        DiyFp tenMk = GetCachedPowerForBinaryExponentRange(tenMkMinimalBinaryExponent, tenMkMaximalBinaryExponent, out int mk);

                        Debug.Assert(MinimalTargetExponent <= (w.e + tenMk.e + DiyFp.SignificandSize));
                        Debug.Assert(MaximalTargetExponent >= (w.e + tenMk.e + DiyFp.SignificandSize));

                        // Note that tenMk is only an approximation of 10^-k.
                        // A DiyFp only contains a 64-bit significan and tenMk is thus only precise up to 64-bits.

                        // The DiyFp.Multiply procedure rounds its result and tenMk is approximated too.
                        // The variable scaledW (as well as scaledBoundaryMinus/Plus) are now off by a small amount.
                        //
                        // In fact, scaledW - (w * 10^k) < 1ulp (unit in last place) of scaledW.
                        // In other words, let f = scaledW.f and e = scaledW.e, then:
                        //      (f - 1) * 2^e < (w * 10^k) < (f + 1) * 2^e

                        DiyFp scaledW = w.Multiply(in tenMk);
                        Debug.Assert(scaledW.e == (boundaryPlus.e + tenMk.e + DiyFp.SignificandSize));

                        // In theory, it would be possible to avoid some recomputations by computing the difference between w
                        // and boundaryMinus/Plus (a power of 2) and to compute scaledBoundaryMinus/Plus by subtracting/adding
                        // from scaledW. However, the code becomes much less readable and the speed enhancements are not terrific.

                        DiyFp scaledBoundaryMinus = boundaryMinus.Multiply(in tenMk);
                        DiyFp scaledBoundaryPlus = boundaryPlus.Multiply(in tenMk);

                        // DigitGen will generate the digits of scaledW. Therefore, we have:
                        //      v == (double)(scaledW * 10^-mk)
                        //
                        // Set decimalExponent == -mk and pass it to DigitGen and if scaledW is not an integer than it will be updated.
                        // For instance, if scaledW == 1.23 then the buffer will be filled with "123" and the decimalExponent will be decreased by 2.

                        bool result = TryDigitGenShortest(in scaledBoundaryMinus, in scaledW, in scaledBoundaryPlus, buffer, out length, out int kappa);
                        decimalExponent = -mk + kappa;
                        return result;
                    }

                    // Returns the biggest power of ten that is less than or equal to the given number.
                    // We furthermore receive the maximum number of bits 'number' has.
                    //
                    // Returns power == 10^(exponent) such that
                    //      power <= number < power * 10
                    // If numberBits == 0, then 0^(0-1) is returned.
                    // The number of bits must be <= 32.
                    //
                    // Preconditions:
                    //      number < (1 << (numberBits + 1))
                    private static uint BiggestPowerTen(uint number, int numberBits, out int exponentPlusOne)
                    {
                        // Inspired by the method for finding an integer log base 10 from here:
                        // http://graphics.stanford.edu/~seander/bithacks.html#IntegerLog10

                        Debug.Assert(number < (1U << (numberBits + 1)));

                        // 1233/4096 is approximately 1/log2(10)
                        int exponentGuess = ((numberBits + 1) * 1233) >> 12;
                        Debug.Assert((uint)(exponentGuess) < s_SmallPowersOfTen.Length);

                        uint power = s_SmallPowersOfTen[exponentGuess];

                        // We don't have any guarantees that 2^numberBits <= number
                        if (number < power)
                        {
                            exponentGuess--;
                            power = s_SmallPowersOfTen[exponentGuess];
                        }

                        exponentPlusOne = exponentGuess + 1;
                        return power;
                    }

                    // Generates (at most) requestedDigits of input number w.
                    //
                    // w is a floating-point number (DiyFp), consisting of a significand and an exponent.
                    // Its exponent is bounded by MinimalTargetExponent and MaximalTargetExponent, hence:
                    //      -60 <= w.e <= -32
                    //
                    // Returns false if it fails, in which case the generated digits in the buffer should not be used.
                    //
                    // Preconditions:
                    //      w is correct up to 1 ulp (unit in last place). That is, its error must be strictly less than a unit of its last digit.
                    //      MinimalTargetExponent <= w.e <= MaximalTargetExponent
                    //
                    // Postconditions:
                    //      Returns false if the procedure fails; otherwise:
                    //      * buffer is not null-terminated, but length contains the number of digits.
                    //      * The representation in buffer is the most precise representation of requestedDigits digits.
                    //      * buffer contains at most requestedDigits digits of w. If there are less than requestedDigits digits then some trailing '0's have been removed.
                    //      * kappa is such that w = buffer * 10^kappa + eps with |eps| < 10^kappa / 2.
                    //
                    // This procedure takes into account the imprecision of its input numbers.
                    // If the precision is not enough to guarantee all the postconditions, then false is returned.
                    // This usually happens rarely, but the failure-rate increases with higher requestedDigits
                    private static bool TryDigitGenCounted(in DiyFp w, int requestedDigits, Span<byte> buffer, out int length, out int kappa)
                    {
                        Debug.Assert(MinimalTargetExponent <= w.e);
                        Debug.Assert(w.e <= MaximalTargetExponent);
                        Debug.Assert(MinimalTargetExponent >= -60);
                        Debug.Assert(MaximalTargetExponent <= -32);

                        // w is assumed to have an error less than 1 unit.
                        // Whenever w is scaled we also scale its error.
                        ulong wError = 1;

                        // We cut the input number into two parts: the integral digits and the fractional digits.
                        // We don't emit any decimal separator, but adapt kapp instead.
                        // For example: instead of writing "1.2", we put "12" into the buffer and increase kappa by 1.
                        var one = new DiyFp(1UL << -w.e, w.e);

                        // Division by one is a shift.
                        uint integrals = (uint)(w.f >> -one.e);

                        // Modulo by one is an and.
                        ulong fractionals = w.f & (one.f - 1);

                        // We deviate from the original algorithm here and do some early checks to determine if we can satisfy requestedDigits.
                        // If we determine that we can't, we exit early and avoid most of the heavy lifting that the algorithm otherwise does.
                        //
                        // When fractionals is zero, we can easily determine if integrals can satisfy requested digits:
                        //      If requestedDigits >= 11, integrals is not able to exhaust the count by itself since 10^(11 -1) > uint.MaxValue >= integrals.
                        //      If integrals < 10^(requestedDigits - 1), integrals cannot exhaust the count.
                        //      Otherwise, integrals might be able to exhaust the count and we need to execute the rest of the code.
                        if ((fractionals == 0) && ((requestedDigits >= 11) || (integrals < s_SmallPowersOfTen[requestedDigits - 1])))
                        {
                            Debug.Assert(buffer[0] == '\0');
                            length = 0;
                            kappa = 0;
                            return false;
                        }

                        uint divisor = BiggestPowerTen(integrals, DiyFp.SignificandSize - (-one.e), out kappa);
                        length = 0;

                        // Loop invariant:
                        //      buffer = w / 10^kappa (integer division)
                        // These invariants hold for the first iteration:
                        //      kappa has been initialized with the divisor exponent + 1
                        //      The divisor is the biggest power of ten that is smaller than integrals
                        while (kappa > 0)
                        {
                            uint digit = MathEx.DivRem(integrals, divisor, out integrals);
                            Debug.Assert(digit <= 9);
                            buffer[length] = (byte)('0' + digit);

                            length++;
                            requestedDigits--;
                            kappa--;

                            // Note that kappa now equals the exponent of the
                            // divisor and that the invariant thus holds again.
                            if (requestedDigits == 0)
                            {
                                break;
                            }

                            divisor /= 10;
                        }

                        if (requestedDigits == 0)
                        {
                            ulong rest = ((ulong)(integrals) << -one.e) + fractionals;
                            return TryRoundWeedCounted(
                                buffer,
                                length,
                                rest,
                                tenKappa: ((ulong)(divisor)) << -one.e,
                                unit: wError,
                                ref kappa
                            );
                        }

                        // The integrals have been generated and we are at the point of the decimal separator.
                        // In the following loop, we simply multiply the remaining digits by 10 and divide by one.
                        // We just need to pay attention to multiply associated data (the unit), too.
                        // Note that the multiplication by 10 does not overflow because:
                        //      w.e >= -60 and thus one.e >= -60

                        Debug.Assert(one.e >= MinimalTargetExponent);
                        Debug.Assert(fractionals < one.f);
                        Debug.Assert((ulong.MaxValue / 10) >= one.f);

                        while ((requestedDigits > 0) && (fractionals > wError))
                        {
                            fractionals *= 10;
                            wError *= 10;

                            // Integer division by one.
                            uint digit = (uint)(fractionals >> -one.e);
                            Debug.Assert(digit <= 9);
                            buffer[length] = (byte)('0' + digit);

                            length++;
                            requestedDigits--;
                            kappa--;

                            // Modulo by one.
                            fractionals &= (one.f - 1);
                        }

                        if (requestedDigits != 0)
                        {
                            buffer[0] = (byte)'\0';
                            length = 0;
                            kappa = 0;
                            return false;
                        }

                        return TryRoundWeedCounted(
                            buffer,
                            length,
                            rest: fractionals,
                            tenKappa: one.f,
                            unit: wError,
                            ref kappa
                        );
                    }

                    // Generates the digits of input number w.
                    //
                    // w is a floating-point number (DiyFp), consisting of a significand and an exponent.
                    // Its exponent is bounded by kMinimalTargetExponent and kMaximalTargetExponent, hence:
                    //      -60 <= w.e() <= -32.
                    //
                    // Returns false if it fails, in which case the generated digits in the buffer should not be used.
                    //
                    // Preconditions:
                    //      low, w and high are correct up to 1 ulp (unit in the last place). That is, their error must be less than a unit of their last digits.
                    //      low.e() == w.e() == high.e()
                    //      low < w < high, and taking into account their error: low~ <= high~
                    //      kMinimalTargetExponent <= w.e() <= kMaximalTargetExponent
                    //
                    // Postconditions:
                    //      Returns false if procedure fails; otherwise:
                    //      * buffer is not null-terminated, but len contains the number of digits.
                    //      * buffer contains the shortest possible decimal digit-sequence such that LOW < buffer * 10^kappa < HIGH, where LOW and HIGH are the correct values of low and high (without their error).
                    //      * If more than one decimal representation gives the minimal number of decimal digits then the one closest to W (where W is the correct value of w) is chosen.
                    //
                    // This procedure takes into account the imprecision of its input numbers.
                    // If the precision is not enough to guarantee all the postconditions then false is returned.
                    // This usually happens rarely (~0.5%).
                    //
                    // Say, for the sake of example, that:
                    //      w.e() == -48, and w.f() == 0x1234567890abcdef
                    //
                    // w's value can be computed by w.f() * 2^w.e()
                    //
                    // We can obtain w's integral digits by simply shifting w.f() by -w.e().
                    //      -> w's integral part is 0x1234
                    //      w's fractional part is therefore 0x567890abcdef.
                    //
                    // Printing w's integral part is easy (simply print 0x1234 in decimal).
                    //
                    // In order to print its fraction we repeatedly multiply the fraction by 10 and get each digit.
                    // For example, the first digit after the point would be computed by
                    //      (0x567890abcdef * 10) >> 48. -> 3
                    //
                    // The whole thing becomes slightly more complicated because we want to stop once we have enough digits.
                    // That is, once the digits inside the buffer represent 'w' we can stop.
                    //
                    // Everything inside the interval low - high represents w.
                    // However we have to pay attention to low, high and w's imprecision.
                    private static bool TryDigitGenShortest(in DiyFp low, in DiyFp w, in DiyFp high, Span<byte> buffer, out int length, out int kappa)
                    {
                        Debug.Assert(low.e == w.e);
                        Debug.Assert(w.e == high.e);

                        Debug.Assert((low.f + 1) <= (high.f - 1));

                        Debug.Assert(MinimalTargetExponent <= w.e);
                        Debug.Assert(w.e <= MaximalTargetExponent);

                        // low, w, and high are imprecise, but by less than one ulp (unit in the last place).
                        //
                        // If we remove (resp. add) 1 ulp from low (resp. high) we are certain that the new numbers
                        // are outside of the interval we want the final representation to lie in.
                        //
                        // Inversely adding (resp. removing) 1 ulp from low (resp. high) would yield numbers that
                        // are certain to lie in the interval. We will use this fact later on.
                        //
                        // We will now start by generating the digits within the uncertain interval.
                        // Later, we will weed out representations that lie outside the safe interval and thus might lie outside the correct interval.

                        ulong unit = 1;

                        var tooLow = new DiyFp(low.f - unit, low.e);
                        var tooHigh = new DiyFp(high.f + unit, high.e);

                        // tooLow and tooHigh are guaranteed to lie outside the interval we want the generated number in.

                        DiyFp unsafeInterval = tooHigh.Subtract(in tooLow);

                        // We now cut the input number into two parts: the integral digits and the fractional digits.
                        // We will not write any decimal separator, but adapt kappa instead.
                        //
                        // Reminder: we are currently computing the digits (Stored inside the buffer) such that:
                        //      tooLow < buffer * 10^kappa < tooHigh
                        //
                        // We use tooHigh for the digitGeneration and stop as soon as possible.
                        // If we stop early, we effectively round down.

                        var one = new DiyFp(1UL << -w.e, w.e);

                        // Division by one is a shift.
                        uint integrals = (uint)(tooHigh.f >> -one.e);

                        // Modulo by one is an and.
                        ulong fractionals = tooHigh.f & (one.f - 1);

                        uint divisor = BiggestPowerTen(integrals, DiyFp.SignificandSize - (-one.e), out kappa);
                        length = 0;

                        // Loop invariant:
                        //      buffer = tooHigh / 10^kappa (integer division)
                        // These invariants hold for the first iteration:
                        //      kappa has been initialized with the divisor exponent + 1
                        //      The divisor is the biggest power of ten that is smaller than integrals
                        while (kappa > 0)
                        {
                            uint digit = MathEx.DivRem(integrals, divisor, out integrals);
                            Debug.Assert(digit <= 9);
                            buffer[length] = (byte)('0' + digit);

                            length++;
                            kappa--;

                            // Note that kappa now equals the exponent of the
                            // divisor and that the invariant thus holds again.

                            ulong rest = ((ulong)(integrals) << -one.e) + fractionals;

                            // Invariant: tooHigh = buffer * 10^kappa + DiyFp(rest, one.e)
                            // Reminder: unsafeInterval.e == one.e

                            if (rest < unsafeInterval.f)
                            {
                                // Rounding down (by not emitting the remaining digits)
                                // yields a number that lies within the unsafe interval

                                return TryRoundWeedShortest(
                                    buffer,
                                    length,
                                    tooHigh.Subtract(w).f,
                                    unsafeInterval.f,
                                    rest,
                                    tenKappa: ((ulong)(divisor)) << -one.e,
                                    unit
                                );
                            }

                            divisor /= 10;
                        }

                        // The integrals have been generated and we are at the point of the decimal separator.
                        // In the following loop, we simply multiply the remaining digits by 10 and divide by one.
                        // We just need to pay attention to multiply associated data (the unit), too.
                        // Note that the multiplication by 10 does not overflow because:
                        //      w.e >= -60 and thus one.e >= -60

                        Debug.Assert(one.e >= MinimalTargetExponent);
                        Debug.Assert(fractionals < one.f);
                        Debug.Assert((ulong.MaxValue / 10) >= one.f);

                        while (true)
                        {
                            fractionals *= 10;
                            unit *= 10;

                            unsafeInterval = new DiyFp(unsafeInterval.f * 10, unsafeInterval.e);

                            // Integer division by one.
                            uint digit = (uint)(fractionals >> -one.e);
                            Debug.Assert(digit <= 9);
                            buffer[length] = (byte)('0' + digit);

                            length++;
                            kappa--;

                            // Modulo by one.
                            fractionals &= (one.f - 1);

                            if (fractionals < unsafeInterval.f)
                            {
                                return TryRoundWeedShortest(
                                    buffer,
                                    length,
                                    tooHigh.Subtract(w).f * unit,
                                    unsafeInterval.f,
                                    rest: fractionals,
                                    tenKappa: one.f,
                                    unit
                                );
                            }
                        }
                    }

                    // Returns a cached power-of-ten with a binary exponent in the range [minExponent; maxExponent] (boundaries included).
                    private static DiyFp GetCachedPowerForBinaryExponentRange(int minExponent, int maxExponent, out int decimalExponent)
                    {
                        Debug.Assert(s_CachedPowersSignificand.Length == s_CachedPowersBinaryExponent.Length);
                        Debug.Assert(s_CachedPowersSignificand.Length == s_CachedPowersDecimalExponent.Length);

                        double k = Math.Ceiling((minExponent + DiyFp.SignificandSize - 1) * D1Log210);
                        int index = ((CachedPowersOffset + (int)(k) - 1) / CachedPowersDecimalExponentDistance) + 1;

                        Debug.Assert((uint)(index) < s_CachedPowersSignificand.Length);

                        Debug.Assert(minExponent <= s_CachedPowersBinaryExponent[index]);
                        Debug.Assert(s_CachedPowersBinaryExponent[index] <= maxExponent);

                        decimalExponent = s_CachedPowersDecimalExponent[index];
                        return new DiyFp(s_CachedPowersSignificand[index], s_CachedPowersBinaryExponent[index]);
                    }

                    // Rounds the buffer upwards if the result is closer to v by possibly adding 1 to the buffer.
                    // If the precision of the calculation is not sufficient to round correctly, return false.
                    //
                    // The rounding might shift the whole buffer, in which case, the kappy is adjusted.
                    // For example "99", kappa = 3 might become "10", kappa = 4.
                    //
                    // If (2 * rest) > tenKappa then the buffer needs to be round up.
                    // rest can have an error of +/- 1 unit.
                    // This function accounts for the imprecision and returns false if the rounding direction cannot be unambiguously determined.
                    //
                    // Preconditions:
                    //      rest < tenKappa
                    private static bool TryRoundWeedCounted(Span<byte> buffer, int length, ulong rest, ulong tenKappa, ulong unit, ref int kappa)
                    {
                        Debug.Assert(rest < tenKappa);

                        // The following tests are done in a specific order to avoid overflows.
                        // They will work correctly with any ulong values of rest < tenKappa and unit.
                        //
                        // If the unit is too big, then we don't know which way to round.
                        // For example, a unit of 50 means that the real number lies within rest +/- 50.
                        // If 10^kappa == 40, then there is no way to tell which way to round.
                        //
                        // Even if unit is just half the size of 10^kappa we are already completely lost.
                        // And after the previous test, we know that the expression will not over/underflow.
                        if ((unit >= tenKappa) || ((tenKappa - unit) <= unit))
                        {
                            return false;
                        }

                        // If 2 * (rest + unit) <= 10^kappa, we can safely round down.
                        if (((tenKappa - rest) > rest) && ((tenKappa - (2 * rest)) >= (2 * unit)))
                        {
                            return true;
                        }

                        // If 2 * (rest - unit) >= 10^kappa, we can safely round up.
                        if ((rest > unit) && (tenKappa <= (rest - unit) || ((tenKappa - (rest - unit)) <= (rest - unit))))
                        {
                            // Increment the last digit recursively until we find a non '9' digit.
                            buffer[length - 1]++;

                            for (int i = (length - 1); i > 0; i--)
                            {
                                if (buffer[i] != ('0' + 10))
                                {
                                    break;
                                }

                                buffer[i] = (byte)('0');
                                buffer[i - 1]++;
                            }

                            // If the first digit is now '0'+10, we had a buffer with all '9's.
                            // With the exception of the first digit, all digits are now '0'.
                            // Simply switch the first digit to '1' and adjust the kappa.
                            // For example, "99" becomes "10" and the power (the kappa) is increased.
                            if (buffer[0] == ('0' + 10))
                            {
                                buffer[0] = (byte)('1');
                                kappa++;
                            }

                            return true;
                        }

                        return false;
                    }

                    // Adjusts the last digit of the generated number and screens out generated solutions that may be inaccurate.
                    // A solution may be inaccurate if it is outside the safe interval or if we cannot provide that it is closer to the input than a neighboring representation of the same length.
                    //
                    // Input:
                    //      buffer containing the digits of tooHigh / 10^kappa
                    //      the buffer's length
                    //      distanceTooHighW == (tooHigh - w).f * unit
                    //      unsafeInterval == (tooHigh - tooLow).f * unit
                    //      rest = (tooHigh - buffer * 10^kapp).f * unit
                    //      tenKappa = 10^kappa * unit
                    //      unit = the common multiplier
                    //
                    // Output:
                    //      Returns true if the buffer is guaranteed to contain the closest representable number to the input.
                    //
                    // Modifies the generated digits in the buffer to approach (round towards) w.
                    private static bool TryRoundWeedShortest(Span<byte> buffer, int length, ulong distanceTooHighW, ulong unsafeInterval, ulong rest, ulong tenKappa, ulong unit)
                    {
                        ulong smallDistance = distanceTooHighW - unit;
                        ulong bigDistance = distanceTooHighW + unit;

                        // Let wLow = tooHigh - bigDistance, and wHigh = tooHigh - smallDistance.
                        //
                        // Note: wLow < w < wHigh
                        //
                        // The real w * unit must lie somewhere inside the interval
                        //      ]w_low; w_high[ (often written as "(w_low; w_high)")

                        // Basically the buffer currently contains a number in the unsafe interval
                        //      ]too_low; too_high[ with too_low < w < too_high
                        //
                        //  tooHigh - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
                        //                    ^v 1 unit            ^      ^                 ^      ^
                        //  boundaryHigh ---------------------     .      .                 .      .
                        //                    ^v 1 unit            .      .                 .      .
                        //  - - - - - - - - - - - - - - - - - - -  +  - - + - - - - - -     .      .
                        //                                         .      .         ^       .      .
                        //                                         .  bigDistance   .       .      .
                        //                                         .      .         .       .    rest
                        //                              smallDistance     .         .       .      .
                        //                                         v      .         .       .      .
                        //  wHigh - - - - - - - - - - - - - - - - - -     .         .       .      .
                        //                    ^v 1 unit                   .         .       .      .
                        //  w ---------------------------------------     .         .       .      .
                        //                    ^v 1 unit                   v         .       .      .
                        //  wLow  - - - - - - - - - - - - - - - - - - - - -         .       .      .
                        //                                                          .       .      v
                        //  buffer -------------------------------------------------+-------+--------
                        //                                                          .       .
                        //                                                  safeInterval    .
                        //                                                          v       .
                        //  - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -     .
                        //                    ^v 1 unit                                     .
                        //  boundaryLow -------------------------                     unsafeInterval
                        //                    ^v 1 unit                                     v
                        //  tooLow  - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
                        //
                        //
                        // Note that the value of buffer could lie anywhere inside the range tooLow to tooHigh.
                        //
                        // boundaryLow, boundaryHigh and w are approximations of the real boundaries and v (the input number).
                        // They are guaranteed to be precise up to one unit.
                        // In fact the error is guaranteed to be strictly less than one unit.
                        //
                        // Anything that lies outside the unsafe interval is guaranteed not to round to v when read again.
                        // Anything that lies inside the safe interval is guaranteed to round to v when read again.
                        //
                        // If the number inside the buffer lies inside the unsafe interval but not inside the safe interval
                        // then we simply do not know and bail out (returning false).
                        //
                        // Similarly we have to take into account the imprecision of 'w' when finding the closest representation of 'w'.
                        // If we have two potential representations, and one is closer to both wLow and wHigh, then we know it is closer to the actual value v.
                        //
                        // By generating the digits of tooHigh we got the largest (closest to tooHigh) buffer that is still in the unsafe interval.
                        // In the case where wHigh < buffer < tooHigh we try to decrement the buffer.
                        // This way the buffer approaches (rounds towards) w.
                        //
                        // There are 3 conditions that stop the decrementation process:
                        //   1) the buffer is already below wHigh
                        //   2) decrementing the buffer would make it leave the unsafe interval
                        //   3) decrementing the buffer would yield a number below wHigh and farther away than the current number.
                        //
                        // In other words:
                        //      (buffer{-1} < wHigh) && wHigh - buffer{-1} > buffer - wHigh
                        //
                        // Instead of using the buffer directly we use its distance to tooHigh.
                        //
                        // Conceptually rest ~= tooHigh - buffer
                        //
                        // We need to do the following tests in this order to avoid over- and underflows.

                        Debug.Assert(rest <= unsafeInterval);

                        while ((rest < smallDistance) && ((unsafeInterval - rest) >= tenKappa) && (((rest + tenKappa) < smallDistance) || ((smallDistance - rest) >= (rest + tenKappa - smallDistance))))
                        {
                            buffer[length - 1]--;
                            rest += tenKappa;
                        }

                        // We have approached w+ as much as possible.
                        // We now test if approaching w- would require changing the buffer.
                        // If yes, then we have two possible representations close to w, but we cannot decide which one is closer.
                        if ((rest < bigDistance) && ((unsafeInterval - rest) >= tenKappa) && (((rest + tenKappa) < bigDistance) || ((bigDistance - rest) > (rest + tenKappa - bigDistance))))
                        {
                            return false;
                        }

                        // Weeding test.
                        //
                        // The safe interval is [tooLow + 2 ulp; tooHigh - 2 ulp]
                        // Since tooLow = tooHigh - unsafeInterval this is equivalent to
                        //      [tooHigh - unsafeInterval + 4 ulp; tooHigh - 2 ulp]
                        //
                        // Conceptually we have: rest ~= tooHigh - buffer
                        return ((2 * unit) <= rest) && (rest <= (unsafeInterval - 4 * unit));
                    }
                }

                // Based on https://github.com/dotnet/runtime/blob/master/src/libraries/System.Private.CoreLib/src/System/Number.DiyFp.cs
                // Licensed to the .NET Foundation under one or more agreements and published by the .NET Foundation under the MIT license.
                //
                // This is a port of the `DiyFp` implementation here: https://github.com/google/double-conversion/blob/a711666ddd063eb1e4b181a6cb981d39a1fc8bac/double-conversion/diy-fp.h
                // The backing structure and how it is used is described in more detail here: http://www.cs.tufts.edu/~nr/cs257/archive/florian-loitsch/printf.pdf
                //
                // This "Do It Yourself Floating Point" class implements a floating-point number with a ulong significand and an int exponent.
                // Normalized DiyFp numbers will have the most significant bit of the significand set.
                // Multiplication and Subtraction do not normalize their results.
                // DiyFp are not designed to contain special doubles (NaN and Infinity).
                private readonly ref struct DiyFp
                {
                    public const int DoubleImplicitBitIndex = 52;
                    public const int SingleImplicitBitIndex = 23;

                    public const int SignificandSize = 64;

                    public readonly ulong f;
                    public readonly int e;

                    // Computes the two boundaries of value.
                    //
                    // The bigger boundary (mPlus) is normalized.
                    // The lower boundary has the same exponent as mPlus.
                    //
                    // Precondition:
                    //  The value encoded by value must be greater than 0.
                    public static DiyFp CreateAndGetBoundaries(double value, out DiyFp mMinus, out DiyFp mPlus)
                    {
                        var result = new DiyFp(value);
                        result.GetBoundaries(DoubleImplicitBitIndex, out mMinus, out mPlus);
                        return result;
                    }

                    // Computes the two boundaries of value.
                    //
                    // The bigger boundary (mPlus) is normalized.
                    // The lower boundary has the same exponent as mPlus.
                    //
                    // Precondition:
                    //  The value encoded by value must be greater than 0.
                    public static DiyFp CreateAndGetBoundaries(float value, out DiyFp mMinus, out DiyFp mPlus)
                    {
                        var result = new DiyFp(value);
                        result.GetBoundaries(SingleImplicitBitIndex, out mMinus, out mPlus);
                        return result;
                    }

                    public DiyFp(double value)
                    {
                        //Debug.Assert(double.IsFinite(value));
                        Debug.Assert(value > 0.0);
                        f = ExtractFractionAndBiasedExponent(value, out e);
                    }

                    public DiyFp(float value)
                    {
                        //Debug.Assert(float.IsFinite(value));
                        Debug.Assert(value > 0.0f);
                        f = ExtractFractionAndBiasedExponent(value, out e);
                    }

                    public DiyFp(ulong f, int e)
                    {
                        this.f = f;
                        this.e = e;
                    }

                    public DiyFp Multiply(in DiyFp other)
                    {
                        // Simply "emulates" a 128-bit multiplication
                        //
                        // However: the resulting number only contains 64-bits. The least
                        // signficant 64-bits are only used for rounding the most significant
                        // 64-bits.

                        uint a = (uint)(f >> 32);
                        uint b = (uint)(f);

                        uint c = (uint)(other.f >> 32);
                        uint d = (uint)(other.f);

                        ulong ac = ((ulong)(a) * c);
                        ulong bc = ((ulong)(b) * c);
                        ulong ad = ((ulong)(a) * d);
                        ulong bd = ((ulong)(b) * d);

                        ulong tmp = (bd >> 32) + (uint)(ad) + (uint)(bc);

                        // By adding (1UL << 31) to tmp, we round the final result.
                        // Halfway cases will be rounded up.

                        tmp += (1U << 31);

                        return new DiyFp(ac + (ad >> 32) + (bc >> 32) + (tmp >> 32), e + other.e + SignificandSize);
                    }

                    public DiyFp Normalize()
                    {
                        // This method is mainly called for normalizing boundaries.
                        //
                        // We deviate from the reference implementation by just using
                        // our LeadingZeroCount function so that we only need to shift
                        // and subtract once.

                        Debug.Assert(f != 0);
                        int lzcnt = MathEx.LeadingZeroCount(f);
                        return new DiyFp(f << lzcnt, e - lzcnt);
                    }

                    // The exponents of both numbers must be the same.
                    // The significand of 'this' must be bigger than the significand of 'other'.
                    // The result will not be normalized.
                    public DiyFp Subtract(in DiyFp other)
                    {
                        Debug.Assert(e == other.e);
                        Debug.Assert(f >= other.f);
                        return new DiyFp(f - other.f, e);
                    }

                    private void GetBoundaries(int implicitBitIndex, out DiyFp mMinus, out DiyFp mPlus)
                    {
                        mPlus = new DiyFp((f << 1) + 1, e - 1).Normalize();

                        // The boundary is closer if the sigificand is of the form:
                        //      f == 2^p-1
                        //
                        // Think of v = 1000e10 and v- = 9999e9
                        // Then the boundary == (v - v-) / 2 is not just at a distance of 1e9 but at a distance of 1e8.
                        // The only exception is for the smallest normal, where the largest denormal is at the same distance as its successor.
                        //
                        // Note: denormals have the same exponent as the smallest normals.

                        // We deviate from the reference implementation by just checking if the significand has only the implicit bit set.
                        // In this scenario, we know that all the explicit bits are 0 and that the unbiased exponent is non-zero.
                        if (f == (1UL << implicitBitIndex))
                        {
                            mMinus = new DiyFp((f << 2) - 1, e - 2);
                        }
                        else
                        {
                            mMinus = new DiyFp((f << 1) - 1, e - 1);
                        }

                        mMinus = new DiyFp(mMinus.f << (mMinus.e - mPlus.e), mPlus.e);
                    }
                }
                #endregion

                #region Dragon4

                // Based on https://github.com/dotnet/runtime/commits/master/src/libraries/System.Private.CoreLib/src/System/Number.Dragon4.cs
                // Licensed to the .NET Foundation under one or more agreements and published by the .NET Foundation under the MIT license.
                
                // This is a port of the `Dragon4` implementation here: http://www.ryanjuckett.com/programming/printing-floating-point-numbers/part-2/
                // The backing algorithm and the proofs behind it are described in more detail here:  https://www.cs.indiana.edu/~dyb/pubs/FP-Printing-PLDI96.pdf

                public static void Dragon4(double value, int cutoffNumber, bool isSignificantDigits, ref NumberBuffer number)
                {
                    Debug.Assert(value > 0);

                    ulong mantissa = ExtractFractionAndBiasedExponent(value, out int exponent);

                    uint mantissaHighBitIdx;
                    bool hasUnequalMargins = false;

                    if ((mantissa >> DiyFp.DoubleImplicitBitIndex) != 0)
                    {
                        mantissaHighBitIdx = DiyFp.DoubleImplicitBitIndex;
                        hasUnequalMargins = (mantissa == (1UL << DiyFp.DoubleImplicitBitIndex));
                    }
                    else
                    {
                        Debug.Assert(mantissa != 0);
                        mantissaHighBitIdx = (uint)MathEx.Log2(mantissa);
                    }

                    int length = (int)(Dragon4(mantissa, exponent, mantissaHighBitIdx, hasUnequalMargins, cutoffNumber, isSignificantDigits, number.Digits, out int decimalExponent));

                    number.Scale = decimalExponent + 1;
                    number.Digits[length] = (byte)'\0';
                    number.DigitsCount = length;                    
                }

                public static void Dragon4(float value, int cutoffNumber, bool isSignificantDigits, ref NumberBuffer number)
                {
                    Debug.Assert(value > 0);

                    uint mantissa = ExtractFractionAndBiasedExponent(value, out int exponent);

                    uint mantissaHighBitIdx;
                    bool hasUnequalMargins = false;

                    if ((mantissa >> DiyFp.SingleImplicitBitIndex) != 0)
                    {
                        mantissaHighBitIdx = DiyFp.SingleImplicitBitIndex;
                        hasUnequalMargins = (mantissa == (1U << DiyFp.SingleImplicitBitIndex));
                    }
                    else
                    {
                        Debug.Assert(mantissa != 0);
                        mantissaHighBitIdx = (uint)MathEx.Log2(mantissa);
                    }

                    int length = (int)(Dragon4(mantissa, exponent, mantissaHighBitIdx, hasUnequalMargins, cutoffNumber, isSignificantDigits, number.Digits, out int decimalExponent));

                    number.Scale = decimalExponent + 1;
                    number.Digits[length] = (byte)'\0';
                    number.DigitsCount = length;                    
                }

                // This is an implementation of the Dragon4 algorithm to convert a binary number in floating-point format to a decimal number in string format.
                // The function returns the number of digits written to the output buffer and the output is not NUL terminated.
                //
                // The floating point input value is (mantissa * 2^exponent).
                //
                // See the following papers for more information on the algorithm:
                //  "How to Print Floating-Point Numbers Accurately"
                //    Steele and White
                //    http://kurtstephens.com/files/p372-steele.pdf
                //  "Printing Floating-Point Numbers Quickly and Accurately"
                //    Burger and Dybvig
                //    http://citeseerx.ist.psu.edu/viewdoc/download?doi=10.1.1.72.4656&rep=rep1&type=pdf
                private static uint Dragon4(ulong mantissa, int exponent, uint mantissaHighBitIdx, bool hasUnequalMargins, int cutoffNumber, bool isSignificantDigits, Span<byte> buffer, out int decimalExponent)
                {
                    int curDigit = 0;

                    Debug.Assert(buffer.Length > 0);

                    // We deviate from the original algorithm and just assert that the mantissa
                    // is not zero. Comparing to zero is fine since the caller should have set
                    // the implicit bit of the mantissa, meaning it would only ever be zero if
                    // the extracted exponent was also zero. And the assertion is fine since we
                    // require that the DoubleToNumber handle zero itself.
                    Debug.Assert(mantissa != 0);

                    // Compute the initial state in integral form such that
                    //      value     = scaledValue / scale
                    //      marginLow = scaledMarginLow / scale

                    Span<uint> scaleBlocks = stackalloc uint[BigInteger.MaxBlockCount];
                    Span<uint> scaledValueBlocks = stackalloc uint[BigInteger.MaxBlockCount];
                    Span<uint> scaledMarginLowBlocks = stackalloc uint[BigInteger.MaxBlockCount];
                    Span<uint> optionalMarginHighBlocks = stackalloc uint[BigInteger.MaxBlockCount];

                    var scale = new BigInteger(scaleBlocks);                        // positive scale applied to value and margin such that they can be represented as whole numbers
                    var scaledValue = new BigInteger(scaledValueBlocks);            // scale * mantissa
                    var scaledMarginLow = new BigInteger(scaledMarginLowBlocks);    // scale * 0.5 * (distance between this floating-point number and its immediate lower value)

                    // For normalized IEEE floating-point values, each time the exponent is incremented the margin also doubles.
                    // That creates a subset of transition numbers where the high margin is twice the size of the low margin.

                    var optionalMarginHigh = new BigInteger(optionalMarginHighBlocks);
                    ref BigInteger pScaledMarginHigh = ref scaledMarginLow;

                    // It's not possible to call ReferenceEquals on ref locals. Boxing takes place and ReferenceEquals always retruns false
                    // so the only way to avoid unsafe pointers is to keep track of when pScaledMarginHigh is assigned to scaledMarginLow.
                    // This is more prone to human error but this method is not so big after all and it's not like this code is going to be 
                    // changed a lot in the future.
                    bool areScaledMarginsEqual;

                    if (hasUnequalMargins)
                    {
                        if (exponent > 0)   // We have no fractional component
                        {
                            // 1) Expand the input value by multiplying out the mantissa and exponent.
                            //    This represents the input value in its whole number representation.
                            // 2) Apply an additional scale of 2 such that later comparisons against the margin values are simplified.
                            // 3) Set the margin value to the loweset mantissa bit's scale.

                            // scaledValue = 2 * 2 * mantissa * 2^exponent
                            scaledValue.SetUInt64(4 * mantissa);
                            scaledValue.ShiftLeft((uint)(exponent));

                            // scale = 2 * 2 * 1
                            scale.SetUInt32(4);

                            // scaledMarginLow = 2 * 2^(exponent - 1)
                            BigInteger.Pow2((uint)(exponent), ref scaledMarginLow);

                            // scaledMarginHigh = 2 * 2 * 2^(exponent + 1)
                            BigInteger.Pow2((uint)(exponent + 1), ref optionalMarginHigh);
                        }
                        else                // We have a fractional exponent
                        {
                            // In order to track the mantissa data as an integer, we store it as is with a large scale

                            // scaledValue = 2 * 2 * mantissa
                            scaledValue.SetUInt64(4 * mantissa);

                            // scale = 2 * 2 * 2^(-exponent)
                            BigInteger.Pow2((uint)(-exponent + 2), ref scale);

                            // scaledMarginLow  = 2 * 2^(-1)
                            scaledMarginLow.SetUInt32(1);

                            // scaledMarginHigh = 2 * 2 * 2^(-1)
                            optionalMarginHigh.SetUInt32(2);
                        }

                        // The high and low margins are different
                        pScaledMarginHigh = ref optionalMarginHigh;
                        areScaledMarginsEqual = false;
                    }
                    else
                    {
                        if (exponent > 0)   // We have no fractional component
                        {
                            // 1) Expand the input value by multiplying out the mantissa and exponent.
                            //    This represents the input value in its whole number representation.
                            // 2) Apply an additional scale of 2 such that later comparisons against the margin values are simplified.
                            // 3) Set the margin value to the lowest mantissa bit's scale.

                            // scaledValue = 2 * mantissa*2^exponent
                            scaledValue.SetUInt64(2 * mantissa);
                            scaledValue.ShiftLeft((uint)(exponent));

                            // scale = 2 * 1
                            scale.SetUInt32(2);

                            // scaledMarginLow = 2 * 2^(exponent-1)
                            BigInteger.Pow2((uint)(exponent), ref scaledMarginLow);
                        }
                        else                // We have a fractional exponent
                        {
                            // In order to track the mantissa data as an integer, we store it as is with a large scale

                            // scaledValue = 2 * mantissa
                            scaledValue.SetUInt64(2 * mantissa);

                            // scale = 2 * 2^(-exponent)
                            BigInteger.Pow2((uint)(-exponent + 1), ref scale);

                            // scaledMarginLow = 2 * 2^(-1)
                            scaledMarginLow.SetUInt32(1);
                        }

                        // The high and low margins are equal
                        pScaledMarginHigh = ref scaledMarginLow;
                        areScaledMarginsEqual = true;
                    }

                    // Compute an estimate for digitExponent that will be correct or undershoot by one.
                    //
                    // This optimization is based on the paper "Printing Floating-Point Numbers Quickly and Accurately" by Burger and Dybvig http://citeseerx.ist.psu.edu/viewdoc/download?doi=10.1.1.72.4656&rep=rep1&type=pdf
                    //
                    // We perform an additional subtraction of 0.69 to increase the frequency of a failed estimate because that lets us take a faster branch in the code.
                    // 0.69 is chosen because 0.69 + log10(2) is less than one by a reasonable epsilon that will account for any floating point error.
                    //
                    // We want to set digitExponent to floor(log10(v)) + 1
                    //      v = mantissa * 2^exponent
                    //      log2(v) = log2(mantissa) + exponent;
                    //      log10(v) = log2(v) * log10(2)
                    //      floor(log2(v)) = mantissaHighBitIdx + exponent;
                    //      log10(v) - log10(2) < (mantissaHighBitIdx + exponent) * log10(2) <= log10(v)
                    //      log10(v) < (mantissaHighBitIdx + exponent) * log10(2) + log10(2) <= log10(v) + log10(2)
                    //      floor(log10(v)) < ceil((mantissaHighBitIdx + exponent) * log10(2)) <= floor(log10(v)) + 1
                    const double Log10V2 = 0.30102999566398119521373889472449;
                    int digitExponent = (int)(Math.Ceiling(((int)(mantissaHighBitIdx) + exponent) * Log10V2 - 0.69));

                    // Divide value by 10^digitExponent.
                    if (digitExponent > 0)
                    {
                        // The exponent is positive creating a division so we multiply up the scale.
                        if (digitExponent <= 9)
                        {
                            scale.MultiplyPow10((uint)(digitExponent));
                        }
                        else if (!scale.IsZero())
                        {
                            Span<uint> poweredValueBlocks = stackalloc uint[BigInteger.MaxBlockCount];
                            var poweredValue = new BigInteger(poweredValueBlocks);
                            BigInteger.Pow10((uint)digitExponent, ref poweredValue);

                            if (poweredValue.Length <= 1)
                                BigInteger.Multiply(in scale, poweredValue.ToUInt32(), ref scale);
                            else
                            {
                                Span<uint> lhsBlocks = stackalloc uint[scale.Length];
                                var lhs = new BigInteger(lhsBlocks);
                                lhs.SetValue(in scale);
                                BigInteger.Multiply(in lhs, in poweredValue, ref scale);
                            }
                        }
                    }
                    else if (digitExponent < 0)
                    {
                        // The exponent is negative creating a multiplication so we multiply up the scaledValue, scaledMarginLow and scaledMarginHigh.

                        Span<uint> pow10Blocks = stackalloc uint[BigInteger.MaxBlockCount];
                        var pow10 = new BigInteger(pow10Blocks);
                        BigInteger.Pow10((uint)(-digitExponent), ref pow10);

                        if (pow10.Length <= 1)
                        {
                            BigInteger.Multiply(in scaledValue, pow10.ToUInt32(), ref scaledValue);
                            BigInteger.Multiply(in scaledMarginLow, pow10.ToUInt32(), ref scaledMarginLow);
                        }
                        else
                        {
                            var n = Math.Max(scaledValue.Length, scaledMarginLow.Length);
                            Span<uint> lhsBlocks = stackalloc uint[n];
                            var lhs = new BigInteger(lhsBlocks);
                            lhs.SetValue(in scaledValue);
                            BigInteger.Multiply(in lhs, in pow10, ref scaledValue);
                            lhs.SetValue(in scaledMarginLow);
                            BigInteger.Multiply(in lhs, in pow10, ref scaledMarginLow);
                        }

                        if (!areScaledMarginsEqual)
                        {
                            BigInteger.Multiply(in scaledMarginLow, 2, ref pScaledMarginHigh);
                        }
                    }

                    bool isEven = (mantissa % 2) == 0;
                    bool estimateTooLow = false;

                    if (cutoffNumber == -1)
                    {
                        // When printing the shortest possible string, we want to
                        // take IEEE unbiased rounding into account so we can return
                        // shorter strings for various edge case values like 1.23E+22

                        Span<uint> scaledValueHighBlocks = stackalloc uint[BigInteger.MaxBlockCount];
                        var scaledValueHigh = new BigInteger(scaledValueHighBlocks);

                        BigInteger.Add(in scaledValue, in pScaledMarginHigh, ref scaledValueHigh);
                        int cmpHigh = BigInteger.Compare(in scaledValueHigh, in scale);
                        estimateTooLow = isEven ? (cmpHigh >= 0) : (cmpHigh > 0);
                    }
                    else
                    {
                        estimateTooLow = BigInteger.Compare(in scaledValue, in scale) >= 0;
                    }

                    // Was our estimate for digitExponent was too low?
                    if (estimateTooLow)
                    {
                        // The exponent estimate was incorrect.
                        // Increment the exponent and don't perform the premultiply needed for the first loop iteration.
                        digitExponent++;
                    }
                    else
                    {
                        // The exponent estimate was correct.
                        // Multiply larger by the output base to prepare for the first loop iteration.
                        scaledValue.Multiply10();
                        scaledMarginLow.Multiply10();

                        if (!areScaledMarginsEqual)
                        {
                            BigInteger.Multiply(in scaledMarginLow, 2, ref pScaledMarginHigh);
                        }
                    }

                    // Compute the cutoff exponent (the exponent of the final digit to print).
                    // Default to the maximum size of the output buffer.
                    int cutoffExponent = digitExponent - buffer.Length;

                    if (cutoffNumber != -1)
                    {
                        int desiredCutoffExponent = 0;

                        if (isSignificantDigits)
                        {
                            // We asked for a specific number of significant digits.
                            Debug.Assert(cutoffNumber > 0);
                            desiredCutoffExponent = digitExponent - cutoffNumber;
                        }
                        else
                        {
                            // We asked for a specific number of fractional digits.
                            Debug.Assert(cutoffNumber >= 0);
                            desiredCutoffExponent = -cutoffNumber;
                        }

                        if (desiredCutoffExponent > cutoffExponent)
                        {
                            // Only select the new cutoffExponent if it won't overflow the destination buffer.
                            cutoffExponent = desiredCutoffExponent;
                        }
                    }

                    // Output the exponent of the first digit we will print
                    decimalExponent = --digitExponent;

                    // In preparation for calling BigInteger.HeuristicDivie(), we need to scale up our values such that the highest block of the denominator is greater than or equal to 8.
                    // We also need to guarantee that the numerator can never have a length greater than the denominator after each loop iteration.
                    // This requires the highest block of the denominator to be less than or equal to 429496729 which is the highest number that can be multiplied by 10 without overflowing to a new block.

                    Debug.Assert(scale.Length > 0);
                    uint hiBlock = scale.LastBlock;

                    if ((hiBlock < 8) || (hiBlock > 429496729))
                    {
                        // Perform a bit shift on all values to get the highest block of the denominator into the range [8,429496729].
                        // We are more likely to make accurate quotient estimations in BigInteger.HeuristicDivide() with higher denominator values so we shift the denominator to place the highest bit at index 27 of the highest block.
                        // This is safe because (2^28 - 1) = 268435455 which is less than 429496729.
                        // This means that all values with a highest bit at index 27 are within range.
                        Debug.Assert(hiBlock != 0);
                        uint hiBlockLog2 = (uint)MathEx.Log2(hiBlock);
                        Debug.Assert((hiBlockLog2 < 3) || (hiBlockLog2 > 27));
                        uint shift = (32 + 27 - hiBlockLog2) % 32;

                        scale.ShiftLeft(shift);
                        scaledValue.ShiftLeft(shift);
                        scaledMarginLow.ShiftLeft(shift);

                        if (!areScaledMarginsEqual)
                        {
                            BigInteger.Multiply(in scaledMarginLow, 2, ref pScaledMarginHigh);
                        }
                    }

                    // These values are used to inspect why the print loop terminated so we can properly round the final digit.
                    bool low;            // did the value get within marginLow distance from zero
                    bool high;           // did the value get within marginHigh distance from one
                    uint outputDigit;    // current digit being output

                    if (cutoffNumber == -1)
                    {
                        Debug.Assert(isSignificantDigits);
                        Debug.Assert(digitExponent >= cutoffExponent);

                        // For the unique cutoff mode, we will try to print until we have reached a level of precision that uniquely distinguishes this value from its neighbors.
                        // If we run out of space in the output buffer, we terminate early.

                        while (true)
                        {
                            // divide out the scale to extract the digit
                            outputDigit = BigInteger.HeuristicDivide(ref scaledValue, in scale);
                            Debug.Assert(outputDigit < 10);

                            Span<uint> scaledValueHighBlocks = stackalloc uint[BigInteger.MaxBlockCount];
                            var scaledValueHigh = new BigInteger(scaledValueHighBlocks);

                            // update the high end of the value
                            BigInteger.Add(in scaledValue, in pScaledMarginHigh, ref scaledValueHigh);

                            // stop looping if we are far enough away from our neighboring values or if we have reached the cutoff digit
                            int cmpLow = BigInteger.Compare(in scaledValue, in scaledMarginLow);
                            int cmpHigh = BigInteger.Compare(in scaledValueHigh, in scale);

                            if (isEven)
                            {
                                low = (cmpLow <= 0);
                                high = (cmpHigh >= 0);
                            }
                            else
                            {
                                low = (cmpLow < 0);
                                high = (cmpHigh > 0);
                            }

                            if (low || high || (digitExponent == cutoffExponent))
                            {
                                break;
                            }

                            // store the output digit
                            buffer[curDigit] = (byte)('0' + outputDigit);
                            curDigit++;

                            // multiply larger by the output base
                            scaledValue.Multiply10();
                            scaledMarginLow.Multiply10();

                            if (!areScaledMarginsEqual)
                            {
                                BigInteger.Multiply(in scaledMarginLow, 2, ref pScaledMarginHigh);
                            }

                            digitExponent--;
                        }
                    }
                    else if (digitExponent >= cutoffExponent)
                    {
                        Debug.Assert((cutoffNumber > 0) || ((cutoffNumber == 0) && !isSignificantDigits));

                        // For length based cutoff modes, we will try to print until we have exhausted all precision (i.e. all remaining digits are zeros) or until we reach the desired cutoff digit.
                        low = false;
                        high = false;

                        while (true)
                        {
                            // divide out the scale to extract the digit
                            outputDigit = BigInteger.HeuristicDivide(ref scaledValue, in scale);
                            Debug.Assert(outputDigit < 10);

                            if (scaledValue.IsZero() || (digitExponent <= cutoffExponent))
                            {
                                break;
                            }

                            // store the output digit
                            buffer[curDigit] = (byte)('0' + outputDigit);
                            curDigit++;

                            // multiply larger by the output base
                            scaledValue.Multiply10();
                            digitExponent--;
                        }
                    }
                    else
                    {
                        // In the scenario where the first significant digit is after the cutoff, we want to treat that
                        // first significant digit as the rounding digit. If the first significant would cause the next
                        // digit to round, we will increase the decimalExponent by one and set the previous digit to one.
                        // This  ensures we correctly handle the case where the first significant digit is exactly one after
                        // the cutoff, it is a 4, and the subsequent digit would round that to 5 inducing a double rounding
                        // bug when NumberToString does its own rounding checks. However, if the first significant digit
                        // would not cause the next one to round, we preserve that digit as is.

                        // divide out the scale to extract the digit
                        outputDigit = BigInteger.HeuristicDivide(ref scaledValue, in scale);
                        Debug.Assert((0 < outputDigit) && (outputDigit < 10));

                        if ((outputDigit > 5) || ((outputDigit == 5) && !scaledValue.IsZero()))
                        {
                            decimalExponent++;
                            outputDigit = 1;
                        }

                        buffer[curDigit] = (byte)('0' + outputDigit);
                        curDigit++;

                        // return the number of digits output
                        return (uint)curDigit;
                    }

                    // round off the final digit
                    // default to rounding down if value got too close to 0
                    bool roundDown = low;

                    if (low == high)    // is it legal to round up and down
                    {
                        // round to the closest digit by comparing value with 0.5.
                        //
                        // To do this we need to convert the inequality to large integer values.
                        //      compare(value, 0.5)
                        //      compare(scale * value, scale * 0.5)
                        //      compare(2 * scale * value, scale)
                        scaledValue.ShiftLeft(1); // Multiply by 2
                        int compare = BigInteger.Compare(in scaledValue, in scale);
                        roundDown = compare < 0;

                        // if we are directly in the middle, round towards the even digit (i.e. IEEE rouding rules)
                        if (compare == 0)
                        {
                            roundDown = (outputDigit & 1) == 0;
                        }
                    }

                    // print the rounded digit
                    if (roundDown)
                    {
                        buffer[curDigit] = (byte)('0' + outputDigit);
                        curDigit++;
                    }
                    else if (outputDigit == 9)      // handle rounding up
                    {
                        // find the first non-nine prior digit
                        while (true)
                        {
                            // if we are at the first digit
                            if (curDigit == 0)
                            {
                                // output 1 at the next highest exponent

                                buffer[curDigit] = (byte)('1');
                                curDigit++;
                                decimalExponent++;

                                break;
                            }

                            curDigit--;

                            if (buffer[curDigit] != '9')
                            {
                                // increment the digit

                                buffer[curDigit]++;
                                curDigit++;

                                break;
                            }
                        }
                    }
                    else
                    {
                        // values in the range [0,8] can perform a simple round up
                        buffer[curDigit] = (byte)('0' + outputDigit + 1);
                        curDigit++;
                    }

                    // return the number of digits output
                    uint outputLen = (uint)curDigit;
                    Debug.Assert(outputLen <= buffer.Length);
                    return outputLen;
                }

                #endregion

                // Based on https://github.com/dotnet/runtime/blob/master/src/libraries/System.Private.CoreLib/src/System/Number.BigInteger.cs
                // Licensed to the .NET Foundation under one or more agreements and published by the .NET Foundation under the MIT license.
                [StructLayout(LayoutKind.Sequential, Pack = 1)]
                internal ref struct BigInteger
                {
                    public BigInteger(Span<uint> buffer, int length = 0)
                    {
                        _length = length;
                        _blocks = buffer;
                    }

                    // The longest binary mantissa requires: explicit mantissa bits + abs(min exponent)
                    // * Half:     10 +    14 =    24
                    // * Single:   23 +   126 =   149
                    // * Double:   52 +  1022 =  1074
                    // * Quad:    112 + 16382 = 16494
                    private const int BitsForLongestBinaryMantissa = 1074;

                    // The longest digit sequence requires: ceil(log2(pow(10, max significant digits + 1 rounding digit)))
                    // * Half:    ceil(log2(pow(10,    21 + 1))) =    74
                    // * Single:  ceil(log2(pow(10,   112 + 1))) =   376
                    // * Double:  ceil(log2(pow(10,   767 + 1))) =  2552
                    // * Quad:    ceil(log2(pow(10, 11563 + 1))) = 38415
                    private const int BitsForLongestDigitSequence = 2552;

                    // We require BitsPerBlock additional bits for shift space used during the pre-division preparation
                    private const int MaxBits = BitsForLongestBinaryMantissa + BitsForLongestDigitSequence + BitsPerBlock;

                    private const int BitsPerBlock = sizeof(int) * 8;
                    public const int MaxBlockCount = (MaxBits + (BitsPerBlock - 1)) / BitsPerBlock;

                    private static readonly uint[] s_Pow10UInt32Table = new uint[]
                    {
                        1,          // 10^0
                        10,         // 10^1
                        100,        // 10^2
                        1000,       // 10^3
                        10000,      // 10^4
                        100000,     // 10^5
                        1000000,    // 10^6
                        10000000,   // 10^7
                        // These last two are accessed only by MultiplyPow10.
                        100000000,  // 10^8
                        1000000000  // 10^9
                    };

                    private static readonly int[] s_Pow10BigNumTableIndices = new int[]
                    {
                        0,          // 10^8
                        2,          // 10^16
                        5,          // 10^32
                        10,         // 10^64
                        18,         // 10^128
                        33,         // 10^256
                        61,         // 10^512
                        116,        // 10^1024
                    };

                    private static readonly uint[] s_Pow10BigNumTable = new uint[]
                    {
                        // 10^8
                        1,          // _length
                        100000000,  // _blocks

                        // 10^16
                        2,          // _length
                        0x6FC10000, // _blocks
                        0x002386F2,

                        // 10^32
                        4,          // _length
                        0x00000000, // _blocks
                        0x85ACEF81,
                        0x2D6D415B,
                        0x000004EE,

                        // 10^64
                        7,          // _length
                        0x00000000, // _blocks
                        0x00000000,
                        0xBF6A1F01,
                        0x6E38ED64,
                        0xDAA797ED,
                        0xE93FF9F4,
                        0x00184F03,

                        // 10^128
                        14,         // _length
                        0x00000000, // _blocks
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x2E953E01,
                        0x03DF9909,
                        0x0F1538FD,
                        0x2374E42F,
                        0xD3CFF5EC,
                        0xC404DC08,
                        0xBCCDB0DA,
                        0xA6337F19,
                        0xE91F2603,
                        0x0000024E,

                        // 10^256
                        27,         // _length
                        0x00000000, // _blocks
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x982E7C01,
                        0xBED3875B,
                        0xD8D99F72,
                        0x12152F87,
                        0x6BDE50C6,
                        0xCF4A6E70,
                        0xD595D80F,
                        0x26B2716E,
                        0xADC666B0,
                        0x1D153624,
                        0x3C42D35A,
                        0x63FF540E,
                        0xCC5573C0,
                        0x65F9EF17,
                        0x55BC28F2,
                        0x80DCC7F7,
                        0xF46EEDDC,
                        0x5FDCEFCE,
                        0x000553F7,

                        // 10^512
                        54,         // _length
                        0x00000000, // _blocks
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0xFC6CF801,
                        0x77F27267,
                        0x8F9546DC,
                        0x5D96976F,
                        0xB83A8A97,
                        0xC31E1AD9,
                        0x46C40513,
                        0x94E65747,
                        0xC88976C1,
                        0x4475B579,
                        0x28F8733B,
                        0xAA1DA1BF,
                        0x703ED321,
                        0x1E25CFEA,
                        0xB21A2F22,
                        0xBC51FB2E,
                        0x96E14F5D,
                        0xBFA3EDAC,
                        0x329C57AE,
                        0xE7FC7153,
                        0xC3FC0695,
                        0x85A91924,
                        0xF95F635E,
                        0xB2908EE0,
                        0x93ABADE4,
                        0x1366732A,
                        0x9449775C,
                        0x69BE5B0E,
                        0x7343AFAC,
                        0xB099BC81,
                        0x45A71D46,
                        0xA2699748,
                        0x8CB07303,
                        0x8A0B1F13,
                        0x8CAB8A97,
                        0xC1D238D9,
                        0x633415D4,
                        0x0000001C,

                        // 10^1024
                        107,        // _length
                        0x00000000, // _blocks
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x2919F001,
                        0xF55B2B72,
                        0x6E7C215B,
                        0x1EC29F86,
                        0x991C4E87,
                        0x15C51A88,
                        0x140AC535,
                        0x4C7D1E1A,
                        0xCC2CD819,
                        0x0ED1440E,
                        0x896634EE,
                        0x7DE16CFB,
                        0x1E43F61F,
                        0x9FCE837D,
                        0x231D2B9C,
                        0x233E55C7,
                        0x65DC60D7,
                        0xF451218B,
                        0x1C5CD134,
                        0xC9635986,
                        0x922BBB9F,
                        0xA7E89431,
                        0x9F9F2A07,
                        0x62BE695A,
                        0x8E1042C4,
                        0x045B7A74,
                        0x1ABE1DE3,
                        0x8AD822A5,
                        0xBA34C411,
                        0xD814B505,
                        0xBF3FDEB3,
                        0x8FC51A16,
                        0xB1B896BC,
                        0xF56DEEEC,
                        0x31FB6BFD,
                        0xB6F4654B,
                        0x101A3616,
                        0x6B7595FB,
                        0xDC1A47FE,
                        0x80D98089,
                        0x80BDA5A5,
                        0x9A202882,
                        0x31EB0F66,
                        0xFC8F1F90,
                        0x976A3310,
                        0xE26A7B7E,
                        0xDF68368A,
                        0x3CE3A0B8,
                        0x8E4262CE,
                        0x75A351A2,
                        0x6CB0B6C9,
                        0x44597583,
                        0x31B5653F,
                        0xC356E38A,
                        0x35FAABA6,
                        0x0190FBA0,
                        0x9FC4ED52,
                        0x88BC491B,
                        0x1640114A,
                        0x005B8041,
                        0xF4F3235E,
                        0x1E8D4649,
                        0x36A8DE06,
                        0x73C55349,
                        0xA7E6BD2A,
                        0xC1A6970C,
                        0x47187094,
                        0xD2DB49EF,
                        0x926C3F5B,
                        0xAE6209D4,
                        0x2D433949,
                        0x34F4A3C6,
                        0xD4305D94,
                        0xD9D61A05,
                        0x00000325,

                        // 9 Trailing blocks to ensure MaxBlockCount
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                        0x00000000,
                    };

                    // Pow10 depends on _length and _blocks defined in this exact order (hence the use of LayoutKind.Sequential with Pack = 1)
                    private int _length;
                    private Span<uint> _blocks;

                    public int Length => _length;

                    public uint LastBlock => _blocks[_length - 1];

                    public static void Add(in BigInteger lhs, in BigInteger rhs, ref BigInteger result)
                    {
                        // determine which operand has the smaller length
                        ref readonly var large = ref (lhs._length < rhs._length) ? ref rhs : ref lhs;
                        ref readonly var small = ref (lhs._length < rhs._length) ? ref lhs : ref rhs;

                        int largeLength = large._length;
                        int smallLength = small._length;

                        // The output will be at least as long as the largest input
                        result._length = largeLength;

                        // Add each block and add carry the overflow to the next block
                        ulong carry = 0;

                        int largeIndex = 0;
                        int smallIndex = 0;
                        int resultIndex = 0;

                        while (smallIndex < smallLength)
                        {
                            ulong sum = carry + large._blocks[largeIndex] + small._blocks[smallIndex];
                            carry = sum >> 32;
                            result._blocks[resultIndex] = (uint)(sum);

                            largeIndex++;
                            smallIndex++;
                            resultIndex++;
                        }

                        // Add the carry to any blocks that only exist in the large operand
                        while (largeIndex < largeLength)
                        {
                            ulong sum = carry + large._blocks[largeIndex];
                            carry = sum >> 32;
                            result._blocks[resultIndex] = (uint)(sum);

                            largeIndex++;
                            resultIndex++;
                        }

                        // If there's still a carry, append a new block
                        if (carry != 0)
                        {
                            Debug.Assert(carry == 1);
                            Debug.Assert((resultIndex == largeLength) && (largeLength < MaxBlockCount));

                            result._blocks[resultIndex] = 1;
                            result._length++;
                        }
                    }

                    public static int Compare(in BigInteger lhs, in BigInteger rhs)
                    {
                        Debug.Assert(unchecked((uint)(lhs._length)) <= MaxBlockCount);
                        Debug.Assert(unchecked((uint)(rhs._length)) <= MaxBlockCount);

                        int lhsLength = lhs._length;
                        int rhsLength = rhs._length;

                        int lengthDelta = (lhsLength - rhsLength);

                        if (lengthDelta != 0)
                        {
                            return lengthDelta;
                        }

                        if (lhsLength == 0)
                        {
                            Debug.Assert(rhsLength == 0);
                            return 0;
                        }

                        for (int index = (lhsLength - 1); index >= 0; index--)
                        {
                            long delta = (long)(lhs._blocks[index]) - rhs._blocks[index];

                            if (delta != 0)
                            {
                                return delta > 0 ? 1 : -1;
                            }
                        }

                        return 0;
                    }

                    public static uint CountSignificantBits(uint value) => 32 - (uint)MathEx.LeadingZeroCount(value);

                    public static uint CountSignificantBits(ulong value) => 64 - (uint)MathEx.LeadingZeroCount(value);

                    public static uint CountSignificantBits(in BigInteger value)
                    {
                        if (value.IsZero())
                            return 0;

                        // We don't track any unused blocks, so we only need to do a BSR on the
                        // last index and add that to the number of bits we skipped.

                        var lastIndex = value._length - 1;
                        return (uint)(lastIndex * BitsPerBlock) + CountSignificantBits(value._blocks[lastIndex]);
                    }

                    public static void DivRem(in BigInteger lhs, in BigInteger rhs, ref BigInteger quo, ref BigInteger rem)
                    {
                        // This is modified from the libraries BigIntegerCalculator.DivRem.cs implementation:
                        // https://github.com/dotnet/runtime/blob/master/src/libraries/System.Runtime.Numerics/src/System/Numerics/BigIntegerCalculator.DivRem.cs

                        Debug.Assert(!rhs.IsZero());

                        if (lhs.IsZero())
                        {
                            quo.SetZero();
                            rem.SetZero();
                            return;
                        }

                        int lhsLength = lhs._length;
                        int rhsLength = rhs._length;

                        if ((lhsLength == 1) && (rhsLength == 1))
                        {
                            uint quotient = MathEx.DivRem(lhs._blocks[0], rhs._blocks[0], out uint remainder);
                            quo.SetUInt32(quotient);
                            rem.SetUInt32(remainder);
                            return;
                        }

                        if (rhsLength == 1)
                        {
                            // We can make the computation much simpler if the rhs is only one block

                            int quoLength = lhsLength;

                            ulong rhsValue = rhs._blocks[0];
                            ulong carry = 0;

                            for (int i = quoLength - 1; i >= 0; i--)
                            {
                                ulong value = (carry << 32) | lhs._blocks[i];
                                ulong digit = MathEx.DivRem(value, rhsValue, out carry);

                                if ((digit == 0) && (i == (quoLength - 1)))
                                {
                                    quoLength--;
                                }
                                else
                                {
                                    quo._blocks[i] = (uint)(digit);
                                }
                            }

                            quo._length = quoLength;
                            rem.SetUInt32((uint)(carry));

                            return;
                        }
                        else if (rhsLength > lhsLength)
                        {
                            // Handle the case where we have no quotient
                            quo.SetZero();
                            rem.SetValue(in lhs);
                            return;
                        }
                        else
                        {
                            int quoLength = lhsLength - rhsLength + 1;
                            rem.SetValue(in lhs);
                            int remLength = lhsLength;

                            // Executes the "grammar-school" algorithm for computing q = a / b.
                            // Before calculating q_i, we get more bits into the highest bit
                            // block of the divisor. Thus, guessing digits of the quotient
                            // will be more precise. Additionally we'll get r = a % b.

                            uint divHi = rhs._blocks[rhsLength - 1];
                            uint divLo = rhs._blocks[rhsLength - 2];

                            // We measure the leading zeros of the divisor
                            int shiftLeft = MathEx.LeadingZeroCount(divHi);
                            int shiftRight = 32 - shiftLeft;

                            // And, we make sure the most significant bit is set
                            if (shiftLeft > 0)
                            {
                                divHi = (divHi << shiftLeft) | (divLo >> shiftRight);
                                divLo <<= shiftLeft;

                                if (rhsLength > 2)
                                {
                                    divLo |= (rhs._blocks[rhsLength - 3] >> shiftRight);
                                }
                            }

                            // Then, we divide all of the bits as we would do it using
                            // pen and paper: guessing the next digit, subtracting, ...
                            for (int i = lhsLength; i >= rhsLength; i--)
                            {
                                int n = i - rhsLength;
                                uint t = i < lhsLength ? rem._blocks[i] : 0;

                                ulong valHi = ((ulong)(t) << 32) | rem._blocks[i - 1];
                                uint valLo = i > 1 ? rem._blocks[i - 2] : 0;

                                // We shifted the divisor, we shift the dividend too
                                if (shiftLeft > 0)
                                {
                                    valHi = (valHi << shiftLeft) | (valLo >> shiftRight);
                                    valLo <<= shiftLeft;

                                    if (i > 2)
                                    {
                                        valLo |= (rem._blocks[i - 3] >> shiftRight);
                                    }
                                }

                                // First guess for the current digit of the quotient,
                                // which naturally must have only 32 bits...
                                ulong digit = valHi / divHi;

                                if (digit > uint.MaxValue)
                                {
                                    digit = uint.MaxValue;
                                }

                                // Our first guess may be a little bit to big
                                while (DivideGuessTooBig(digit, valHi, valLo, divHi, divLo))
                                {
                                    digit--;
                                }

                                if (digit > 0)
                                {
                                    // Now it's time to subtract our current quotient
                                    uint carry = SubtractDivisor(in rem, n, in rhs, digit);

                                    if (carry != t)
                                    {
                                        Debug.Assert(carry == t + 1);

                                        // Our guess was still exactly one too high
                                        carry = AddDivisor(in rem, n, in rhs);
                                        digit--;

                                        Debug.Assert(carry == 1);
                                    }
                                }

                                // We have the digit!
                                if (quoLength != 0)
                                {
                                    if ((digit == 0) && (n == (quoLength - 1)))
                                    {
                                        quoLength--;
                                    }
                                    else
                                    {
                                        quo._blocks[n] = (uint)(digit);
                                    }
                                }

                                if (i < remLength)
                                {
                                    remLength--;
                                }
                            }

                            quo._length = quoLength;

                            // We need to check for the case where remainder is zero

                            for (int i = remLength - 1; i >= 0; i--)
                            {
                                if (rem._blocks[i] == 0)
                                {
                                    remLength--;
                                }
                            }

                            rem._length = remLength;
                        }
                    }

                    public static uint HeuristicDivide(ref BigInteger dividend, in BigInteger divisor)
                    {
                        int divisorLength = divisor._length;

                        if (dividend._length < divisorLength)
                        {
                            return 0;
                        }

                        // This is an estimated quotient. Its error should be less than 2.
                        // Reference inequality:
                        // a/b - floor(floor(a)/(floor(b) + 1)) < 2
                        int lastIndex = (divisorLength - 1);
                        uint quotient = dividend._blocks[lastIndex] / (divisor._blocks[lastIndex] + 1);

                        if (quotient != 0)
                        {
                            // Now we use our estimated quotient to update each block of dividend.
                            // dividend = dividend - divisor * quotient
                            int index = 0;

                            ulong borrow = 0;
                            ulong carry = 0;

                            do
                            {
                                ulong product = ((ulong)(divisor._blocks[index]) * quotient) + carry;
                                carry = product >> 32;

                                ulong difference = (ulong)(dividend._blocks[index]) - (uint)(product) - borrow;
                                borrow = (difference >> 32) & 1;

                                dividend._blocks[index] = (uint)(difference);

                                index++;
                            }
                            while (index < divisorLength);

                            // Remove all leading zero blocks from dividend
                            while ((divisorLength > 0) && (dividend._blocks[divisorLength - 1] == 0))
                            {
                                divisorLength--;
                            }

                            dividend._length = divisorLength;
                        }

                        // If the dividend is still larger than the divisor, we overshot our estimate quotient. To correct,
                        // we increment the quotient and subtract one more divisor from the dividend (Because we guaranteed the error range).
                        if (Compare(in dividend, in divisor) >= 0)
                        {
                            quotient++;

                            // dividend = dividend - divisor
                            int index = 0;
                            ulong borrow = 0;

                            do
                            {
                                ulong difference = (ulong)(dividend._blocks[index]) - divisor._blocks[index] - borrow;
                                borrow = (difference >> 32) & 1;

                                dividend._blocks[index] = (uint)(difference);

                                index++;
                            }
                            while (index < divisorLength);

                            // Remove all leading zero blocks from dividend
                            while ((divisorLength > 0) && (dividend._blocks[divisorLength - 1] == 0))
                            {
                                divisorLength--;
                            }

                            dividend._length = divisorLength;
                        }

                        return quotient;
                    }

                    public static void Multiply(in BigInteger lhs, uint value, ref BigInteger result)
                    {
                        if (lhs._length <= 1)
                        {
                            result.SetUInt64((ulong)lhs.ToUInt32() * value);
                            return;
                        }

                        if (value <= 1)
                        {
                            if (value == 0)
                            {
                                result.SetZero();
                            }
                            else
                            {
                                result.SetValue(in lhs);
                            }
                            return;
                        }

                        int lhsLength = lhs._length;
                        int index = 0;
                        uint carry = 0;

                        while (index < lhsLength)
                        {
                            ulong product = ((ulong)(lhs._blocks[index]) * value) + carry;
                            result._blocks[index] = (uint)(product);
                            carry = (uint)(product >> 32);

                            index++;
                        }

                        if (carry != 0)
                        {
                            Debug.Assert(unchecked((uint)(lhsLength)) + 1 <= MaxBlockCount);
                            result._blocks[index] = carry;
                            result._length = (lhsLength + 1);
                        }
                        else
                        {
                            result._length = lhsLength;
                        }
                    }

                    public static void Multiply(in BigInteger lhs, in BigInteger rhs, ref BigInteger result)
                    {
                        if (lhs._length <= 1)
                        {
                            Multiply(in rhs, lhs.ToUInt32(), ref result);
                            return;
                        }

                        if (rhs._length <= 1)
                        {
                            Multiply(in lhs, rhs.ToUInt32(), ref result);
                            return;
                        }

                        ref readonly BigInteger large = ref lhs;
                        int largeLength = lhs._length;

                        ref readonly BigInteger small = ref rhs;
                        int smallLength = rhs._length;

                        if (largeLength < smallLength)
                        {
                            large = ref rhs;
                            largeLength = rhs._length;

                            small = ref lhs;
                            smallLength = lhs._length;
                        }

                        int maxResultLength = smallLength + largeLength;
                        Debug.Assert(unchecked((uint)(maxResultLength)) <= MaxBlockCount);

                        // Zero out result internal blocks.
                        result._length = maxResultLength;
                        result.Clear(maxResultLength);

                        int smallIndex = 0;
                        int resultStartIndex = 0;

                        while (smallIndex < smallLength)
                        {
                            // Multiply each block of large BigNum.
                            if (small._blocks[smallIndex] != 0)
                            {
                                int largeIndex = 0;
                                int resultIndex = resultStartIndex;

                                ulong carry = 0;

                                do
                                {
                                    ulong product = result._blocks[resultIndex] + ((ulong)(small._blocks[smallIndex]) * large._blocks[largeIndex]) + carry;
                                    carry = product >> 32;
                                    result._blocks[resultIndex] = (uint)(product);

                                    resultIndex++;
                                    largeIndex++;
                                }
                                while (largeIndex < largeLength);

                                result._blocks[resultIndex] = (uint)(carry);
                            }

                            smallIndex++;
                            resultStartIndex++;
                        }

                        if ((maxResultLength > 0) && (result._blocks[maxResultLength - 1] == 0))
                        {
                            result._length--;
                        }
                    }

                    public static void Pow2(uint exponent, ref BigInteger result)
                    {
                        var blocksToShift = (int)DivRem32(exponent, out uint remainingBitsToShift);
                        result._length = blocksToShift + 1;
                        Debug.Assert(unchecked((uint)result._length) <= MaxBlockCount);
                        if (blocksToShift > 0)
                        {
                            result.Clear(blocksToShift);
                        }
                        result._blocks[blocksToShift] = 1U << (int)remainingBitsToShift;
                    }

                    public static void Pow10(uint exponent, ref BigInteger result)
                    {
                        // We leverage two arrays - s_Pow10UInt32Table and s_Pow10BigNumTable to speed up the Pow10 calculation.
                        //
                        // s_Pow10UInt32Table stores the results of 10^0 to 10^7.
                        // s_Pow10BigNumTable stores the results of 10^8, 10^16, 10^32, 10^64, 10^128, 10^256, and 10^512
                        //
                        // For example, let's say exp = 0b111111. We can split the exp to two parts, one is small exp,
                        // which 10^smallExp can be represented as uint, another part is 10^bigExp, which must be represented as BigNum.
                        // So the result should be 10^smallExp * 10^bigExp.
                        //
                        // Calculating 10^smallExp is simple, we just lookup the 10^smallExp from s_Pow10UInt32Table.
                        // But here's a bad news: although uint can represent 10^9, exp 9's binary representation is 1001.
                        // That means 10^(1011), 10^(1101), 10^(1111) all cannot be stored as uint, we cannot easily say something like:
                        // "Any bits <= 3 is small exp, any bits > 3 is big exp". So instead of involving 10^8, 10^9 to s_Pow10UInt32Table,
                        // consider 10^8 and 10^9 as a bigNum, so they fall into s_Pow10BigNumTable. Now we can have a simple rule:
                        // "Any bits <= 3 is small exp, any bits > 3 is big exp".
                        //
                        // For 0b111111, we first calculate 10^(smallExp), which is 10^(7), now we can shift right 3 bits, prepare to calculate the bigExp part,
                        // the exp now becomes 0b000111.
                        //
                        // Apparently the lowest bit of bigExp should represent 10^8 because we have already shifted 3 bits for smallExp, so s_Pow10BigNumTable[0] = 10^8.
                        // Now let's shift exp right 1 bit, the lowest bit should represent 10^(8 * 2) = 10^16, and so on...
                        //
                        // That's why we just need the values of s_Pow10BigNumTable be power of 2.
                        //
                        // More details of this implementation can be found at: https://github.com/dotnet/coreclr/pull/12894#discussion_r128890596

                        // Validate that `s_Pow10BigNumTable` has exactly enough trailing elements to fill a BigInteger (which contains MaxBlockCount + 1 elements)
                        // We validate here, since this is the only current consumer of the array
                        Debug.Assert((s_Pow10BigNumTableIndices[s_Pow10BigNumTableIndices.Length - 1] + MaxBlockCount + 2) == s_Pow10BigNumTable.Length);

                        Span<uint> temp1Blocks = stackalloc uint[BigInteger.MaxBlockCount];
                        var temp1 = new BigInteger(temp1Blocks);
                        temp1.SetUInt32(s_Pow10UInt32Table[exponent & 0x7]);
                        ref BigInteger lhs = ref temp1;

                        Span<uint> temp2Blocks = stackalloc uint[BigInteger.MaxBlockCount];
                        var temp2 = new BigInteger(temp2Blocks);
                        ref BigInteger product = ref temp2;

                        exponent >>= 3;
                        uint index = 0;

                        while (exponent != 0)
                        {
                            // If the current bit is set, multiply it with the corresponding power of 10
                            if ((exponent & 1) != 0)
                            {
                                var bigNumTableIndex = s_Pow10BigNumTableIndices[index];
                                var bigNumLength = (int)s_Pow10BigNumTable[bigNumTableIndex];
                                Span<uint> bigNumBlocks = s_Pow10BigNumTable.AsSpan(bigNumTableIndex + 1, bigNumLength);
                                var rhs = new BigInteger(bigNumBlocks, bigNumLength);
                                Multiply(in lhs, in rhs, ref product);

                                // Swap to the next temporary
                                ref BigInteger temp = ref product;
                                product = ref lhs;
                                lhs = ref temp;
                            }

                            // Advance to the next bit
                            ++index;
                            exponent >>= 1;
                        }

                        // This is equivalent to result.SetValue(in lhs). We can't call SetValue() because there's a chance lhs mey point
                        // to temp1 or temp2 and leak an interna stack reference and the compiler doesn't like this ;)
                        int rhsLength = lhs._length;
                        result._length = rhsLength;
                        lhs._blocks.Slice(0, rhsLength).CopyTo(result._blocks);
                    }

                    private static uint AddDivisor(in BigInteger lhs, int lhsStartIndex, in BigInteger rhs)
                    {
                        int lhsLength = lhs._length;
                        int rhsLength = rhs._length;

                        Debug.Assert(lhsLength >= 0);
                        Debug.Assert(rhsLength >= 0);
                        Debug.Assert(lhsLength >= rhsLength);

                        // Repairs the dividend, if the last subtract was too much

                        ulong carry = 0UL;

                        for (int i = 0; i < rhsLength; i++)
                        {
                            ref uint lhsValue = ref lhs._blocks[lhsStartIndex + i];

                            ulong digit = lhsValue + carry + rhs._blocks[i];
                            lhsValue = unchecked((uint)digit);
                            carry = digit >> 32;
                        }

                        return (uint)(carry);
                    }

                    private static bool DivideGuessTooBig(ulong q, ulong valHi, uint valLo, uint divHi, uint divLo)
                    {
                        Debug.Assert(q <= 0xFFFFFFFF);

                        // We multiply the two most significant limbs of the divisor
                        // with the current guess for the quotient. If those are bigger
                        // than the three most significant limbs of the current dividend
                        // we return true, which means the current guess is still too big.

                        ulong chkHi = divHi * q;
                        ulong chkLo = divLo * q;

                        chkHi += (chkLo >> 32);
                        chkLo &= uint.MaxValue;

                        if (chkHi < valHi)
                            return false;

                        if (chkHi > valHi)
                            return true;

                        if (chkLo < valLo)
                            return false;

                        if (chkLo > valLo)
                            return true;

                        return false;
                    }

                    private static uint SubtractDivisor(in BigInteger lhs, int lhsStartIndex, in BigInteger rhs, ulong q)
                    {
                        int lhsLength = lhs._length - lhsStartIndex;
                        int rhsLength = rhs._length;

                        Debug.Assert(lhsLength >= 0);
                        Debug.Assert(rhsLength >= 0);
                        Debug.Assert(lhsLength >= rhsLength);
                        Debug.Assert(q <= uint.MaxValue);

                        // Combines a subtract and a multiply operation, which is naturally
                        // more efficient than multiplying and then subtracting...

                        ulong carry = 0;

                        for (int i = 0; i < rhsLength; i++)
                        {
                            carry += rhs._blocks[i] * q;
                            uint digit = unchecked((uint)carry);
                            carry >>= 32;

                            ref uint lhsValue = ref lhs._blocks[lhsStartIndex + i];

                            if (lhsValue < digit)
                            {
                                carry++;
                            }

                            lhsValue = unchecked(lhsValue - digit);
                        }

                        return (uint)(carry);
                    }

                    public void Add(uint value)
                    {
                        int length = _length;
                        if (length == 0)
                        {
                            SetUInt32(value);
                            return;
                        }

                        _blocks[0] += value;
                        if (_blocks[0] >= value)
                        {
                            // No carry
                            return;
                        }

                        for (int index = 1; index < length; index++)
                        {
                            _blocks[index]++;
                            if (_blocks[index] > 0)
                            {
                                // No carry
                                return;
                            }
                        }

                        Debug.Assert(unchecked((uint)(length)) + 1 <= MaxBlockCount);
                        _blocks[length] = 1;
                        _length = length + 1;
                    }

                    public bool IsZero() => _length == 0;

                    public void Multiply(uint value) => Multiply(in this, value, ref this);

                    public void Multiply10()
                    {
                        if (IsZero())
                        {
                            return;
                        }

                        int index = 0;
                        int length = _length;
                        ulong carry = 0;

                        do
                        {
                            ulong block = (ulong)(_blocks[index]);
                            ulong product = (block << 3) + (block << 1) + carry;
                            carry = product >> 32;
                            _blocks[index] = (uint)(product);

                            index++;
                        } while (index < length);

                        if (carry != 0)
                        {
                            Debug.Assert(unchecked((uint)(_length)) + 1 <= MaxBlockCount);
                            _blocks[index] = (uint)carry;
                            _length++;
                        }
                    }

                    public void MultiplyPow10(uint exponent)
                    {
                        Debug.Assert(exponent <= 9);
                        Multiply(s_Pow10UInt32Table[exponent]);
                    }

                    public void SetUInt32(uint value)
                    {
                        if (value == 0)
                        {
                            SetZero();
                        }
                        else
                        {
                            this._blocks[0] = value;
                            this._length = 1;
                        }
                    }

                    public void SetUInt64(ulong value)
                    {
                        if (value <= uint.MaxValue)
                        {
                            SetUInt32((uint)(value));
                        }
                        else
                        {
                            this._blocks[0] = (uint)(value);
                            this._blocks[1] = (uint)(value >> 32);
                            this._length = 2;
                        }
                    }

                    public void SetValue(in BigInteger value)
                    {
                        int rhsLength = value._length;
                        this._length = rhsLength;
                        value._blocks.Slice(0, rhsLength).CopyTo(this._blocks);
                    }

                    public void SetZero() => _length = 0;

                    public void ShiftLeft(uint shift)
                    {
                        // Process blocks high to low so that we can safely process in place
                        int length = _length;

                        if ((length == 0) || (shift == 0))
                        {
                            return;
                        }

                        var blocksToShift = (int)DivRem32(shift, out uint remainingBitsToShift);

                        // Copy blocks from high to low
                        int readIndex = (length - 1);
                        int writeIndex = readIndex + blocksToShift;

                        // Check if the shift is block aligned
                        if (remainingBitsToShift == 0)
                        {
                            Debug.Assert(writeIndex < MaxBlockCount);

                            while (readIndex >= 0)
                            {
                                _blocks[writeIndex] = _blocks[readIndex];
                                readIndex--;
                                writeIndex--;
                            }

                            _length += blocksToShift;

                            // Zero the remaining low blocks
                            Clear(blocksToShift);
                        }
                        else
                        {
                            // We need an extra block for the partial shift
                            writeIndex++;
                            Debug.Assert(writeIndex < MaxBlockCount);

                            // Set the length to hold the shifted blocks
                            _length = writeIndex + 1;

                            // Output the initial blocks
                            uint lowBitsShift = (32 - remainingBitsToShift);
                            uint highBits = 0;
                            uint block = _blocks[readIndex];
                            uint lowBits = block >> (int)(lowBitsShift);
                            while (readIndex > 0)
                            {
                                _blocks[writeIndex] = highBits | lowBits;
                                highBits = block << (int)(remainingBitsToShift);

                                --readIndex;
                                --writeIndex;

                                block = _blocks[readIndex];
                                lowBits = block >> (int)lowBitsShift;
                            }

                            // Output the final blocks
                            _blocks[writeIndex] = highBits | lowBits;
                            _blocks[writeIndex - 1] = block << (int)(remainingBitsToShift);

                            // Zero the remaining low blocks
                            Clear(blocksToShift);

                            // Check if the terminating block has no set bits
                            if (_blocks[_length - 1] == 0)
                            {
                                _length--;
                            }
                        }
                    }

                    public uint ToUInt32() => (_length > 0) ? _blocks[0] : 0;

                    public ulong ToUInt64() => (_length > 1) ? ((ulong)(_blocks[1]) << 32) + _blocks[0] : (_length > 0) ? _blocks[0] : 0;

                    private void Clear(int length) => _blocks.Slice(0, length).Fill(0);

                    private static uint DivRem32(uint value, out uint remainder)
                    {
                        remainder = value & 31;
                        return value >> 5;
                    }
                }
            }
        }
    }   
}