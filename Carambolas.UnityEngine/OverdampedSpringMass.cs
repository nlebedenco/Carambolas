using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Carambolas.UnityEngine
{
    /// <summary>
    /// Models a mass of 1 unit attached to an ideal spring that is overdamped. The spring mass returns to its resting position
    /// with no overshoot thus never producing an oscillation. It differs from a critically damped spring in that the system 
    /// returns quickly at start but eases out as it gets close to the target (when distance &lt; 1 unit) so it doesn't necessarily
    /// reaches equilibrium in the minimum amount of time.
    /// </summary>
    public static class OverdampedSpringMass
    {
        /// <summary>
        /// Interpolate from <paramref name="current"/> to <paramref name="target"/> scaled by displacement (distance) 
        /// from the resting position (<paramref name="target"/>), so it has a high start speed and a slow ease out.
        /// Form a uniform motion to target <see cref="Mathf.MoveTowards"/>
        /// </summary>
        public static float MoveTowards(float current, float target, float deltaTime, float springConstant, float epsilon = 1e-4f)
        {           
            if (!deltaTime.IsStrictlyPositive() || current == target || !springConstant.IsStrictlyPositive())
                return current;

            var displacement = target - current;

            // If distance is too small, just snap to target.
            if (Scalar.Equals(displacement, 0f, epsilon))
                return target;

            // From Hooke's law S = (k * x * t * t) so when (k * t * t) > 1 the spring mass got to its resting position before the whole deltaTime was over, hence the clamp to avoid overshooting.
            return current + (displacement * Math.Min(1f, springConstant * deltaTime * deltaTime)); 
        }

        /// <summary>
        /// Interpolate from <paramref name="current"/> to <paramref name="target"/> scaled by displacement (distance) 
        /// from the resting position (<paramref name="target"/>), so it has a high start speed and a slow ease out.
        /// Form a uniform motion to target <see cref="Vector2.MoveTowards"/>
        /// </summary>
        public static Vector2 MoveTowards(in Vector2 current, Vector2 target, float deltaTime, float springConstant, float epsilon = Vector2.kEpsilon)
        {
            if (!deltaTime.IsStrictlyPositive() || !springConstant.IsStrictlyPositive())
                return current;

            var displacement = target - current;
            var sqrMagnitude = displacement.sqrMagnitude;

            // If distance is too small, just snap to target.
            if (sqrMagnitude < (Vector2.kEpsilon * (double)Vector2.kEpsilon))
                return target;

            var magnitude = Mathf.Sqrt(sqrMagnitude);
            var direction = displacement / magnitude;

            // From Hooke's law S = (k * x * t * t) so when (k * t * t) > 1 the spring mass got to its resting position before the whole deltaTime was over, hence the clamp to avoid overshooting.
            return current + (displacement * Math.Min(1f, springConstant * deltaTime * deltaTime));
        }

        /// <summary>
        /// Interpolate from <paramref name="current"/> to <paramref name="target"/> scaled by displacement (distance) 
        /// from the resting position (<paramref name="target"/>), so it has a high start speed and a slow ease out.
        /// Form a uniform motion to target <see cref="Vector3.MoveTowards"/>
        /// </summary>
        public static Vector3 MoveTowards(in Vector3 current, in Vector3 target, float deltaTime, float springConstant, float epsilon = Vector3.kEpsilon)
        {
            if (!deltaTime.IsStrictlyPositive() || !springConstant.IsStrictlyPositive())
                return current;

            var displacement = target - current;
            var sqrMagnitude = displacement.sqrMagnitude;

            // If distance is too small, just snap to target.
            if (sqrMagnitude < (Vector3.kEpsilon * (double)Vector3.kEpsilon))
                return target;

            var magnitude = Mathf.Sqrt(sqrMagnitude);
            var direction = displacement / magnitude;

            // From Hooke's law S = (k * x * t * t) so when (k * t * t) > 1 the spring mass got to its resting position before the whole deltaTime was over, hence the clamp to avoid overshooting.
            return current + (displacement * Math.Min(1f, springConstant * deltaTime * deltaTime));
        }

        /// <summary>
        /// Interpolate from <paramref name="current"/> to <paramref name="target"/> scaled by displacement (distance) 
        /// from the resting position (<paramref name="target"/>), so it has a high start speed and a slow ease out.
        /// Form a uniform motion to target <see cref="Vector4.MoveTowards"/>
        /// </summary>
        public static Vector4 MoveTowards(in Vector4 current, in Vector4 target, float deltaTime, float springConstant, float epsilon = Vector4.kEpsilon)
        {
            if (!deltaTime.IsStrictlyPositive() || !springConstant.IsStrictlyPositive())
                return current;

            var displacement = target - current;
            var sqrMagnitude = displacement.sqrMagnitude;

            // If distance is too small, just snap to target.
            if (sqrMagnitude < (Vector4.kEpsilon * (double)Vector4.kEpsilon))
                return target;

            var magnitude = Mathf.Sqrt(sqrMagnitude);
            var direction = displacement / magnitude;

            // From Hooke's law S = (k * x * t * t) so when (k * t * t) > 1 the spring mass got to its resting position before the whole deltaTime was over, hence the clamp to avoid overshooting.
            return current + (displacement * Math.Min(1f, springConstant * deltaTime * deltaTime));
        }

        /// <summary>
        /// Interpolate from <paramref name="from"/> to <paramref name="to"/> scaled by displacement (distance) 
        /// from the resting position (<paramref name="to"/>), so it has a high start speed and ease out to a uniform motion.
        /// Form a uniform motion to target <see cref="Quaternion.RotateTowards"/>
        /// </summary>
        /// <param name="epsilon">A value in the range [0, 1] that is the complement to the acos of the max acceptable angle for two quaternions to be considered equal. 
        /// Default value corresponds 0.001 degrees so that acos(1 - <paramref name="epsilon"/>) = 0.001 degrees. </param>
        public static Quaternion RotateTowards(in Quaternion from, in Quaternion to, float deltaTime, float springConstant, float epsilon = 1.5230905e-10f)
        {
            if (!deltaTime.IsStrictlyPositive() || !springConstant.IsStrictlyPositive())
                return from;

            var dot = Quaternion.Dot(from, to);

            // If distance is too small, just snap to target.
            if (dot > 1.0 - epsilon)
                return to;
            
            var displacement = Mathf.Acos(Math.Min(Math.Abs(dot), 1f)) * 360f / Mathf.PI;

            // From Hooke's law S = (k * x * t * t) so when (k * t * t) > 1 the spring mass got to its resting position before the whole deltaTime was over.
            // For Slerp we must pass an alpha = S / x = (k * x * t * t) / x = k * t * t but if displacement (x) is below 1 degree we apply a uniform motion 
            // calculated as if displacement = 1 to avoid a long exp decay in which case alpha = (k * 1 * t * t) / x
            var alpha = springConstant * deltaTime * deltaTime;
            if (displacement < 1f)
                alpha /= displacement;
            
            return Quaternion.SlerpUnclamped(from, to, Math.Min(1f, alpha));
        }

        /// <summary>
        /// Interpolate from <paramref name="current"/> to <paramref name="target"/> scaled by displacement (distance) 
        /// from the resting position (<paramref name="target"/>), so it has a high start speed and ease out to a uniform motion.
        /// Form a uniform motion to target <see cref="Mathf.MoveTowards"/>
        /// </summary>
        public static float MoveTowardsAngle(in float current, float target, float deltaTime, float springConstant, float epsilon = 1e-3f)
        {
            if (!deltaTime.IsStrictlyPositive() || current == target || !springConstant.IsStrictlyPositive())
                return current;

            var displacement = Mathf.DeltaAngle(current, target);

            // If distance is too small, just snap to target.
            if (Scalar.Equals(displacement, 0f, epsilon))
                return target;

            // From Hooke's law S = (k * x * t * t) so when (k * t * t) > 1 the spring mass got to its resting position before the whole deltaTime was over.
            // If displacement (x) is below 1 degree we apply a uniform motion calculated as if displacement = 1 to avoid a long exp decay.
            return current + Math.Min(displacement, Math.Max(1f, displacement) * springConstant * deltaTime * deltaTime);
        }

        /// <summary>
        /// Interpolate each component from <paramref name="current"/> to <paramref name="target"/> in degrees optionally scaled by displacement (angular distance) 
        /// from the resting position (<paramref name="target"/>), so it has a high start speed and ease out to a uniform motion.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 MoveTowardsAngle(in Vector3 current, Vector3 target, float deltaTime, float springConstant, float epsilon = 1e-3f)
            => new Vector3(
                MoveTowardsAngle(current.x, target.x, deltaTime, springConstant),
                MoveTowardsAngle(current.y, target.y, deltaTime, springConstant),
                MoveTowardsAngle(current.z, target.z, deltaTime, springConstant));
    }
}

