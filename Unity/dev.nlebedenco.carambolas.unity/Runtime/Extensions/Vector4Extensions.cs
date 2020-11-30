using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Carambolas.UnityEngine
{
    public static class Vector4Extensions
    {
        /// <summary>
        /// Check if this vector is zero (optionally given a tolerance).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsZero(this in Vector4 self, float epsilon = Vector4.kEpsilon) => self.sqrMagnitude < (epsilon * (double)epsilon);

        /// <summary>
        /// Compare two vectors given a tolerance. For default tolerance use operator == instead.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Equals(this in Vector4 self, in Vector4 other, float epsilon) => Vector4.SqrMagnitude(self - other) < (epsilon * (double)epsilon);

        /// <summary>
        /// Check if this vector has a magnitude of one (optionally given a tolerance).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNormalized(this in Vector4 self, float epsilon = Vector4.kEpsilon) => Math.Abs(1.0f - self.sqrMagnitude) < (epsilon * (double)epsilon);
    }
}
