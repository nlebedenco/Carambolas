using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Carambolas
{
    /// <summary>
    /// Provides extensions to arrays 
    /// </summary>
    public static class ArrayExtensions
    {
        /// <summary> 
        /// Sorts the elements in an Array using the specified Comparison{T}. 
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Sort<T>(this T[] array, Comparison<T> comparison) => Array.Sort(array, comparison);

        /// <summary>
        /// /// Sorts the elements in an Array.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Sort<T>(this T[] array) => Array.Sort(array);

        /// <summary> 
        /// Sorts the elements in an entire Array using the IComparable{T} generic
        /// interface implementation of each element of the Array. 
        /// </summary> 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Sort<T>(this T[] array, IComparer<T> comparer) => Array.Sort(array, comparer);

        /// <summary>
        /// Determines whether an element is in the Array 
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains<T>(this T[] array, T value) => Array.Exists(array, item => item.Equals(value));

        /// <summary>
        /// Determines if the provided index is valid for the array.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidIndex<T>(this T[] array, int index) => index >= 0 && index < array.Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Find<T>(this T[] array, Predicate<T> match) => Array.Find(array, match);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FindIndex<T>(this T[] array, Predicate<T> match) => Array.FindIndex(array, match);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FindIndex<T>(this T[] array, int startIndex, Predicate<T> match) => Array.FindIndex(array, startIndex, match);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FindIndex<T>(this T[] array, int startIndex, int count, Predicate<T> match) => Array.FindIndex(array, startIndex, count, match);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FindLastIndex<T>(this T[] array, Predicate<T> match) => Array.FindLastIndex(array, match);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FindLastIndex<T>(this T[] array, int startIndex, Predicate<T> match) => Array.FindLastIndex(array, startIndex, match);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FindLastIndex<T>(this T[] array, int startIndex, int count, Predicate<T> match) => Array.FindLastIndex(array, startIndex, count, match);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearch<T>(this T[] array, T value) => Array.BinarySearch(array, value);
    }
}
