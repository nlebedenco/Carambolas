using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

using Carambolas.Internal;

namespace Carambolas.Collections.Generic
{
    /// <summary>
    /// A hybrid collection that behaves both as a dictionary and as a sorted list.
    /// </summary>
    [DebuggerDisplay("Count = {Count}")]
    [DebuggerTypeProxy(typeof(OrderedDictionaryDebugView<,>))]
    public class SortedList<TKey, TValue>: IDictionary<TKey, TValue>, IList<KeyValuePair<TKey, TValue>>
    {
        private readonly List<TKey> keys;
        private readonly List<TValue> values;

        private readonly ReadOnlyCollection<TKey> keysAsReadOnly;
        private readonly ReadOnlyCollection<TValue> valuesAsReadOnly;

        private readonly IComparer<TKey> comparer;

        private int version;

        public SortedList() : this(0, default) { }
        public SortedList(IComparer<TKey> comparer) : this(0, default) { }
        public SortedList(IDictionary<TKey, TValue> dictionary) : this(dictionary, default) { }
        public SortedList(int capacity) : this(capacity, default) { }
        public SortedList(IDictionary<TKey, TValue> dictionary, IComparer<TKey> comparer) : this(dictionary.Count, comparer)
        {
            foreach (var kv in dictionary)
                Add(kv.Key, kv.Value);
        }

        public SortedList(int capacity, IComparer<TKey> comparer)
        {
            keys = new List<TKey>(capacity);
            values = new List<TValue>(capacity);

            keysAsReadOnly = new ReadOnlyCollection<TKey>(keys);
            valuesAsReadOnly = new ReadOnlyCollection<TValue>(values);

            this.comparer = comparer;
        }

        private int Search(TKey key) => keys.BinarySearch(key, comparer);

        public TValue this[TKey key]
        {
            get
            {
                var i = Search(key);
                if (i < 0)
                    throw new KeyNotFoundException(Resources.GetString(Strings.KeyNotFound));

                return values[i];
            }

            set
            {
                var i = Search(key);
                if (i >= 0)
                    values[i] = value;
                else
                {
                    i = ~i;
                    keys.Insert(i, key);
                    values.Insert(i, value);
                    ++version;
                }
            }
        }

        KeyValuePair<TKey, TValue> IList<KeyValuePair<TKey, TValue>>.this[int index]
        {
            get => new KeyValuePair<TKey, TValue>(keys[index], values[index]);

            set => throw new NotSupportedException();
        }

        public int Count => keys.Count;

        public int Capacity
        {
            get => keys.Capacity;
            set => keys.Capacity = values.Capacity = value;
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

        public IComparer<TKey> Comparer => comparer;
        
        public ReadOnlyCollection<TKey> Keys => keysAsReadOnly;
        public ReadOnlyCollection<TValue> Values => valuesAsReadOnly;

        ICollection<TKey>   IDictionary<TKey, TValue>.Keys => keysAsReadOnly;
        ICollection<TValue> IDictionary<TKey, TValue>.Values => valuesAsReadOnly;

        public void Add(TKey key, TValue value)
        {
            var i = Search(key);
            if (i >= 0)
                throw new ArgumentException(Resources.GetString(Strings.DuplicateKey), nameof(key));

            i = ~i;
            keys.Insert(i, key);
            values.Insert(i, value);
            ++version;
        }

        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

        public void Clear()
        {
            keys.Clear();
            values.Clear();
            ++version;
        }

        public bool ContainsKey(TKey key) => Search(key) >= 0;

        public bool ContainsValue(TValue value) => values.Contains(value);

        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
        {
            var i = Search(item.Key);
            if (i < 0)
                return false;

            return EqualityComparer<TValue>.Default.Equals(values[i], item.Value);
        }

        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        int IList<KeyValuePair<TKey, TValue>>.IndexOf(KeyValuePair<TKey, TValue> item)
        {
            var i  = Search(item.Key);
            if (i < 0)
                return -1;

            return EqualityComparer<TValue>.Default.Equals(values[i], item.Value) ? i : -1;
        }

        public int IndexOfKey(TKey key) => Search(key);

        public int IndexOfValue(TValue value) => values.IndexOf(value);

        public bool Remove(TKey key)
        {
            var i = Search(key);
            if (i >= 0)
            {
                RemoveAt(i);
                ++version;
                return true;
            }

            return false;
        }

        public void RemoveAt(int index)
        {
            keys.RemoveAt(index);
            values.RemoveAt(index);
            ++version;
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
        {
            var i = Search(item.Key);
            if (i < 0)
                return false;

            if (!EqualityComparer<TValue>.Default.Equals(values[i], item.Value))
                return false;

            RemoveAt(i);
            return true;
        }

        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            if (arrayIndex < 0 || arrayIndex > array.Length)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex), string.Format(Resources.GetString(Strings.IndexOutOfRangeOrLengthIsGreaterThanBuffer), nameof(arrayIndex)));

            for (int i = 0; i != keys.Count && arrayIndex < array.Length; ++i, ++arrayIndex)
                array[arrayIndex] = new KeyValuePair<TKey, TValue>(keys[i], values[i]);
        }

        public void TrimExcess()
        {
            keys.TrimExcess();
            values.TrimExcess();
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            var i = Search(key);
            if (i >= 0)
            {
                value = values[i];
                return true;
            }

            value = default;
            return false;
        }

        void IList<KeyValuePair<TKey, TValue>>.Insert(int index, KeyValuePair<TKey, TValue> item) => throw new NotSupportedException();

        public struct Enumerator: IEnumerator<KeyValuePair<TKey, TValue>>
        {
            private readonly SortedList<TKey, TValue> list;
            private readonly int version;

            private int index;            
            private KeyValuePair<TKey, TValue> current;

            internal Enumerator(SortedList<TKey, TValue> list)
            {
                this.list = list;
                index = 0;
                version = list.version;
                current = default;
            }

            public void Dispose() { }

            public bool MoveNext()
            {
                if (version != list.version || (uint)index >= (uint)list.Count)
                    return MoveNextRare();
                current = new KeyValuePair<TKey, TValue>(list.keys[index], list.values[index]);
                ++index;
                return true;
            }

            private bool MoveNextRare()
            {
                if (version != list.version)
                    throw new InvalidOperationException(Resources.GetString(Strings.EnumeratorFailedVersion));
                index = list.keys.Count + 1;
                current = default;
                return false;
            }

            public KeyValuePair<TKey, TValue> Current => current;

            object IEnumerator.Current
            {
                get
                {
                    if (index == 0 || index == list.keys.Count + 1)
                        throw new InvalidOperationException(Resources.GetString(Strings.EnumeratorNotStartedOrFinished));
                    return current;
                }
            }

            void IEnumerator.Reset()
            {
                if (version != list.version)
                    throw new InvalidOperationException(Resources.GetString(Strings.EnumeratorFailedVersion));
                index = 0;
                current = default;
            }
        }
    }
}
