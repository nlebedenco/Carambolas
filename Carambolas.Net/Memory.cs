using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Carambolas.Net
{
    /// <summary>
    /// A pooled byte buffer.
    /// </summary>
    [DebuggerDisplay("Length = {Length}")]
    internal sealed class Memory
    {
        internal const int BlockCount = 4;

        [DebuggerDisplay("Count = {queue.Count}")]
        internal sealed class Pool: IDisposable
        {
            [DebuggerDisplay("BlockSize = {BlockSize}; Count = {queue.Count}")]
            public sealed class Level: IDisposable
            {
                [DebuggerBrowsable(DebuggerBrowsableState.Never)]
                private SpinLock queueLock = new SpinLock(false);

                [DebuggerBrowsable(DebuggerBrowsableState.Never)]
                private Queue<byte[]> queue = new Queue<byte[]>(32);

                [DebuggerBrowsable(DebuggerBrowsableState.Never)]
                public readonly ushort BlockSize;

                public Level(ushort blockSize) => BlockSize = blockSize;

                public byte[] Get()
                {
                    var locked = false;
                    try
                    {
                        queueLock.Enter(ref locked);
                        if ((queue ?? throw new ObjectDisposedException(GetType().FullName)).Count > 0)
                            return queue.Dequeue();
                    }
                    finally
                    {
                        if (locked)
                            queueLock.Exit(false);
                    }

                    return new byte[BlockSize];
                }

                public void Return(byte[] block)
                {
                    if (queue != null)
                    {
                        var locked = false;
                        try
                        {
                            queueLock.Enter(ref locked);
                            queue.Enqueue(block);
                        }
                        finally
                        {
                            if (locked)
                                queueLock.Exit(false);
                        }
                    }
                }

                public void Return(byte[][] blocks)
                {
                    if (queue != null)
                    {
                        var locked = false;
                        try
                        {
                            queueLock.Enter(ref locked);
                            for (int i = 0; i < blocks.Length; ++i)
                            {
                                var block = blocks[i];
                                if (block == null)
                                    break;

                                queue.Enqueue(block);
                                blocks[i] = null;
                            }
                        }
                        finally
                        {
                            if (locked)
                                queueLock.Exit(false);
                        }
                    }
                }

                public void Dispose() => queue = null;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private SpinLock queueLock = new SpinLock(false);

            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private Queue<Memory> queue = new Queue<Memory>(32);

            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            public readonly Level[] Levels = { new Level(64), new Level(256), new Level(1024), new Level(4096), new Level(16384) };

            public Memory Get()
            {
                var locked = false;                
                try
                {
                    queueLock.Enter(ref locked);
                    if ((queue ?? throw new ObjectDisposedException(GetType().FullName)).Count > 0)
                        return queue.Dequeue();
                }
                finally
                {
                    if (locked)
                        queueLock.Exit(false);
                }

                return new Memory(this);
            }

            public void Return(Memory instance)
            {
                instance.Version++;
                instance.Capacity = 0;
                instance.length = 0;

                instance.level?.Return(instance.blocks);
                instance.level = default;

                if (queue != null)
                {
                    var locked = false;                    
                    try
                    {
                        queueLock.Enter(ref locked);
                        queue.Enqueue(instance);
                    }
                    finally
                    {
                        if (locked)
                            queueLock.Exit(false);
                    }
                }
            }

            public void Dispose()
            {
                queue = null;
                for (int i = 0; i < Levels.Length; ++i)
                {
                    Levels[i]?.Dispose();
                    Levels[i] = null;
                }
            }
        }

        private Memory(Pool pool) => this.pool = pool;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly byte[][] blocks = new byte[BlockCount][];

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Pool pool;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Pool.Level level;

        internal ushort BlockSize => level.BlockSize;

        internal long Version { get; private set; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public int Capacity { get; private set; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int length;
        public int Length
        {
            get => length;
            set
            {
                EnsureCapacity(value);
                length = value;
            }
        }

        private void EnsureCapacity(int value)
        {
            if (value < 0 || value > (ushort.MaxValue + 1))
                throw new ArgumentOutOfRangeException(nameof(value));

            if (value == 0)
                return;

            if (level == null)
            {
                level = GetLevel(value);
                var n = ((value - 1) / BlockSize) + 1;
                for (int i = 0; i < n; ++i)
                    blocks[i] = level.Get();

                Capacity = n * level.BlockSize;
            }
            else
            {
                if (Capacity < value)
                {
                    if ((level.BlockSize << 2) < value)
                    {
                        var next = GetLevel(value);
                        var block = next.Get();

                        CopyTo(block);
                        level.Return(blocks);
                        level = next;
                        blocks[0] = block;
                        var n = ((value - 1) / BlockSize) + 1;
                        for (int i = 1; i < n; ++i)
                            blocks[i] = level.Get();

                        Capacity = n * level.BlockSize;
                    }
                    else
                    {
                        var n = ((value - 1) / BlockSize) + 1;
                        for (int i = 0; i < n; ++i)
                            if (blocks[i] == null)
                                blocks[i] = level.Get();

                        Capacity = n * level.BlockSize;
                    }
                }
            }
        }

        private Pool.Level GetLevel(int value)
        {
            if (value < 256)
                return pool.Levels[0];

            if (value < 1024)
                return pool.Levels[1];

            if (value < 4096)
                return pool.Levels[2];

            if (value < 16384)
                return pool.Levels[3];

            return pool.Levels[4];
        }

        public void Dispose() => pool.Return(this);

        public byte this[int index]
        {
            get
            {
                if (index < 0 || index >= Length)
                    throw new ArgumentOutOfRangeException(nameof(index));

                return blocks[index / BlockSize][index % BlockSize];
            }

            set
            {
                if (index < 0 || index > ushort.MaxValue)
                    throw new ArgumentOutOfRangeException(nameof(index));

                var minlen = index + 1;
                if (Length < minlen)
                    Length = minlen;
                blocks[index / BlockSize][index % BlockSize] = value;
            }
        }

        public void CopyFrom(in ArraySegment<byte> source) => CopyFrom(source.Array, source.Offset, 0, source.Count);
        public void CopyFrom(in ArraySegment<byte> source, int destinationIndex) => CopyFrom(source.Array, source.Offset, destinationIndex, source.Count);

        public void CopyFrom(byte[] sourceArray) => CopyFrom(sourceArray, 0, 0, sourceArray.Length);
        public void CopyFrom(byte[] sourceArray, int sourceIndex, int length) => CopyFrom(sourceArray, sourceIndex, 0, length);        

        public void CopyFrom(byte[] sourceArray, int sourceIndex, int destinationIndex, int length)
        {
            if (sourceArray == null)
                throw new ArgumentNullException(nameof(sourceArray));

            if (destinationIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(destinationIndex));

            if (sourceIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(sourceIndex));

            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (length > 0)
            {
                if (sourceIndex > (sourceArray.Length - length))                    
                    throw new ArgumentException(string.Format(Resources.GetString(Strings.IndexOutOfRangeOrLengthIsGreaterThanNumberOfElements), nameof(sourceIndex), nameof(length), nameof(sourceArray)), nameof(length));

                var firstIndex = destinationIndex;
                var lastIndex = firstIndex + length - 1;

                var minlen = lastIndex + 1;
                if (Length < minlen)
                    Length = minlen;

                var from = firstIndex / BlockSize;
                var to = lastIndex / BlockSize;
                var size = Math.Min(length, BlockSize - firstIndex % BlockSize);
                var block = blocks[from];

                Array.Copy(sourceArray, sourceIndex, block, firstIndex % BlockSize, size);
                length -= size;
                sourceIndex += size;

                for (var i = from + 1; i <= to; ++i)
                {
                    size = Math.Min(length, BlockSize);
                    block = blocks[i];
                    Array.Copy(sourceArray, sourceIndex, block, 0, size);
                    length -= size;
                    sourceIndex += size;
                }
            }
        }

        public void CopyTo(byte[] destinationArray) => CopyTo(0, destinationArray, 0, Length);
        public void CopyTo(byte[] destinationArray, int destinationIndex, int length) => CopyTo(0, destinationArray, destinationIndex, length);

        public void CopyTo(int sourceIndex, byte[] destinationArray, int destinationIndex, int length)
        {
            if (destinationArray == null)
                throw new ArgumentNullException(nameof(destinationArray));

            if (sourceIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(sourceIndex));

            if (destinationIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(destinationIndex));

            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (length > 0)
            {
                if (destinationIndex > (destinationArray.Length - length))
                    throw new ArgumentException(string.Format(Resources.GetString(Strings.IndexOutOfRangeOrLengthIsGreaterThanNumberOfElements), nameof(destinationIndex), nameof(length), nameof(destinationArray)), nameof(destinationArray));

                if (sourceIndex > (Length - length))
                    throw new ArgumentException(string.Format(Resources.GetString(Strings.IndexOutOfRangeOrLengthIsGreaterThanBuffer), nameof(sourceIndex), nameof(length)), nameof(length));

                var firstIndex = sourceIndex;
                var lastIndex = firstIndex + length - 1;

                var from = firstIndex / BlockSize;
                var to = lastIndex / BlockSize;
                var size = Math.Min(length, BlockSize - firstIndex % BlockSize);
                var block = blocks[from];
                Array.Copy(block, firstIndex % BlockSize, destinationArray, destinationIndex, size);
                length -= size;
                destinationIndex += size;

                for (var i = from + 1; i <= to; ++i)
                {
                    size = Math.Min(length, BlockSize);
                    block = blocks[i];
                    Array.Copy(block, 0, destinationArray, destinationIndex, size);
                    length -= size;
                    destinationIndex += size;
                }
            }
        }

        #region Unchecked overwrites 

        /// <summary>
        /// Unchecked overwrite used to fix encoded outbound messages on transmission. 
        /// Inelegant but effective. Use with caution.
        /// </summary>
        internal void UncheckedOverwrite(byte value, int index) => blocks[0][index] = value;

        /// <summary>
        /// Unchecked overwrite used to fix encoded outbound messages on transmission. 
        /// Inelegant but effective. Use with caution.
        /// </summary>
        internal void UncheckedOverwrite(ushort value, int index)
        {
            blocks[0][index] = (byte)((value >> 8) & 0xff);
            blocks[0][index + 1] = (byte)(value & 0xff);
        }

        #endregion
    }
}
