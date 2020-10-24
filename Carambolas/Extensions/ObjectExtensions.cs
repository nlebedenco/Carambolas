using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Carambolas
{
    public static class ObjectExtensions
    {
        #region Reflection

        public static IEnumerable<FieldInfo> GetAllFields(this object target, Func<FieldInfo, bool> predicate)
        {
            var types = new List<Type>() { target.GetType() };

            while (types.Last().BaseType != null)
                types.Add(types.Last().BaseType);

            for (int i = types.Count - 1; i >= 0; i--)
            {
                var fieldInfos = types[i]
                    .GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Where(predicate);

                foreach (var fieldInfo in fieldInfos)
                    yield return fieldInfo;
            }
        }

        public static IEnumerable<PropertyInfo> GetAllProperties(this object target, Func<PropertyInfo, bool> predicate)
        {
            var types = new List<Type>() { target.GetType() };

            while (types.Last().BaseType != null)
                types.Add(types.Last().BaseType);

            for (int i = types.Count - 1; i >= 0; i--)
            {
                var propertyInfos = types[i]
                    .GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Where(predicate);

                foreach (var propertyInfo in propertyInfos)
                    yield return propertyInfo;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<MethodInfo> GetAllMethods(this object target, Func<MethodInfo, bool> predicate)
            => target.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                .Where(predicate);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FieldInfo GetField(this object target, string fieldName) => GetAllFields(target, f => f.Name.Equals(fieldName, StringComparison.InvariantCulture)).FirstOrDefault();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PropertyInfo GetProperty(this object target, string propertyName) => GetAllProperties(target, p => p.Name.Equals(propertyName, StringComparison.InvariantCulture)).FirstOrDefault();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MethodInfo GetMethod(this object target, string methodName) => GetAllMethods(target, m => m.Name.Equals(methodName, StringComparison.InvariantCulture)).FirstOrDefault();

        #endregion
    }
}
