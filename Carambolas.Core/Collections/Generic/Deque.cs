using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Resources = Carambolas.Internal.Resources;
using Strings = Carambolas.Internal.Strings;

namespace Carambolas.Collections.Generic
{
    /// <summary>
    /// A genetic Deque class implemented as a circular buffer offering O(1) random access 
    /// and O(1) append on both ends. Insertions and Clear are O(n).
    /// </summary>
    public class Deque<T>: IList<T>
    {
        private static int RecommendedCapacity(int current, int required)
        {
            // 2146435071 is the maximum size allowed for a single array dimmension in .NET
            // according to https://docs.microsoft.com/en-us/dotnet/api/system.array?redirectedfrom=MSDN&view=netstandard-2.0)
            // Even if gcAllowVeryLargeObjects we would have to provide overloads supporting long for every method that may index 
            // the internal char buffer array and some operations such as remove, replace and insert would take a considerable 
            // amount of time uder such large arrays.

            if (required > 2146435071)
                throw new ArgumentOutOfRangeException(nameof(required));

            int value;
            if (current == 0)
                value = DefaultCapacity;
            else if (current < (2146435071 >> 1))
                value = current << 1;
            else
                value = 2146435071;

            if (value < required)
                value = required;

            return value;
        }

        private const int DefaultCapacity = 16;

        private T[] buffer;
        private int version;

        private int offset;
        private int count;

        public T this[int index]
        {
            get => Get(index);

            set
            {
                if (index < 0 || index >= count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                buffer[(offset + index) % buffer.Length] = value;
            }
        }

        private ref T Get(int index)
        {
            if (index < 0 || index >= count)
                throw new IndexOutOfRangeException();

            return ref buffer[(offset + index) % buffer.Length];
        }

        bool ICollection<T>.IsReadOnly => false;

        public Deque() => buffer = Array.Empty<T>();

        public Deque(int capacity)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            buffer = capacity > 0 ? new T[capacity] : Array.Empty<T>();
        }

        public Deque(ICollection<T> collection)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            buffer = Array.Empty<T>();
            RequireCapacity(collection.Count);
            foreach (var value in collection)
                PushBack(value);
        }

        public Deque(IEnumerable<T> enumerable)
        {
            buffer = Array.Empty<T>();
            RequireCapacity(enumerable.Count());
            foreach (var value in enumerable)
                PushBack(value);
        }

        public int Capacity => buffer.Length;

        public int Count => count;

        public void EnsureCapacity(int value)
        {
            var current = buffer;
            if (value > current.Length)
            {
                var rented = new T[value];
                CopyTo(rented, 0);
                buffer = rented;
                offset = 0;
            }
        }

        public void TrimExcess()
        {
            var current = buffer;
            if (count < current.Length)
            {
                if (count > 0)
                {
                    var rented = new T[count];
                    CopyTo(rented, 0);
                    buffer = rented;
                }
                else
                {
                    buffer = Array.Empty<T>();
                }

                offset = 0;
            }
        }

        private void RequireCapacity(int min)
        {

            var current = buffer;
            if (min > current.Length)
            {
                var rented = new T[RecommendedCapacity(current.Length, min)];
                CopyTo(rented, 0);
                buffer = rented;
                offset = 0;
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            if (arrayIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));

            if (count > array.Length - arrayIndex)
                throw new ArgumentException(string.Format(Resources.GetString(Strings.IndexOutOfRangeOrNumberOfElementsIsLessThanMinimum), nameof(arrayIndex), count, nameof(array)), nameof(array));

            if (count > 0)
            {
                if (offset + count < buffer.Length)
                    Array.Copy(buffer, offset, array, arrayIndex, count);
                else
                {
                    var m = buffer.Length - offset;
                    Array.Copy(buffer, offset, array, arrayIndex, m);
                    Array.Copy(buffer, 0, array, arrayIndex + m, count - m);
                }
            }
        }

        public void Add(T item) => PushBack(item);

