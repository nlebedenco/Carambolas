using System;
using System.Collections;
using System.Collections.Generic;

namespace Carambolas.Collections.Generic
{
    public class ReadOnlySet<T>: IReadOnlyCollection<T>, ISet<T>
    {
        private readonly HashSet<T> set = new HashSet<T>();

        public ReadOnlySet(HashSet<T> set)
        {
            this.set = set ?? throw new ArgumentNullException(nameof(set));
        }

        public int Count => set.Count;

        public bool IsReadOnly => true;

        bool ISet<T>.Add(T item) => throw new NotSupportedException();
        void ICollection<T>.Add(T item) => throw new NotSupportedException();
        bool ICollection<T>.Remove(T item) => throw new NotSupportedException();
        void ICollection<T>.Clear() => throw new NotSupportedException();

        public bool Contains(T item) => set.Contains(item);
        public void CopyTo(T[] array, int arrayIndex) => set.CopyTo(array, arrayIndex);

        public HashSet<T>.Enumerator GetEnumerator() => set.GetEnumerator();
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => (set as IEnumerable<T>).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => (set as IEnumerable<T>).GetEnumerator();

        void ISet<T>.UnionWith(IEnumerable<T> other) => throw new NotSupportedException();
        void ISet<T>.IntersectWith(IEnumerable<T> other) => throw new NotSupportedException();
        void ISet<T>.ExceptWith(IEnumerable<T> other) => throw new NotSupportedException();
        void ISet<T>.SymmetricExceptWith(IEnumerable<T> other) => throw new NotSupportedException();

        public bool IsSubsetOf(IEnumerable<T> other) => set.IsSubsetOf(other);
        public bool IsSupersetOf(IEnumerable<T> other) => set.IsSupersetOf(other);
        public bool IsProperSupersetOf(IEnumerable<T> other) => set.IsProperSupersetOf(other);
        public bool IsProperSubsetOf(IEnumerable<T> other) => set.IsProperSubsetOf(other);
        public bool Overlaps(IEnumerable<T> other) => set.Overlaps(other);
        public bool SetEquals(IEnumerable<T> other) => set.SetEquals(other);
    }

    /// <summary>
    /// A replacement for <see cref="System.Collections.ObjectModel.ReadOnlyCollection{T}"/> that exposes a value-type 
    /// enumerator instead of a <see cref="IEnumerator{T}"/> so the compiler may optimize foreach loops and avoid boxing.
    /// </summary>
    public class ReadOnlyCollection<T>: IReadOnlyList<T>, IList<T>
    {
        private readonly List<T> list;

        public ReadOnlyCollection()
        {
            list = new List<T>();
        }

        public ReadOnlyCollection(List<T> list)
        {
            this.list = list ?? throw new ArgumentNullException(nameof(list));
        }

        public T this[int index] => list[index];
        T IList<T>.this[int index]
        {
            get => list[index];
            set => throw new NotSupportedException();
        }

        public int Count => list.Count;
        public bool IsReadOnly => true;

        void ICollection<T>.Add(T item) => throw new NotSupportedException();
        bool ICollection<T>.Remove(T item) => throw new NotSupportedException();
        void ICollection<T>.Clear() => throw new NotSupportedException();

        void IList<T>.Insert(int index, T item) => throw new NotSupportedException();
        void IList<T>.RemoveAt(int index) => throw new NotSupportedException();

        public int IndexOf(T item) => list.IndexOf(item);
        public bool Contains(T item) => list.Contains(item);
        public void CopyTo(T[] array, int arrayIndex) => list.CopyTo(array, arrayIndex);

        public List<T>.Enumerator GetEnumerator() => list.GetEnumerator();
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => (list as IEnumerable<T>).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => (list as IEnumerable<T>).GetEnumerator();
    }

