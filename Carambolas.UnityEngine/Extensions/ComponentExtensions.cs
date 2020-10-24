using System;

using UnityEngine;

namespace Carambolas.UnityEngine
{
    public static class ComponentExtensions
    {
        public static T[] GetComponentsInImmediateChildren<T>(this Component self, bool includeInactive = false) where T : class => self.gameObject.GetComponentsInImmediateChildren<T>(includeInactive);

        public static T FindComponent<T>(this Component self, string name) where T : class => self.transform.Find(name).OrNull()?.GetComponent<T>();

        public static bool IsRuntimePrefab(this Component self) => self.gameObject.IsRuntimePrefab();

        public static bool TryGetComponent<T>(this Component self, out T value) where T : class => (value = self.GetComponent<T>()) != null;
    }
}
