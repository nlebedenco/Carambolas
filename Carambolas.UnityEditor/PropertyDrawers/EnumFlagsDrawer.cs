using System;

using UnityEngine;
using UnityEditor;

using Carambolas.UnityEngine;

namespace Carambolas.UnityEditor
{
    [CustomPropertyDrawer(typeof(EnumFlagsAttribute))]
    public class EnumFlagsAttributeDrawer: PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            label = EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            {
                var enumNames = property.enumNames;
                var displayOptions = new string[enumNames.Length - 1];
                Array.Copy(enumNames, 1, displayOptions, 0, displayOptions.Length);

                var value = EditorGUI.MaskField(position, label, property.intValue, displayOptions);
                if (EditorGUI.EndChangeCheck())
                {
                    if (value < 0)
                    {
                        int bits = 0;
                        foreach (var enumValue in Enum.GetValues(fieldInfo.FieldType))
                        {
                            int checkBit = value & (int)enumValue;
                            if (checkBit != 0)
                                bits |= (int)enumValue;
                        }

                        value = bits;
                    }
                    property.intValue = value;
                }
            }
            EditorGUI.EndProperty();
        }
    }
}
