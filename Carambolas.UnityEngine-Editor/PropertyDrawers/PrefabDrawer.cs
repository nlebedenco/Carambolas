using System;

using UnityEngine;
using UnityEditor;

using Carambolas.UnityEngine;

namespace Carambolas.UnityEditor
{
    [CustomPropertyDrawer(typeof(PrefabAttribute))]
    public class PrefabDrawer: PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType == SerializedPropertyType.ObjectReference)
            {
                label = EditorGUI.BeginProperty(position, label, property);
                EditorGUI.BeginChangeCheck();

                var componentType = (attribute as PrefabAttribute)?.ComponentType;

                var previous = property.objectReferenceValue;
                var value = EditorGUI.ObjectField(position, label, property.objectReferenceValue, typeof(GameObject), false);
                if (EditorGUI.EndChangeCheck())
                {
                    property.objectReferenceValue = (value == null || componentType == null) ? value : ((((GameObject)value).HasComponent(componentType)) ? value : previous);
                }

                EditorGUI.EndProperty();
            }
            else
            {
                EditorGUI.LabelField(position, label.text, "Prefab Attribute can only be used with Object fields.");
            }
        }
    }
}
