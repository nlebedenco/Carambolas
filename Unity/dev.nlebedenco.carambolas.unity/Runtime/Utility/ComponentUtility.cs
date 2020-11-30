using System;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Carambolas.UnityEngine
{
    public abstract class ComponentUtility
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Create<T>(string name = "") where T : Component => (new GameObject(name ?? nameof(T), typeof(T))).GetComponent<T>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Component Create(Type type, string name = "") => (new GameObject(name ?? type.Name, type)).GetComponent(type);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Create<T>(GameObject go) where T : Component => go.AddComponent<T>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Component Create(Type type, GameObject go) => go.AddComponent(type);

    }
}
