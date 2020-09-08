using System;
using System.Collections.Generic;
using System.Text;

namespace Carambolas.Net
{
    internal partial struct Channel
    {
        public partial struct Inbound
        {
            public abstract partial class Node: IDisposable
            {
                private Node parent;
                private Node left;
                private Node right;
                private bool red;

                public Protocol.Ordinal SequenceNumber;

                public virtual void Dispose() { }
            }
        }
    }
}
