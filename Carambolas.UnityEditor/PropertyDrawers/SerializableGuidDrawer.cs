using System;

using UnityEngine;
using UnityEditor;

using Carambolas.UnityEngine;
using Guid = Carambolas.UnityEngine.Guid;

namespace Carambolas.UnityEditor
{
    [CustomPropertyDrawer(typeof(Guid))]
    public class SerializableGuidDrawer: PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var p0 = property.FindPropertyRelative("a");
            var p1 = property.FindPropertyRelative("b");
            var p2 = property.FindPropertyRelative("c");
            var p3 = property.FindPropertyRelative("d");

            var a = unchecked((uint)p0.intValue);
            var b = unchecked((ushort)p1.intValue);
            var c = unchecked((ushort)p2.intValue);
            var d = unchecked((ulong)p3.longValue);
            
            var guid = new Guid(a, b, c, d);

            label = EditorGUI.BeginProperty(position, label, property);
            using (var input = new EditorGUI.ChangeCheckScope())
            {
                var value = EditorGUI.DelayedTextField(position, label, guid.ToString());
                if (input.changed)
                {
                    guid = new Guid(value);
                    (a, b, c, d) = guid;
                    p0.intValue = (int)a;
                    p1.intValue = b;
                    p2.intValue = c;
                    p3.longValue = (long)d;
                }
            }
            EditorGUI.EndProperty();
        }
    }
}
