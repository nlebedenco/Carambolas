using System;
using System.Collections;
using System.IO;

using UnityEngine;
using UnityEditor;

using Carambolas.UnityEngine;

namespace Carambolas.UnityEditor
{
    internal static class ExportToBinary
    {
        private const string MenuItemName = "Assets/Export to Binary";

        [MenuItem(MenuItemName, true)]
        private static bool CanExecute() => Selection.activeObject is ScriptableObject && Selection.objects?.Length == 1;

        [MenuItem(MenuItemName, false, 20)]
        private static void Execute()
        {
            var activeObject = Selection.activeObject;
            var path = Path.ChangeExtension(AssetDatabase.GetAssetPath(activeObject), "jgz");
            using (var file = File.Create(path))
                BinaryUtility.Save(activeObject, file);
            AssetDatabase.Refresh();
        }
    }
}
