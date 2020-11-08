using System;
using System.Runtime.CompilerServices;

namespace Carambolas
{
    public static class TypeExtensions
    {
        /// <summary>
        /// Find if type is derived from base type. Use <see cref="IsEqualOrAssignableFrom"/> for a similar method that supports interface types.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEqualOrSubclassOf(this Type type, Type baseType) => (type == baseType || type.IsSubclassOf(baseType));

        /// <summary>
        /// Find if type is derived from base type. This method supports interface types.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEqualOrAssignableFrom(this Type type, Type derivedType) => (type == derivedType || type.IsAssignableFrom(derivedType));
    }
}
