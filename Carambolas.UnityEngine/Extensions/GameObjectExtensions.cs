using System;
using System.Linq;

using UnityEngine;

namespace Carambolas.UnityEngine
{
    public static class GameObjectExtensions
    {
        /// <summary>
        /// Get a component from this GameObject; if the component does not exist it will be
        /// immediately added and returned.
        /// </summary>
        public static T GetOrAddComponent<T>(this GameObject self) where T : Component => self.GetComponent<T>().OrNull() ?? self.AddComponent<T>();

        /// <summary>
        /// Get a component from this GameObject; if the component does not exist it will be
        /// immediately added and returned.
        /// </summary>
        public static Component GetOrAddComponent(this GameObject self, Type type) => self.GetComponent(type).OrNull() ?? self.AddComponent(type);

        public static T[] GetComponentsInImmediateChildren<T>(this GameObject self, bool includeInactive = false) where T : class => self.transform.Cast<Transform>().Select(t => t.GetComponent<T>()).ToArray();

        public static T FindComponent<T>(this GameObject self, string name) where T : class => self.transform.Find(name).OrNull()?.GetComponent<T>();

        public static bool TryGetComponent<T>(this GameObject self, out T value) where T : class => (value = self.GetComponent<T>()) != null;

        public static bool HasComponent<T>(this GameObject self) where T : class => self.GetComponent<T>() != null;

        public static bool HasComponent(this GameObject self, Type componentType) => self.GetComponent(componentType);

        /// <summary>
        /// Determines if this game object is a child of a given game object. 
        /// </summary>
        /// <returns>
        /// true if this transform is a child, deep child (child of a child) or identical to this game object, otherwise false.
        /// </returns>
        public static bool IsChildOf(this GameObject self, GameObject other) => other ? self.transform.IsChildOf(other.transform) : false;

        public static bool IsRuntimePrefab(this GameObject self) => Application.isPlaying && !self.scene.IsValid();
    }
}
