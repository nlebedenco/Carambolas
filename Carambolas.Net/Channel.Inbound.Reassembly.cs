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
            public sealed partial class Reassembly: Node
            {
                private Reassembly() { }

                /// <summary>
                /// Last fragment index.
                /// </summary>
                public byte Last;

                public Memory Data;

                public override void Dispose() => Return(this);

                [DebuggerBrowsable(DebuggerBrowsableState.Never)]
                private Action<Reassembly> Return;
            }
        }
    }
}
