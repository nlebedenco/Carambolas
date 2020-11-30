using System;
using System.Runtime.CompilerServices;

using UnityObject = UnityEngine.Object;

namespace Carambolas.UnityEngine
{
    public static class UnityObjectExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetNameInParentheses(this UnityObject self) => string.IsNullOrEmpty(self.name) ? string.Empty : $"({self.name})";

        /// <summary>
        /// Compare to null using Unity's overloaded equality operator and returns the object or actual null.
        /// Needed if you want to use the null-conditional operators with <see cref="UnityObject"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T OrNull<T>(this T self) where T : UnityObject
        {
            #pragma warning disable IDE0029
            return self == null ? null : self;
            #pragma warning restore IDE0029
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNullOrDestroyed(this UnityObject o) => o == null;

    }
}
