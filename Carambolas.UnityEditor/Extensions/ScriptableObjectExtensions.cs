using System;
using System.Linq;

using UnityEngine;
using UnityEditor;

using Carambolas.UnityEngine;

namespace Carambolas.UnityEditor
{
    public static class ScriptableObjectExtensions
    {
        public static void AddToPreloadedAssets(this ScriptableObject scriptableObject)
        {
            var preloadedAssets = PlayerSettings.GetPreloadedAssets();
            if (preloadedAssets.Any(preloadedAsset => preloadedAsset && preloadedAsset.GetInstanceID() == scriptableObject.GetInstanceID()))
                return;

            Array.Resize(ref preloadedAssets, preloadedAssets.Length + 1);
            preloadedAssets[preloadedAssets.Length - 1] = scriptableObject;
            PlayerSettings.SetPreloadedAssets(preloadedAssets);
        }
    }
}
