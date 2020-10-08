using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Carambolas.Net
{
    internal class BinaryReader
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private byte[] buffer;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int begin;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int end;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int position;
        
        public byte[] Buffer => buffer;

        public int Position => position;
        public int Available => end - position;

        public BinaryReader() : this(Array.Empty<byte>()) { }
        public BinaryReader(byte[] buffer) : this(buffer, 0, buffer.Length) { }
        public BinaryReader(byte[] buffer, int length) : this(buffer, 0, length) { }
        public BinaryReader(byte[] buffer, int index, int length) => Reset(buffer, index, length);

        public void Reset(int offset, int length)
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (offset > buffer.Length - length)
                throw new ArgumentException(string.Format(SR.IndexOutOfRangeOrLengthIsGreaterThanBuffer, nameof(offset), nameof(length)), nameof(length));

            UncheckedReset(offset, length);
        }

        public void Reset(byte[] buffer, int offset, int length)
        {
            this.buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            Reset(offset, length);
        }

        internal void UncheckedReset(int offset, int length)
        {
            this.begin = offset;
            this.end = offset + length;
            this.position = begin;
        }


        public void Truncate(int value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            if (end - value < 0)
                throw new ArgumentException(string.Format(SR.BinaryReader.TruncateUnderflow, value), nameof(value));

            UncheckedTruncate(value);
        }

        internal void UncheckedTruncate(int value) => end -= value;


        public void Skip(int n)
        {
            if (n > 0)
            {
                Ensure(n);
                UncheckedSkip(n);
            }
        }

        internal void UncheckedSkip(int n) => position += n;


        public void Read(out char value)
        {
            Read(out ushort v);
            value = (char)v;
        }

        public void Read(out byte value)
        {
            Ensure(1);
            value = buffer[position++];
        }

        public void Read(out short value)
        {
            Read(out ushort v);
            value = (short)v;
        }

        public void Read(out ushort value)
        {
            Ensure(sizeof(ushort));
            UncheckedRead(out value);
        }

        public void Read(out int value)
        {
            Read(out uint v);
            value = (int)v;
        }

        public void Read(out uint value)
        {
            Ensure(sizeof(uint));
            UncheckedRead(out value);
        }

        public void Read(out long value)
        {
            Read(out ulong v);
            value = (long)v;
        }

        public void Read(out ulong value)
        {
            Ensure(sizeof(ulong));
            UncheckedRead(out value);
        }

        public void Read(out float value)
        {
            Read(out int v);
            value = new FloatConverter { AsInt32 = v }.AsFloat;
        }

        public void Read(out double value)
        {
            Read(out long v);
            value = new DoubleConverter { AsInt64 = v }.AsDouble;
        }

        public void Read(byte[] destinationArray, int length) => Read(destinationArray, 0, length);        

        public void Read(byte[] destinationArray, int destinationIndex, int length)
        {
            Ensure(length);
            UncheckedRead(destinationArray, destinationIndex, length);
        }       

        public void Read(out Guid value)
        {
            var union = default(GuidConverter);
            Read(out union.MSB);
            Read(out union.LSB);
            value = union.Guid;
        }

        public void Read(out IPEndPoint value)
        {
            Read(out ushort port);
            Read(out byte addressFamily);
            if (addressFamily == (byte)System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                Read(out ulong a);
                Read(out ulong b);
                value = new IPEndPoint(new IPAddress(a, b), port);
            }
            else
            {
                Read(out uint a);
                value = new IPEndPoint(new IPAddress(a), port);
            }
        }

        internal void Read(Memory destination, int destinationIndex, int length)
        {
            Ensure(length);
            UncheckedRead(destination, destinationIndex, length);
        }


        public bool TryRead(out char value)
        {
            if (TryRead(out ushort v))
            {
                value = (char)v;
                return true;
            }

            value = default;
            return false;    
        }

        public bool TryRead(out byte value)
        {
            if (Available < 1)
            {
                value = default;
                return false;
            }

            value = buffer[position++];
            return true;
        }

        public bool TryRead(out short value)
        {
            if (TryRead(out ushort v))
            {
                value = (short)v;
                return true;
            }

            value = default;
            return false;
        }

        public bool TryRead(out ushort value)
        {
            if (Available < 2)
            {
                value = default;
                return false;
            }

            UncheckedRead(out value);
            return true;
        }

        public bool TryRead(out int value)
        {
            if (TryRead(out uint v))
            {
                value = (int)v;
                return true;
            }

            value = default;
            return false;
        }

        public bool TryRead(out uint value)
        {
            if (Available < 4)
            {
                value = default;
                return false;
            }

            UncheckedRead(out value);
            return true;
        }

        public bool TryRead(out long value)
        {
            if (TryRead(out ulong v))
            {
                value = (long)v;
                return true;
            }

            value = default;
            return false;
        }

        public bool TryRead(out ulong value)
        {
            if (Available < 8)
            {
                value = default;
                return false;
            }

            UncheckedRead(out value);
            return true;
        }
      
        public bool TryRead(out float value)
        {
            if (TryRead(out int v))
            {
                value = new FloatConverter { AsInt32 = v }.AsFloat;
                return true;
            }

            value = default;
            return false;
        }

        public bool TryRead(out double value)
        {
            if (TryRead(out long v))
            {
                value = new DoubleConverter { AsInt64 = v }.AsDouble;
                return true;
            }

            value = default;
            return false;
        }

        public bool TryRead(byte[] destinationArray, int length) => TryRead(destinationArray, 0, length);

        public bool TryRead(byte[] destinationArray, int destinationIndex, int length)
        {
            if (Available < length)
                return false;

            UncheckedRead(destinationArray, destinationIndex, length);
            return true;
        }
            
        public bool TryRead(out Guid value)
        {
            var context = position;
            var union = default(GuidConverter);            
            if (TryRead(out union.MSB) && TryRead(out union.LSB))
            {
                value = union.Guid;
                return true;
            }

            (value, position) = (default, context);
            return false;
        }

        public bool TryRead(out IPEndPoint value)
        {
            var context = position;
            if (TryRead(out ushort port) && TryRead(out byte addressFamily))
            {
                if (addressFamily == (byte)System.Net.Sockets.AddressFamily.InterNetworkV6)
                {
                    if (TryRead(out ulong a) && TryRead(out ulong b))
                    {
                        value = new IPEndPoint(new IPAddress(a, b), port);
                        return true;
                    }
                }
                else
                {
                    if (TryRead(out uint a))
                    { 
                        value = new IPEndPoint(new IPAddress(a), port);
                        return true;
                    }
                }
            }

            (value, position) = (default, context);
            return false;
        }

        internal bool TryRead(Memory destination, int destinationIndex, int length)
        {
            if (Available < length)
                return false;

            UncheckedRead(destination, destinationIndex, length);
            return true;
        }


        internal void Ensure(int n)
        {
            if (Available < n)
                throw new InvalidOperationException(string.Format(SR.BinaryReader.InsuficientData, Available, n));
        }


        internal void UncheckedRead(out byte value)
        {
            value = buffer[position++];
        }

        internal void UncheckedRead(out ushort value)
        {
            var i = position;
            value = (ushort)((buffer[i] << 8) | buffer[i + 1]);
            position = i + 2;
        }

        internal void UncheckedRead(out uint value)
        {
            var i = position;
            value = (uint)((buffer[i] << 24) | (buffer[i + 1] << 16) | (buffer[i + 2] << 8) | buffer[i + 3]);
            position = i + 4;
        }

        internal void UncheckedRead(out ulong value)
        {
            var i = position;
            value = (ulong)((buffer[i] << 56) | (buffer[i + 1] << 48) | (buffer[i + 2] << 40) | (buffer[i + 3] << 32) | (buffer[i + 4] << 24) | (buffer[i + 5] << 16) | (buffer[i + 6] << 8) | buffer[i + 7]);
            position = i + 8;
        }

        internal void UncheckedRead(byte[] destinationArray, int destinationIndex, int length)
        {
            Array.Copy(buffer, position, destinationArray, destinationIndex, length);
            position += length;
        }

        internal void UncheckedRead(Memory destination, int destinationIndex, int length)
        {
            destination.CopyFrom(buffer, position, destinationIndex, length);
            position += length;
        }

    }
}
