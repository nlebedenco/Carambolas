using System;
using System.Linq;

using UnityEngine;
using UnityEditor;

using Carambolas.UnityEngine;

namespace Carambolas.UnityEditor
{
    internal class PreloadedAssetsRemoveDeleted: AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (deletedAssets?.Length > 0)
                PlayerSettings.SetPreloadedAssets(PlayerSettings.GetPreloadedAssets().Where(asset => asset).ToArray());
        }
    }
}
