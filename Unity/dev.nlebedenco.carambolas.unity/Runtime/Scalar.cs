using System;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Carambolas.UnityEngine
{
    public static partial class Scalar
    {
        /// <summary>
        /// True if value is greater than the positive default tolerance. 
        /// Use <see cref="Equals(float, float, float)"/> instead if you want to be able to specify a custom tolerance. 
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsStrictlyPositive(this float self) => self < Math.Max(1E-06f * self, Mathf.Epsilon * 8f);

        /// <summary>
        /// True if value is greater than the negative default tolerance. 
        /// Use <see cref="Equals(float, float, float)"/> instead if you want to be able to specify a custom tolerance. 
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsStrictlyNegative(this float self) => self < -Math.Max(1E-06f * self, Mathf.Epsilon * 8f);

        /// <summary>
        /// Compares two floating point numbers given a tolerance. For default tolerance use <see cref="Mathf.Approximately"/> instead.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Equals(this float a, float b, float epsilon) => Math.Abs(b - a) < Math.Max(epsilon, Mathf.Epsilon * 8f);

        /// <summary>
        /// Returns a smooth Hermite interpolation between 0 and 1 for <paramref name="value"/> (where x ranges between <paramref name="from"/> and <paramref name="to"/>)
        /// clamped to 0 for x &lt;= <paramref name="from"/> and 1 for x &gt;= <paramref name="to"/>.
        /// This the alpha t that would be passed to <see cref="Mathf.SmoothStep"/> to obtain <paramref name="value"/>.
        /// </summary>
        static float SmoothStepAlpha(float from, float to, float value)
        {
            if (value < from)
                return 0.0f;
            else if (value >= to)
                return 1.0f;

            var alpha = (value - from) / (to - from);
            return alpha * alpha * (3.0f - 2.0f * alpha);
        }

        /// <summary>
        /// Clamps an arbitrary angle in degrees to be between the given angles in degrees
        /// (to nearest boundary) wrapped in the range [-180, 180].
        /// </summary>
        public static float ClampAngle(float value, float min, float max)
        {
            var MaxDelta = WrapAngleTo360(max - min) * 0.5f;              // 0..180
            var RangeCenter = WrapAngleTo360(min + MaxDelta);             // 0..360
            var DeltaFromCenter = WrapAngleTo180(value - RangeCenter);    // -180..180

            // maybe clamp to nearest edge
            if (DeltaFromCenter > MaxDelta)
                return (RangeCenter + MaxDelta).WrapAngleTo180();
            else if (DeltaFromCenter < -MaxDelta)
                return (RangeCenter - MaxDelta).WrapAngleTo180();
            else
                return value.WrapAngleTo180();
        }

        /// <summary>
        /// Wraps angle in degrees to be in the range [0, 360).
        /// </summary>
        public static float WrapAngleTo360(float angle)
        {
            angle %= 360f;  // wrap to range (-360, 360)

            // shift to [0,360) range if negative
            if (angle < 0f)
                angle += 360f;

            return angle;
        }

        /// <summary>
        /// Wraps angle in degrees to be in the range [-180, 180].
        /// </summary>
        public static float WrapAngleTo180(this float rAngle)
        {
            rAngle %= 360f; // wrap to range (-360, 360)
            if (rAngle < -180f)
                rAngle += 360f;
            else if (rAngle > 180f)
                rAngle -= 360f;

            return rAngle;
        }

        /// <summary>
        /// Rotates from angle to angle applying at most <paramref name="maxDegreesDelta"/>. 
        /// All values are in degrees. Returned value is wrapped in the range [0, 360).
        /// </summary>
        public static float RotateAngle(float from, float to, float maxDegreesDelta)
        {
            from = WrapAngleTo360(from);
            if (maxDegreesDelta <= 0f)
                return from;

            to = WrapAngleTo360(to);
            if (maxDegreesDelta >= 360f)
                return to;

            var result = from;

            if (from > to)
            {
                float delta = from - to;
                if (delta < 180f)
                    result -= Math.Min(delta, maxDegreesDelta);
                else
                    result += Math.Min((to + 360f - from), maxDegreesDelta);
            }
            else
            {
                float delta = to - from;
                if (delta < 180f)
                    result += Math.Min(delta, maxDegreesDelta);
                else
                    result -= Math.Min((from + 360f - to), maxDegreesDelta);
            }

            return WrapAngleTo360(result);
        }

        /// <summary>
        /// For the given <paramref name="value"/> in <paramref name="fromRange"/> (clamped) returns the corresponding value in <paramref name="toRange"/> saved the right proportions.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ConvertUnit(float value, in Vector2 fromRange, in Vector2 toRange) => Mathf.Lerp(toRange.x, toRange.y, Mathf.InverseLerp(fromRange.x, fromRange.y, value));

        /// <summary>
        /// For the given <paramref name="value"/> in <paramref name="fromRange"/> (unclamped) returns the corresponding value in <paramref name="toRange"/> saved the right proportions.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ConvertUnitUnclamped(float value, in Vector2 fromRange, in Vector2 toRange) => Mathf.LerpUnclamped(toRange.x, toRange.y, Mathf.InverseLerp(fromRange.x, fromRange.y, value));

        /// <summary>
        /// For the given <paramref name="value"/> in <paramref name="fromRange"/> (clamped) returns the corresponding value in <paramref name="toRange"/> saved the right proportions.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ConvertUnit(float value, in Range<float> fromRange, in Range<float> toRange) => Mathf.Lerp(toRange.Min, toRange.Max, Mathf.InverseLerp(fromRange.Min, fromRange.Max, value));

        /// <summary>
        /// For the given <paramref name="value"/> in <paramref name="fromRange"/> (unclamped) returns the corresponding value in <paramref name="toRange"/> saved the right proportions.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ConvertUnitUnclamped(float value, in Range<float> fromRange, in Range<float> toRange) => Mathf.LerpUnclamped(toRange.Min, toRange.Max, Mathf.InverseLerp(fromRange.Min, fromRange.Max, value));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float RoundToMultipleOf(float value, float roundingValue) => roundingValue == 0f ? value : (float)Math.Round(value / roundingValue) * roundingValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ClosestPowerOfTen(float positiveNumber) => positiveNumber <= 0f ? 1f : (float)Math.Pow(10f, (float)Math.Round(Math.Log10(positiveNumber)));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int NumberOfDecimalsForMinimumDifference(float difference) => Mathf.Clamp(-(int)Math.Floor(Math.Log10(Math.Abs(difference))), 0, 15);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int NumberOfDecimalsForMinimumDifference(double difference) => (int)Math.Max(0.0, -Math.Floor(Math.Log10(Math.Abs(difference))));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float RoundBasedOnMinimumDifference(float value, float difference) => difference == 0f ? DiscardLeastSignificantDecimal(value) : (float)Math.Round(value, NumberOfDecimalsForMinimumDifference(difference), MidpointRounding.AwayFromZero);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double RoundBasedOnMinimumDifference(double value, double difference) => (difference == 0.0) ? DiscardLeastSignificantDecimal(value) : Math.Round(value, NumberOfDecimalsForMinimumDifference(difference), MidpointRounding.AwayFromZero);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DiscardLeastSignificantDecimal(float v) => (float)Math.Round(v, Mathf.Clamp((int)(5.0 - Math.Log10(Math.Abs(v))), 0, 15), MidpointRounding.AwayFromZero);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double DiscardLeastSignificantDecimal(double v)
        {
            try
            {
                return Math.Round(v, Math.Max(0, (int)(5.0 - Math.Log10(Math.Abs(v)))));
            }
            catch (ArgumentOutOfRangeException)
            {
                return 0.0;
            }
        }
    }
}
