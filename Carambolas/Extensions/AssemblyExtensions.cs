using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Carambolas
{
    public static class AssemblyExtensions
    {
        public static IEnumerable<Type> GetLoadableTypes(this Assembly assembly)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types;
            }

            return types.Where(t => t != null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<Type> GetAllTypesImplementingOpenGenericType(this Assembly assembly, Type openGenericType)
            => assembly
                .GetLoadableTypes()
                .SelectMany(x => x.GetInterfaces(), (x, i) => new { Type = x, Interface = i, x.BaseType })
                .Where(a => (a.BaseType != null && a.BaseType.IsGenericType && openGenericType.IsAssignableFrom(a.BaseType.GetGenericTypeDefinition())) || (a.Interface.IsGenericType && openGenericType.IsAssignableFrom(a.Interface.GetGenericTypeDefinition())))
                .Select(a => a.Type);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<Type> GetAllTypesAssignableFrom(this Assembly assembly, Type other)
            => assembly
                .GetLoadableTypes()
                .Where(t => t.IsAssignableFrom(other));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<Type> GetAllSubclassesOf(this Assembly assembly, Type other)
            => assembly
                .GetLoadableTypes()
                .Where(t => t == other || t.IsSubclassOf(other));
    }
}
