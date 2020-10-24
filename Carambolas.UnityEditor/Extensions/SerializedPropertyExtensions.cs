using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

using UnityEngine;
using UnityObject = UnityEngine.Object;

using UnityEditor;

using Carambolas.UnityEngine;

namespace Carambolas.UnityEditor
{
    public static class SerializedPropertyExtensions
    {
        private static readonly PropertyInfo gradientValue = typeof(SerializedProperty).GetProperty("gradientValue", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void SetValue(this SerializedProperty property, int value)
        {
            if (property.propertyType == SerializedPropertyType.Enum)
                property.enumValueIndex = value;
            else
                property.intValue = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetValue(this SerializedProperty property, bool value) => property.boolValue = value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetValue(this SerializedProperty property, float value) => property.floatValue = value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetValue(this SerializedProperty property, string value) => property.stringValue = value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetValue(this SerializedProperty property, Color value) => property.colorValue = value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetValue(this SerializedProperty property, UnityObject value) => property.objectReferenceValue = value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetValue(this SerializedProperty property, Vector2 value) => property.vector2Value = value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetValue(this SerializedProperty property, Vector2Int value) => property.vector2IntValue = value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetValue(this SerializedProperty property, Vector3 value) => property.vector3Value = value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetValue(this SerializedProperty property, Vector3Int value) => property.vector3IntValue = value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetValue(this SerializedProperty property, Vector4 value) => property.vector4Value = value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetValue(this SerializedProperty property, Rect value) => property.rectValue = value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetValue(this SerializedProperty property, AnimationCurve value) => property.animationCurveValue = value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetValue(this SerializedProperty property, Bounds value) => property.boundsValue = value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetValue(this SerializedProperty property, BoundsInt value) => property.boundsIntValue = value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetValue(this SerializedProperty property, Gradient value) => gradientValue.SetValue(property, value, null);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetValue(this SerializedProperty property, Quaternion value) => property.quaternionValue = value;

        public static IEnumerable<SerializedProperty> GetChildren(this SerializedProperty property)
        {
            var end = property.Copy();
            if (!end.Next(false))
                end = null;

            var it = property.Copy();
            if (it.Next(true))
            {
                do yield return it;
                while (it.Next(false) && (end == null || !SerializedProperty.EqualContents(it, end)));
            }
        }

        public static IEnumerable<SerializedProperty> GetVisibleChildren(this SerializedProperty property)
        {
            var end = property.Copy();
            if (!end.NextVisible(false))
                end = null;

            var it = property.Copy();
            if (it.NextVisible(true))
            {
                do yield return it;
                while (it.NextVisible(false) && (end == null || !SerializedProperty.EqualContents(it, end)));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetAttribute<T>(this SerializedProperty property) where T : Attribute => property.GetTargetObject().GetField(property.name).GetCustomAttribute<T>(true);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<T> GetAttributes<T>(this SerializedProperty property) where T : Attribute => property.GetTargetObject().GetField(property.name).GetCustomAttributes<T>(true);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UnityObject GetTargetObject(this SerializedProperty property) => property.serializedObject.targetObject;
    }  
}
