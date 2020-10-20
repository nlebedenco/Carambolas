using System;
using System.Runtime.CompilerServices;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Carambolas.UnityEngine
{
    public static class GameObjectExtensions
    {
        /// <summary>
        /// Get a component from this GameObject; if the component does not exist it will be
        /// immediately added and returned.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetOrAddComponent<T>(this GameObject self) where T : Component => self.GetComponent<T>() ?? self.AddComponent<T>();

        /// <summary>
        /// Get a component from this GameObject; if the component does not exist it will be
        /// immediately added and returned.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Component GetOrAddComponent(this GameObject self, Type type) => self.GetComponent(type) ?? self.AddComponent(type);

        /// <summary>
        /// Determines if this game object is a child of a given game object. 
        /// </summary>
        /// <returns>
        /// true if this transform is a child, deep child (child of a child) or identical to this game object, otherwise false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsChildOf(this GameObject self, GameObject other) => other == null ? false : self.transform.IsChildOf(other.transform);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasComponent<T>(this GameObject self) where T: Component => self.GetComponent<T>() != null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasComponent(this GameObject self, Type componentType) => self.GetComponent(componentType) != null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsRuntimePrefab(this GameObject self)
        {
            return
#if UNITY_EDITOR
            Application.isPlaying &&
#endif
            !self.scene.IsValid();
        }

#if UNITY_EDITOR
        public static GameObject GetPrefab(this GameObject self)
        {
            if (PrefabUtility.IsPartOfNonAssetPrefabInstance(self))
                return PrefabUtility.GetCorrespondingObjectFromSource(self);

            if (PrefabUtility.IsPartOfPrefabAsset(self))             
                return self;

            return null;
        }

        public static string GetPrefabPath(this GameObject self)
        {
            var prefab = self.GetPrefab();
            return (prefab == null)
                ? ((self.transform.parent == null) ? UnityEditor.Experimental.SceneManagement.PrefabStageUtility.GetPrefabStage(self)?.prefabAssetPath : null)
                : AssetDatabase.GetAssetPath(prefab);
        }

        public static string GetPrefabGuid(this GameObject self)
        {
            var assetPath = GetPrefabPath(self);
            return (string.IsNullOrEmpty(assetPath)) ? null : AssetDatabase.AssetPathToGUID(assetPath);
        }
#endif
    }
}
