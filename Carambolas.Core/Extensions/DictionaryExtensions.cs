using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Carambolas
{
    public static class DictionaryExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddOrReplace<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue value) => dict[key] = value;
    }
}
