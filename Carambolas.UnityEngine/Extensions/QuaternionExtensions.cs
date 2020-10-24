using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Carambolas.UnityEngine
{
    public static class QuaternionExtensions
    {
        /// <summary>
        /// Compare two quaternions given a tolerance corresponding to the Acos of the max acceptable angle for the quaternions to be considered equal.
        /// This can be used to enforce more or less strict tolerances. Default value of the <paramref name="epsilon"/> argument
        /// corresponds to 0.001 degrees. For Unity's default tolerance (which is aprox 0.0815 degrees) use operator == instead.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Equals(this in Quaternion self, in Quaternion other, double epsilon) => Quaternion.Dot(self, other) > epsilon;

        /// <summary>
        /// Simple identity test that supports the inverse identity
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsIdentity(this in Quaternion self) => Mathf.Approximately(self.x, 0f) && Mathf.Approximately(self.y, 0f) && Mathf.Approximately(self.z, 0f) && Mathf.Approximately(Mathf.Abs(self.w), 1f);

        /// <summary>
        /// Quaternions have two values that represent the same rotation.
        /// One is the axis with the base angle. The other is the -axis with
        /// a -angle. Negating the quaternion gives the same result, but
        /// represented in a different way
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion Negate(this in Quaternion self) => new Quaternion(-self.x, -self.y, -self.z, -self.w);

        /// <summary>
        /// Returns the Conjugate of the quaternion. The conjugate
        /// represents the opposite angular displacement.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion Conjugate(this in Quaternion self) => new Quaternion(-self.x, -self.y, -self.z, self.w);

        /// <summary>
        /// Returns a vector representing this quaternion's forward direction
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 Forward(this in Quaternion self) => new Vector3(2 * (self.x * self.z + self.w * self.y), 2 * (self.y * self.z - self.w * self.x), 1 - 2 * (self.x * self.x + self.y * self.y));

        /// <summary>
        /// Returns a vector representing this quaternion's up direction
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 Up(this in Quaternion self) => new Vector3(2 * (self.x * self.y - self.w * self.z), 1 - 2 * (self.x * self.x + self.z * self.z), 2 * (self.y * self.z + self.w * self.x));

        /// <summary>
        /// Returns a vector representing this quaternion's right direction
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 Right(this in Quaternion self) => new Vector3(1 - 2 * (self.y * self.y + self.z * self.z), 2 * (self.x * self.y + self.w * self.z), 2 * (self.x * self.z - self.w * self.y));

        /// <summary>
        /// Creates a quaternion that represents the rotation required to turn the this quaternion into the specified one.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion RotationTo(this in Quaternion self, in Quaternion other) => Quaternion.Inverse(self) * other;

        /// <summary>
        /// Creates a quaternion that represents the rotation that results when this quaternion is locally applied to the specified one.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion OrientationTo(this in Quaternion self, in Quaternion other) => other * Quaternion.Inverse(self);

        /// <summary>
        /// An alternative to <see cref="Quaternion.Angle"/> that can be passed a specific tolerance corresponding to the Acos of the max acceptable angle
        /// for two quaternions to be considered equal.
        /// </summary>
        /// <param name="epsilon">A value in the range [0, 1] that is the complement to the acos of the max acceptable angle for two quaternions to be considered equal. 
        /// Default value corresponds 0.001 degrees so that acos(1 - <paramref name="epsilon"/>) = 0.001 degrees. </param>
        public static float AngleTo(this in Quaternion from, in Quaternion to, double epsilon = 1.5230905e-10)
        {
            var dot = Quaternion.Dot(from, to);
            return dot > (1.0 - epsilon) ? 0f : Mathf.Acos(Math.Min(Math.Abs(dot), 1f)) * 360f / Mathf.PI;
        }

        /// <summary>
        /// Obtains a twist rotation (around <paramref name="axis"/>) that is part of the quaternion.
        /// </summary>
        public static Quaternion ExtractTwist(this in Quaternion self, in Vector3 axis)
        {
            var normalized = axis.normalized;
            var dot = Vector3.Dot(new Vector3(self.x, self.y, self.z), normalized);
            normalized *= dot;
            var twist = new Quaternion(normalized.x, normalized.y, normalized.z, self.w);
            twist.Normalize();
            return twist;
        }

        /// <summary>
        /// Decompose this quaternion into a twist rotation (around <paramref name="twistAxis"/>) and a swing rotation 
        /// around a vector perpendicular to <paramref name="twistAxis"/>.
        /// To rebuild, use: Quaternion q = swing * twist;
        /// For more info see: http://www.alinenormoyle.com/weblog/?p=726
        /// </summary>
        public static void DecomposeSwingTwist(this in Quaternion self,in Vector3 twistAxis, out Quaternion swing, out Quaternion twist)
        {
            twist = ExtractTwist(in self, in twistAxis);
            swing = self * Quaternion.Inverse(twist);
        }

        /// <summary>
        /// Extracts out the rotational axis and angles for a swing and twist in the range [-180, 180]. 
        /// </summary>
        public static void DecomposeTwistSwingToAngleAxis(this in Quaternion self, in Vector3 twistAxis, out float swingAngle, out Vector3 swingAxis, out float twistAngle)
        {
            DecomposeSwingTwist(in self, in twistAxis, out Quaternion swing, out Quaternion twist);
            twist.ToAngleAxis(out twistAngle, out Vector3 ignored);

            if (self == twist)
            {
                swingAngle = 0f;
                swingAxis = twist.Right();
            }
            else
            {
                swing.ToAngleAxis(out swingAngle, out swingAxis);
            }
        }
    }       
}
