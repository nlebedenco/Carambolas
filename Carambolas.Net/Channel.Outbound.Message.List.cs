using System;
using System.Diagnostics;

namespace Carambolas.Net
{
    internal partial struct Channel
    {
        public partial struct Outbound
        {
            public sealed partial class Message
            {
                public struct List: IDisposable
                {
                    public bool IsEmpty => first == default;

                    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
                    private Message first;
                    public Message First
                    {
                        get => first;
                        set
                        {
                            first = value;
                            if (value == null)
                                last = null;
                        }
                    }

                    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
                    private Message last;
                    public Message Last => last;

                    public void AddLast(Message message)
                    {
                        message.AddAfter(last);
                        last = message;
                        if (first == null)
                            first = message;
                    }

                    public void AddLast(Message from, Message to)
                    {
                        from.Prev = last;
                        if (last != null)
                            last.Next = from;

                        last = to;

                        if (first == null)
                            first = from;
                    }

                    public void AddLast(in List other) => AddLast(other.first, other.last);

                    public void Dispose(Message message)
                    {
                        if (first == message)
                            first = message.Next;
                        if (last == message)
                            last = message.Prev;

                        message.Dispose();
                    }

                    public void Dispose()
                    {
                        var message = first;
                        while (message != null)
                        {
                            var next = message.Next;
                            Dispose(message);
                            message = next;
                        }

                        Clear();
                    }

                    public void Clear() => first = last = default;
                }
            }
        }
    }
}