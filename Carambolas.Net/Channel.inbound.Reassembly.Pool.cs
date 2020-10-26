using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Carambolas.Net
{
    internal partial struct Channel
    {
        public partial struct Inbound
        {
            public sealed partial class Reassembly
            {
                [DebuggerDisplay("Count = {queue.Count}")]
                public sealed class Pool: Pool<Reassembly>
                {
                    protected override Reassembly Create() => new Reassembly() { OnDisposed = Return };

                    protected override void OnReturn(Reassembly instance)
                    {
                        instance.SequenceNumber = default;

                        instance.Data?.Dispose();
                        instance.Data = null;
                    }
                }
            }
        }
    }
}
