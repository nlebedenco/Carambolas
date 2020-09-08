using System;
using System.Runtime.InteropServices;

namespace Carambolas.Net
{
    public sealed partial class Host
    {
        public sealed class Stream
        {
            [StructLayout(LayoutKind.Auto)]
            public readonly struct Settings
            {
                public static readonly Settings Default = new Settings(16384, 0.5f);

                /// <summary>
                /// Buffer size in bytes.
                /// </summary>
                public readonly int BufferSize;

                /// <summary>
                /// A value in the range [0, 1] indicating the fraction of the buffer that can be used for user data.
                /// </summary>
                public readonly float BufferUtilization;

                public Settings(int bufferSize, float bufferUtilization) => (BufferSize, BufferUtilization) = (bufferSize, bufferUtilization);
            }

            /// <summary>
            ///  Maximum number of packets that can be processed in a single frame.
            /// </summary>
            public uint PacketRate = uint.MaxValue;

            /// <summary>
            /// Buffer size in bytes.
            /// </summary>
            public int BufferSize { get; private set; }

            /// <summary>
            /// A value in the range [0, 1] indicating the fraction of the buffer that can be used for user data.
            /// </summary>
            public float BufferUtilization
            {
                get => bufferUtilization;
                set
                {
                    bufferUtilization = Math.Max(0, Math.Min(value, 1));
                    OnChanged();
                }
            }
            private float bufferUtilization;

            /// <summary>
            /// Amount of buffer space in bytes alloted for each peer.
            /// </summary>
            public int BufferShare { get; private set; }

            /// <summary>
            /// Number of peers sharing the buffer.
            /// </summary>
            internal int Count
            {
                get => count;
                set
                {
                    count = value;
                    OnChanged();
                }
            }
            private int count;

            internal Stream() { }

            internal void Reset(in Settings settings) => Reset(settings.BufferSize, settings.BufferUtilization);
            internal void Reset(int bufferSize = default, float bufferUtilization = default)
            {
                this.BufferSize = bufferSize;
                this.bufferUtilization = bufferUtilization;

                count = default;
                BufferShare = default;
            }

            private void OnChanged() => BufferShare = (count == 0) ? 0 : (int)(BufferSize * bufferUtilization / count);
        }
    }
}
