using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Carambolas.Net
{
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Data: IEquatable<Data>
    {
        internal Data(byte channel, Memory memory)
        {
            this.channel = channel;
            this.buffer = memory ?? throw new ArgumentNullException(nameof(memory));
            this.length = memory.Length;
            this.version = memory.Version;
        }
       
        private readonly Memory buffer;
        private readonly long version;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Memory safe
        {
            get
            {
                if (buffer == null || buffer.Version != version)
                    throw new ObjectDisposedException(GetType().FullName);

                return buffer;
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly byte channel;
        public byte Channel => channel;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly int length;
        public int Length => length;

        public void CopyTo(byte[] destinationArray) => safe.CopyTo(destinationArray);
        public void CopyTo(byte[] destinationArray, int destinationIndex, int length) => safe.CopyTo(destinationArray, destinationIndex, length);
        public void CopyTo(int index, byte[] destinationArray, int destinationIndex, int length) => safe.CopyTo(index, destinationArray, destinationIndex, length);        

        public void Dispose()
        {
            if (buffer != null && buffer.Version == version)
                buffer.Dispose();
        }

        private static bool Equals(in Data a, in Data b) => a.buffer == b.buffer && a.version == b.version;

        public bool Equals(Data other) => Equals(in this, in other);

        public override int GetHashCode()
        {
            var h1 = buffer?.GetHashCode() ?? 0;
            var h2 = (int)(version ^ (version >> 32));
            return (int)((uint)(h1 << 5) | (uint)h1 >> 27) + h1 ^ h2;
        }

        public override bool Equals(object obj) => obj is Data other && Equals(in this, in other);

        public override string ToString() => $"{nameof(Data)} {{ {nameof(Channel)}: {Channel}, {nameof(Length)}: {Length} }}";

        public static bool operator ==(in Data a, in Data b) => Equals(in a, in b);
        public static bool operator !=(in Data a, in Data b) => !Equals(in a, in b);
    }   
}
