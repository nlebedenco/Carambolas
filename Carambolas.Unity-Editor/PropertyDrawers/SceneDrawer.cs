using System;

using UnityEngine;
using UnityEditor;

using Carambolas.UnityEngine;

namespace Carambolas.UnityEditor
{
    [CustomPropertyDrawer(typeof(SceneAttribute))]
    public class SceneDrawer: PropertyDrawer
    {
        private static  bool TryGetSceneAsset(string path, out SceneAsset sceneAsset) => (sceneAsset = AssetDatabase.LoadAssetAtPath(path, typeof(SceneAsset)) as SceneAsset) != null;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType == SerializedPropertyType.String)
            {
                var previous = property.stringValue;
                SceneAsset sceneAsset = null;
                if (!string.IsNullOrEmpty(previous))
                    TryGetSceneAsset(previous, out sceneAsset);

                label.tooltip = previous;
                label = EditorGUI.BeginProperty(position, label, property);
                {
                    EditorGUI.BeginChangeCheck();

                    sceneAsset = EditorGUI.ObjectField(position, label, sceneAsset, typeof(SceneAsset), false) as SceneAsset;

                    if (EditorGUI.EndChangeCheck())
                    {
                        if (sceneAsset == null)
                            property.stringValue = string.Empty;
                        else
                        {
                            var value = AssetDatabase.GetAssetPath(sceneAsset);
                            if (value != previous)
                                property.stringValue = value;
                        }
                    }
                }
                EditorGUI.EndProperty();

                if (!string.IsNullOrEmpty(previous) && !sceneAsset)
                    EditorGUILayout.HelpBox("This scene cannot be found. Please locate the correct scene asset or remove this entry.", MessageType.Warning);
            }
            else
            {
                EditorGUI.LabelField(position, label.text, "Scene Attribute can only be used with string fields to store the scene path.");
            }
        }
    }
}
