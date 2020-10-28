using System;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Carambolas.UnityEngine
{
    public static class Component<T>  where T: Component
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Create(string name = null) => (new GameObject(name ?? typeof(T).Name, typeof(T))).GetComponent<T>();
    }
}
