using System;
using System.Diagnostics;
using UnityEngine;

namespace Carambolas.UnityEngine
{
    public static class ScriptableObjectExtensions
    {
        [Conditional("UNITY_EDITOR")]
        public static void AddToPreloadedAssets(this ScriptableObject scriptableObject)
        {
#if UNITY_EDITOR
            var preloadedAssets = UnityEditor.PlayerSettings.GetPreloadedAssets().ToList();
            if (preloadedAssets.Any(preloadedAsset => preloadedAsset && preloadedAsset.GetInstanceID() == scriptableObject.GetInstanceID()))
                return;

            preloadedAssets.Add(scriptableObject);
            UnityEditor.PlayerSettings.SetPreloadedAssets(preloadedAssets.ToArray());
#endif
        }
    }
}
