using System;
using System.Collections;
using System.IO;

using UnityEngine;
using UnityEditor;

using Carambolas.UnityEngine;
using JsonUtility = Carambolas.UnityEngine.JsonUtility;

namespace Carambolas.UnityEditor
{
    internal static class ExportToJson
    {
        private const string MenuItemName = "Assets/Export to Json";

        [MenuItem(MenuItemName, true)]
        private static bool CanExecute() => Selection.activeObject is ScriptableObject && Selection.objects?.Length == 1;

        [MenuItem(MenuItemName, false, 20)]
        private static void Execute()
        {
            var activeObject = Selection.activeObject;
            var path = Path.ChangeExtension(AssetDatabase.GetAssetPath(activeObject), ".json");
            using (var file = File.CreateText(path))
                JsonUtility.Save(activeObject, file, true);
            AssetDatabase.Refresh();
        }
    }
}
