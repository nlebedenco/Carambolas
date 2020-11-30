using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Carambolas.UnityEngine
{
    public static class Vector2Extensions
    {
        /// <summary>
        /// Check if this vector is zero (optionally given a tolerance).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsZero(this in Vector2 self, float epsilon = Vector2.kEpsilon) => self.sqrMagnitude < (epsilon * (double)epsilon);

        /// <summary>
        /// Compare two vectors given a tolerance. For default tolerance use operator == instead.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Equals(this in Vector2 self, in Vector2 other, float epsilon) => Vector2.SqrMagnitude(self - other) < (epsilon * (double)epsilon);

        /// <summary>
        /// Check if this vector has a magnitude of one (optionally given a tolerance).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNormalized(this in Vector2 self, float epsilon = Vector2.kEpsilon) => Math.Abs(1.0f - self.sqrMagnitude) < (epsilon * (double)epsilon);

        /// <summary>
        /// Convert the given cartesian coordinate pair (x, y) to polar coordinate system (radius, angle).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 CartesianToPolar(this in Vector2 cartesian) => new Vector2(Mathf.Sqrt(cartesian.x * cartesian.x + cartesian.y * cartesian.y), Mathf.Atan2(cartesian.y, cartesian.x));

        /// <summary>
        /// Convert the given polar coordinate (radius, angle) pair to cartesian (x, y) coordinate system. 
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 PolarToCartesian(this in Vector2 polar) => new Vector2(polar.x * Mathf.Cos(polar.y), polar.x * Mathf.Sin(polar.y));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Range<float> ToRange(this in Vector2 v) => new Range<float>(v.x, v.y);
    }
}