    /// <summary>
    /// A replacement for <see cref="System.Collections.ObjectModel.ReadOnlyDictionary{TKey, TValue}"/> that exposes a value-type 
    /// enumerator instead of a <see cref="IEnumerator{T}"/> so the compiler may optimize foreach loops and avoid boxing.
    /// </summary>
    public class ReadOnlyDictionary<TKey, TValue>: IReadOnlyDictionary<TKey, TValue>, IDictionary<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> dictionary = new Dictionary<TKey, TValue>();

        public ReadOnlyDictionary(Dictionary<TKey, TValue> dictionary)
        {
            this.dictionary = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
        }

        public TValue this[TKey key] => dictionary[key];

        public int Count => dictionary.Count;
        public IEqualityComparer<TKey> Comparer => dictionary.Comparer;
        public bool IsReadOnly => true;

        public Dictionary<TKey, TValue>.KeyCollection Keys => dictionary.Keys;
        public Dictionary<TKey, TValue>.ValueCollection Values => dictionary.Values;

        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Keys;
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Values;

        ICollection<TKey> IDictionary<TKey, TValue>.Keys => Keys;
        ICollection<TValue> IDictionary<TKey, TValue>.Values => Values;

        TValue IDictionary<TKey, TValue>.this[TKey key]
        {
            get => dictionary[key];
            set => throw new NotSupportedException();
        }

        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) => throw new NotSupportedException();
        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item) => throw new NotSupportedException();
        void ICollection<KeyValuePair<TKey, TValue>>.Clear() => throw new NotSupportedException();

        public bool ContainsKey(TKey key) => dictionary.ContainsKey(key);
        public bool ContainsValue(TValue value) => dictionary.ContainsValue(value);

        public Dictionary<TKey, TValue>.Enumerator GetEnumerator() => dictionary.GetEnumerator();
        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => (dictionary as IEnumerable<KeyValuePair<TKey, TValue>>).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<KeyValuePair<TKey, TValue>>)dictionary).GetEnumerator();

        public bool TryGetValue(TKey key, out TValue value) => dictionary.TryGetValue(key, out value);

        void IDictionary<TKey, TValue>.Add(TKey key, TValue value) => throw new NotSupportedException();
        bool IDictionary<TKey, TValue>.Remove(TKey key) => throw new NotSupportedException();

        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item) => (dictionary as ICollection<KeyValuePair<TKey, TValue>>).Contains(item);
        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => (dictionary as IDictionary<TKey, TValue>).CopyTo(array, arrayIndex);
    }

    public class ReadOnlySortedDictionary<TKey, TValue>: IReadOnlyDictionary<TKey, TValue>, IDictionary<TKey, TValue>
    {
        private readonly SortedDictionary<TKey, TValue> dictionary = new SortedDictionary<TKey, TValue>();

        public ReadOnlySortedDictionary(SortedDictionary<TKey, TValue> dictionary)
        {
            this.dictionary = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
        }

        public TValue this[TKey key] => dictionary[key];

        public int Count => dictionary.Count;
        public bool IsReadOnly => true;

        public SortedDictionary<TKey, TValue>.KeyCollection Keys => dictionary.Keys;
        public SortedDictionary<TKey, TValue>.ValueCollection Values => dictionary.Values;

        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Keys;
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Values;

        ICollection<TKey> IDictionary<TKey, TValue>.Keys => Keys;
        ICollection<TValue> IDictionary<TKey, TValue>.Values => Values;

        TValue IDictionary<TKey, TValue>.this[TKey key]
        {
            get => dictionary[key];
            set => throw new NotSupportedException();
        }

        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) => throw new NotSupportedException();
        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item) => throw new NotSupportedException();
        void ICollection<KeyValuePair<TKey, TValue>>.Clear() => throw new NotSupportedException();

        public bool ContainsKey(TKey key) => dictionary.ContainsKey(key);
        public bool ContainsValue(TValue value) => dictionary.ContainsValue(value);

        public SortedDictionary<TKey, TValue>.Enumerator GetEnumerator() => dictionary.GetEnumerator();
        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => (dictionary as IEnumerable<KeyValuePair<TKey, TValue>>).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<KeyValuePair<TKey, TValue>>)dictionary).GetEnumerator();

        public bool TryGetValue(TKey key, out TValue value) => dictionary.TryGetValue(key, out value);

        void IDictionary<TKey, TValue>.Add(TKey key, TValue value) => throw new NotSupportedException();
        bool IDictionary<TKey, TValue>.Remove(TKey key) => throw new NotSupportedException();

        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item) => (dictionary as ICollection<KeyValuePair<TKey, TValue>>).Contains(item);

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => dictionary.CopyTo(array, arrayIndex);
    }

    public class ReadOnlyOrderedDictionary<TKey, TValue>: IReadOnlyDictionary<TKey, TValue>, IDictionary<TKey, TValue>, IReadOnlyList<KeyValuePair<TKey, TValue>>, IList<KeyValuePair<TKey, TValue>>
    {
        private readonly OrderedDictionary<TKey, TValue> dictionary = new OrderedDictionary<TKey, TValue>();

        public ReadOnlyOrderedDictionary(OrderedDictionary<TKey, TValue> dictionary)
        {
            this.dictionary = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
        }

        public TValue this[int index] => dictionary[index];
        KeyValuePair<TKey, TValue> IReadOnlyList<KeyValuePair<TKey, TValue>>.this[int index] => new KeyValuePair<TKey, TValue>(dictionary.GetKey(index), dictionary[index]);
        KeyValuePair<TKey, TValue> IList<KeyValuePair<TKey, TValue>>.this[int index]
        {
            get => new KeyValuePair<TKey, TValue>(dictionary.GetKey(index), dictionary[index]);
            set => throw new NotSupportedException();
        }

        public TValue this[TKey key] => dictionary[key];
        TValue IDictionary<TKey, TValue>.this[TKey key]
        {
            get => dictionary[key];
            set => throw new NotSupportedException();
        }

        public int Count => dictionary.Count;
        public bool IsReadOnly => true;

        public ReadOnlyCollection<TKey> Keys => dictionary.Keys;
        public ReadOnlyCollection<TValue> Values => dictionary.Values;

        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Keys;
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Values;

        ICollection<TKey> IDictionary<TKey, TValue>.Keys => Keys;
        ICollection<TValue> IDictionary<TKey, TValue>.Values => Values;

        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) => throw new NotSupportedException();
        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item) => throw new NotSupportedException();
        void ICollection<KeyValuePair<TKey, TValue>>.Clear() => throw new NotSupportedException();

        public bool ContainsKey(TKey key) => dictionary.ContainsKey(key);
        public bool ContainsValue(TValue value) => dictionary.ContainsValue(value);

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => dictionary.GetEnumerator();
        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => (dictionary as IEnumerable<KeyValuePair<TKey, TValue>>).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<KeyValuePair<TKey, TValue>>)dictionary).GetEnumerator();

        public bool TryGetValue(TKey key, out TValue value) => dictionary.TryGetValue(key, out value);

        void IDictionary<TKey, TValue>.Add(TKey key, TValue value) => throw new NotSupportedException();
        bool IDictionary<TKey, TValue>.Remove(TKey key) => throw new NotSupportedException();

        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item) => (dictionary as ICollection<KeyValuePair<TKey, TValue>>).Contains(item);
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => dictionary.CopyTo(array, arrayIndex);

        public TKey GetKey(int index) => dictionary.GetKey(index);
        public int IndexOf(TKey key) => dictionary.IndexOf(key);
        int IList<KeyValuePair<TKey, TValue>>.IndexOf(KeyValuePair<TKey, TValue> item) => (dictionary as IList<KeyValuePair<TKey, TValue>>).IndexOf(item);

        void IList<KeyValuePair<TKey, TValue>>.Insert(int index, KeyValuePair<TKey, TValue> item) => throw new NotImplementedException();
        void IList<KeyValuePair<TKey, TValue>>.RemoveAt(int index) => throw new NotSupportedException();
    }
}
