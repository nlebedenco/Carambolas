using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace Carambolas.UnityEngine.Collections.Generic
{
    [Serializable]
    public class SerializableSortedDictionary<TKey, TValue>: IDictionary<TKey, TValue>, ICollection<KeyValuePair<TKey, TValue>>, IEnumerable<KeyValuePair<TKey, TValue>>, IDictionary, ICollection, IEnumerable, ISerializationCallbackReceiver
    {
        [SerializeField]
        private List<TKey> keys = new List<TKey>();

        [SerializeField]
        private List<TValue> values = new List<TValue>();

        [NonSerialized]
        private SortedDictionary<TKey, TValue> dictionary;

        public SerializableSortedDictionary()
        {
            dictionary = new SortedDictionary<TKey, TValue>();
        }

        public SerializableSortedDictionary(IComparer<TKey> comparer)
        {
            dictionary = new SortedDictionary<TKey, TValue>(comparer);
        }

        public SerializableSortedDictionary(IDictionary<TKey, TValue> dictionary)
        {
            this.dictionary = new SortedDictionary<TKey, TValue>(dictionary);
        }

        public SerializableSortedDictionary(IDictionary<TKey, TValue> dictionary, IComparer<TKey> comparer)
        {
            this.dictionary = new SortedDictionary<TKey, TValue>(dictionary, comparer);
        }

        #region Implementation of ISerializationCallbackReceiver

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            dictionary.Clear();
            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                if (key == null)
                    continue;

                var value = values[i];
                dictionary.Add(key, value);
            }

            keys.Clear();
            values.Clear();
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            keys.Clear();
            values.Clear();
            foreach (var pair in dictionary)
            {
                if (pair.Key == null)
                    continue;

                keys.Add(pair.Key);
                values.Add(pair.Value);
            }
        }

        #endregion

        public IComparer<TKey> Comparer { get { return dictionary.Comparer; } }

        #region Implementation IDictionary

        public bool IsFixedSize
        {
            get { return false; }
        }

        public ICollection<TKey> Keys
        {
            get { return dictionary.Keys; }
        }

        ICollection IDictionary.Keys
        {
            get { return dictionary.Keys; }
        }

        public ICollection<TValue> Values
        {
            get { return dictionary.Values; }
        }

        ICollection IDictionary.Values
        {
            get { return dictionary.Values; }
        }

        public TValue this[TKey key]
        {
            get { return dictionary[key]; }
            set { dictionary[key] = value; }
        }

        object IDictionary.this[object key]
        {
            get
            {
                if (!(key is TKey))
                    return null;

                return dictionary[(TKey)key];
            }

            set
            {
                if (!(key is TKey))
                    return;

                if (!(value is TValue) && value != null)
                    return;

                dictionary[(TKey)key] = (TValue)value;
            }
        }

        public void Add(TKey key, TValue value)
        {
            dictionary.Add(key, value);
        }

        void IDictionary.Add(object key, object value)
        {
            if (!(key is TKey))
                return;

            if (!(value is TValue) && value != null)
                return;

            dictionary.Add((TKey)key, (TValue)value);
        }

        public bool ContainsKey(TKey key)
        {
            return dictionary.ContainsKey(key);
        }

        bool IDictionary.Contains(object key)
        {
            if (!(key is TKey))
                return false;

            return dictionary.ContainsKey((TKey)key);
        }

        public bool Remove(TKey key)
        {
            return dictionary.Remove(key);
        }

        void IDictionary.Remove(object key)
        {
            if (!(key is TKey))
                return;

            dictionary.Remove((TKey)key);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return dictionary.TryGetValue(key, out value);
        }

        IDictionaryEnumerator IDictionary.GetEnumerator()
        {
            return ((IDictionary)dictionary).GetEnumerator();
        }

        #endregion

        #region Implementation ICollection

        public int Count
        {
            get { return dictionary.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool IsSynchronized
        {
            get { return false; }
        }

        public object SyncRoot
        {
            get { return null; }
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            dictionary.Add(item.Key, item.Value);
        }

        public void Clear()
        {
            dictionary.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return dictionary.ContainsKey(item.Key) && dictionary[item.Key].Equals(item.Value);
        }

        void ICollection.CopyTo(Array array, int index)
        {
        }

        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return dictionary.Remove(item.Key);
        }

        #endregion

        #region Implementation of IEnumerable

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return dictionary.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return dictionary.GetEnumerator();
        }

        #endregion
    }
}
