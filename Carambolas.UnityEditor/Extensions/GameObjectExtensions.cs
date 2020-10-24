using System;

using UnityEngine;
using UnityEditor;

using Carambolas.UnityEngine;

namespace Carambolas.UnityEditor
{
    public static class GameObjectExtensions
    {
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
            return prefab
                ? AssetDatabase.GetAssetPath(prefab)
                : (self.transform.parent ? null : global::UnityEditor.Experimental.SceneManagement.PrefabStageUtility.GetPrefabStage(self)?.prefabAssetPath);
        }

        public static string GetPrefabGuid(this GameObject self)
        {
            var assetPath = GetPrefabPath(self);
            return string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.AssetPathToGUID(assetPath);
        }
    }
}
