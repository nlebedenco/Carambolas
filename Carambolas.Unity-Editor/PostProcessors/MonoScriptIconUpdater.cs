using System;
using System.Reflection;

using UnityEngine;
using UnityObject = UnityEngine.Object;
using UnityResources = UnityEngine.Resources;

using UnityEditor;

using Carambolas.UnityEngine;
using Debug = Carambolas.UnityEngine.Debug;

namespace Carambolas.UnityEditor
{
    internal class MonoScriptIconUpdater
    {
        private static MethodInfo GetIconForObject = typeof(EditorGUIUtility).GetMethod("GetIconForObject", BindingFlags.Static | BindingFlags.NonPublic);
        private static MethodInfo SetIconForObject = typeof(EditorGUIUtility).GetMethod("SetIconForObject", BindingFlags.Static | BindingFlags.NonPublic);
        private static MethodInfo CopyMonoScriptIconToImporters = typeof(MonoImporter).GetMethod("CopyMonoScriptIconToImporters", BindingFlags.Static | BindingFlags.NonPublic);

        [InitializeOnLoadMethod]
        private static void OnLoad()
        {
            AssemblyReloadEvents.afterAssemblyReload -= AfterAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += AfterAssemblyReload;
        }

        private static void AfterAssemblyReload()
        {
            var scripts = MonoImporter.GetAllRuntimeMonoScripts();
            if (scripts != null)
                foreach (var script in scripts)
                    UpdateIcon(script);
        }

        private static void UpdateIcon(MonoScript script)
        {
            if (script)
            {
                var type = script.GetClass();
                var attr = type?.GetCustomAttribute<IconAttribute>(true);
                string iconName = attr?.IconName;
                if (!string.IsNullOrEmpty(iconName))
                {
                    var icon = UnityResources.Load<Texture2D>(iconName);
                    if (icon)
                    {
                        var current = GetIconForObject.Invoke(null, new object[] { script }) as Texture2D;
                        if (!current || icon != current)
                        {
                            SetIconForObject.Invoke(null, new object[] { script, icon });
                            CopyMonoScriptIconToImporters.Invoke(null, new object[] { script });

                            EditorApplication.delayCall += () =>
                            {
                                foreach (var guid in AssetDatabase.FindAssets($"t:{script.GetClass()}"))
                                    AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ImportRecursive);
                            };
                        }
                    }
                    else
                    {
                        Debug.LogWarningFormat("Failed to load icon {0} required by type {1}.", iconName, type.FullName);
                    }
                }
            }
        }
    }
}
