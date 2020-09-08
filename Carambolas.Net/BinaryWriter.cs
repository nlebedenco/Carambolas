using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Carambolas.Net
{
    internal sealed class BinaryWriter
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
        public int Offset => begin;        
        public int Position => position;
        public int Count => position - begin;
        public int Available => end - position;
                        
        public BinaryWriter() : this(Array.Empty<byte>()) { }
        public BinaryWriter(byte[] buffer) : this(buffer, 0, buffer.Length) { }
        public BinaryWriter(byte[] buffer, int length) : this(buffer, 0, length) { }
        public BinaryWriter(byte[] buffer, int offset, int length) => Reset(buffer, offset, length);

        public void Reset() => position = begin;
        public void Reset(int length) => Reset(0, length);
        public void Reset(int offset, int length)
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));
            
            if (offset > buffer.Length - length)
                throw new ArgumentException(string.Format(SR.IndexOutOfRangeOrLengthIsGreaterThanBuffer, nameof(offset), nameof(length)), nameof(length));

            this.begin = offset;
            this.end = offset + length;
            this.position = offset;
        }

        public void Reset(byte[] buffer, int offset, int length)
        {
            this.buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            Reset(offset, length);            
        }


        public void Expand(int value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            if (end + value > buffer.Length)
                throw new ArgumentException(string.Format(SR.BinaryWriter.ExpandOverflow, value), nameof(value));

            UnsafeExpand(value);
        }

        internal void UnsafeExpand(int value) => end += value;


        public void Skip(int n)
        {
            if (n > 0)
            {
                Ensure(n);
                UnsafeSkip(n);
            }
        }

        internal void UnsafeSkip(int n) => position += n;


        public void Write(char value) => Write((ushort)value);

        public void Write(byte value)
        {
            Ensure(1);
            UnsafeWrite(value);
        }

        public void Write(short value) => Write((ushort)value);

        public void Write(ushort value)
        {
            Ensure(sizeof(ushort));
            UnsafeWrite(value);
        }

        public void Write(int value) => Write((uint)value);

        public void Write(uint value)
        {
            Ensure(sizeof(uint));
            UnsafeWrite(value);
        }

        public void Write(long value) => Write((ulong)value);

        public void Write(ulong value)
        {
            Ensure(sizeof(ulong));
            UnsafeWrite(value);            
        }      

        public void Write(float value) => Write(new FloatConverter { AsFloat = value }.AsInt32);

        public void Write(double value) => Write(new DoubleConverter { AsDouble = value }.AsInt64);

        public void Write(byte[] sourceArray, int length) => Write(sourceArray, 0, length);

        public void Write(byte[] sourceArray, int sourceIndex, int length)
        {
            Ensure(length);
            UnsafeWrite(sourceArray, sourceIndex, length);
        }

        public void Write(Guid value)
        {
            var union = new GuidConverter { Guid = value };
            Write(union.MSB);
            Write(union.LSB);
        }

        public void Write(in IPEndPoint endPoint)
        {
            Write(endPoint.Port);
            Write((byte)endPoint.Address.AddressFamily);
            if (endPoint.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                Write(endPoint.Address.IPv6PackedAddress0);
                Write(endPoint.Address.IPv6PackedAddress1);
            }
            else
            {
                Write(endPoint.Address.IPv4PackedAddress);
            }
        }

        internal void Write(Memory source) => Write(source, 0, source.Length);

        internal void Write(Memory source, int length) => Write(source, 0, length);

        internal void Write(Memory source, int sourceIndex, int length)
        {
            Ensure(length);
            UnsafeWrite(source, sourceIndex, length);
        }


        public bool TryWrite(char value) => TryWrite((ushort)value);

        public bool TryWrite(byte value)
        {
            if (Available < sizeof(byte))
                return false;

            UnsafeWrite(value);
            return true;
        }

        public bool TryWrite(short value) => TryWrite((ushort)value);

        public bool TryWrite(ushort value)
        {
            if (Available < sizeof(ushort))
                return false;

            UnsafeWrite(value);
            return true;
        }

        public bool TryWrite(int value) => TryWrite((uint)value);

        public bool TryWrite(uint value)
        {
            if (Available < sizeof(uint))
                return false;

            UnsafeWrite(value);
            return true;
        }

        public bool TryWrite(long value) => TryWrite((ulong)value);

        public bool TryWrite(ulong value)
        {
            if (Available < sizeof(ulong))
                return false;

            UnsafeWrite(value);
            return true;
        }
      
        public bool TryWrite(float value) => TryWrite(new FloatConverter { AsFloat = value }.AsInt32);

        public bool TryWrite(double value) => TryWrite(new DoubleConverter { AsDouble = value }.AsInt64);

        public bool TryWrite(byte[] sourceArray, int length) => TryWrite(sourceArray, 0, length);

        public bool TryWrite(byte[] sourceArray, int sourceIndex, int length)
        {
            if (Available < length)
                return false;

            UnsafeWrite(sourceArray, sourceIndex, length);
            return true;
        }       

        public bool TryWrite(Guid value)
        {
            var union = new GuidConverter { Guid = value };
            return TryWrite(union.MSB) && TryWrite(union.LSB);
        }

        public bool TryWrite(in IPEndPoint endPoint) => TryWrite(endPoint.Port) 
            && TryWrite((byte)endPoint.Address.AddressFamily)
            && (endPoint.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                ? (TryWrite(endPoint.Address.IPv6PackedAddress0) && TryWrite(endPoint.Address.IPv6PackedAddress1))
                : TryWrite(endPoint.Address.IPv4PackedAddress);

        internal bool TryWrite(Memory source) => TryWrite(source, 0, source.Length);

        internal bool TryWrite(Memory source, int length) => TryWrite(source, 0, length);

        internal bool TryWrite(Memory source, int sourceIndex, int length)
        {
            if (Available < length)
                return false;

            UnsafeWrite(source, sourceIndex, length);
            return true;
        }



        internal void Ensure(int n)
        {
            if (Available < n)
                throw new InvalidOperationException(string.Format(SR.BinaryWriter.InsuficientSpace, Available, n));
        }


        internal void UnsafeWrite(byte value)
        {
            buffer[position] = value;
            position += 1;
        }

        internal void UnsafeWrite(byte value, int count)
        {
            var i = position;
            while (count > 0)
            {
                buffer[i++] = value;
                count--;
            }
                
            position = i;
        }

        internal void UnsafeWrite(ushort value)
        {
            var i = position;
            buffer[position] = (byte)((value >> 8) & 0xff);
            buffer[position + 1] = (byte)(value & 0xff);
            position = i + 2;
        }

        internal void UnsafeWrite(uint value)
        {
            var i = position;
            buffer[i] = (byte)((value >> 24) & 0xff);
            buffer[i + 1] = (byte)((value >> 16) & 0xff);
            buffer[i + 2] = (byte)((value >> 8) & 0xff);
            buffer[i + 3] = (byte)(value & 0xff);
            position = i + 4;
        }

        internal void UnsafeWrite(ulong value)
        {
            var i = position;
            buffer[i] = (byte)((value >> 56) & 0xff);
            buffer[i + 1] = (byte)((value >> 48) & 0xff);
            buffer[i + 2] = (byte)((value >> 40) & 0xff);
            buffer[i + 3] = (byte)((value >> 32) & 0xff);
            buffer[i + 4] = (byte)((value >> 24) & 0xff);
            buffer[i + 5] = (byte)((value >> 16) & 0xff);
            buffer[i + 6] = (byte)((value >> 8) & 0xff);
            buffer[i + 7] = (byte)(value & 0xff);
            position = i + 8;
        }

        internal void UnsafeWrite(byte[] sourceArray, int sourceIndex, int length)
        {
            Array.Copy(sourceArray, sourceIndex, buffer, position, length);
            position += length;
        }

        internal void UnsafeWrite(Memory source, int sourceIndex, int length)
        {
            source.CopyTo(sourceIndex, buffer, position, length);
            position += length;
        }


        internal void UnsafeOverwrite(byte value, int index)
        {
            buffer[index] = (byte)(value & 0xff);
        }

        internal void UnsafeOverwrite(ushort value, int index)
        {
            buffer[index] = (byte)((value >> 8) & 0xff);
            buffer[index + 1] = (byte)(value & 0xff);
        }
    }
}
