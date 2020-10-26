using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Carambolas
{
    [DebuggerDisplay("Count = {queue.Count}")]
    public abstract class Pool<T>: IDisposable 
        where T : class
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private SpinLock queueLock = new SpinLock(false);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Queue<T> queue = new Queue<T>(64);

        protected virtual bool ShouldDisposeInstances => false;

        private int capacity;
        public int Capacity
        {
            get => capacity;
            set
            {
                if (capacity != value)
                {
                    capacity = value;
                    var locked = false;
                    try
                    {
                        queueLock.Enter(ref locked);
                        if (queue.Count < capacity)
                        {
                            var instance = queue.Dequeue();
                            if (ShouldDisposeInstances &&  instance is IDisposable disposable)
                                disposable.Dispose();
                        }
                    }
                    finally
                    {
                        if (locked)
                            queueLock.Exit(false);
                    }
                }
            }
        }

        public Pool(int capacity = int.MaxValue) => this.capacity = capacity;

        protected abstract T Create();

        protected virtual void OnTake(T instance) { }
        protected virtual void OnReturn(T instance) { }

        public T Take()
        {
            T instance;
            var locked = false;
            try
            {
                queueLock.Enter(ref locked);
                if ((queue ?? throw new ObjectDisposedException(GetType().FullName)).Count == 0)
                    goto Instantiate;

                instance = queue.Dequeue();
            }
            finally
            {
                if (locked)
                    queueLock.Exit(false);
            }

            OnTake(instance);
            return instance;

            Instantiate:
            return Create();
        }

        public void Return(T instance)
        {
            OnReturn(instance);

            if (queue != null) // nothing can be done if pool is already disposed.
            {
                var locked = false;
                try
                {
                    queueLock.Enter(ref locked);
                    if (queue.Count < capacity)
                        queue.Enqueue(instance);
                }
                finally
                {
                    if (locked)
                        queueLock.Exit(false);
                }
            }
        }

        public virtual void Dispose()
        {
            var locked = false;
            try
            {
                queueLock.Enter(ref locked);
                if (ShouldDisposeInstances)
                {
                    var enumerator = queue.GetEnumerator();
                    while (enumerator.MoveNext())
                        if (enumerator.Current is IDisposable disposable)
                            disposable.Dispose();
                }
                queue = null;
            }
            finally
            {
                if (locked)
                    queueLock.Exit(false);
            }            
        }
    }

    public class LinkedListNodePool<T>: Pool<LinkedListNode<T>> where T: class
    {
        public LinkedListNodePool(int capacity = int.MaxValue) : base(capacity) { }

        protected override LinkedListNode<T> Create() => new LinkedListNode<T>(default);
    }
}