        public void Clear()
        {
            if (count > 0)
            {
                if (offset + count < buffer.Length)
                    Array.Clear(buffer, offset, count);
                else
                {
                    var m = buffer.Length - offset;
                    Array.Clear(buffer, offset, m);
                    Array.Clear(buffer, 0, count - m);
                }
            }

            count = 0;
            offset = 0;
            version++;
        }

        public bool Contains(T item) => IndexOf(item) >= 0;

        public int IndexOf(T item)
        {
            for (int i = 0; i < count; ++i)
                if (EqualityComparer<T>.Default.Equals(Get(i), item))
                    return i;

            return -1;
        }

        public bool Remove(T item)
        {
            var index = IndexOf(item);
            if (index < 0)
                return false;

            RemoveAt(index);
            return true;
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= count)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (index == 0)
            {
                PopFront();
                return;
            }

            if (index == count - 1)
            {
                PopBack();
                return;
            }

            if (index < offset)
            {
                var n = count - buffer.Length + offset - 1;
                for (int i = index; i < n; ++i)
                    buffer[i] = buffer[i + 1];
            }
            else
            {
                for (int i = index; i > offset; --i)
                    buffer[i] = buffer[i - 1];
                buffer[offset] = default;
                offset = (offset + 1) % buffer.Length;
            }

            count--;
            version++;
        }

        public void Insert(int index, T item)
        {
            if (index == 0)
            {
                PushFront(item);
                return;
            }

            if (index == Count)
            {
                PushBack(item);
                return;
            }

            RequireCapacity(count + 1);
            if (index < offset)
            {

            }
            else
            {

            }

            count++;
            version++;
        }

        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void PushFront(T item)
        {
            RequireCapacity(count + 1);
            var n = buffer.Length;
            var i = (offset + n - 1) % n;
            buffer[i] = item;
            count++;
            offset = i;
            version++;
        }

        public void PushBack(T item)
        {
            RequireCapacity(count + 1);
            var n = buffer.Length;
            var i = (offset + count) % n;
            buffer[i] = item;
            count++;
            version++;
        }

        public T PopFront()
        {
            if (count == 0)
                throw new InvalidOperationException(Resources.GetString(Strings.CollectionIsEmpty));

            var n = buffer.Length;
            var i = offset;
            var value = buffer[i];
            buffer[i] = default;
            offset = (i + 1) % n;
            count--;
            version++;
            return value;
        }
        
        public T PopBack()
        {
            if (count == 0)
                throw new InvalidOperationException(Resources.GetString(Strings.CollectionIsEmpty));

            var n = buffer.Length;
            var i = (offset + count - 1) % n;
            var value = buffer[i];
            buffer[i] = default;
            count--;
            version++;
            return value;
        }

        public struct Enumerator: IEnumerator<T>
        {
            private readonly Deque<T> deque;
            private readonly int version;

            private int index;
            private T current;

            internal Enumerator(Deque<T> deque)
            {
                this.deque = deque;
                index = 0;
                version = deque.version;
                current = default;
            }

            public void Dispose() { }

            public bool MoveNext()
            {
                if (version != deque.version || (uint)index >= (uint)deque.Count)
                    return MoveNextRare();
                current = deque[index];
                ++index;
                return true;
            }

            private bool MoveNextRare()
            {
                if (version != deque.version)
                    throw new InvalidOperationException(Resources.GetString(Strings.EnumeratorFailedVersion));
                index = deque.Count + 1;
                current = default;
                return false;
            }

            public T Current => current;

            object IEnumerator.Current
            {
                get
                {
                    if (index == 0 || index == deque.Count + 1)
                        throw new InvalidOperationException(Resources.GetString(Strings.EnumeratorNotStartedOrFinished));

                    return current;
                }
            }

            void IEnumerator.Reset()
            {
                if (version != deque.version)
                    throw new InvalidOperationException(Resources.GetString(Strings.EnumeratorFailedVersion));

                index = 0;
                current = default;
            }
        }
    }
}
