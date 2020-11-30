using System;

using UnityEngine;

namespace Carambolas.UnityEngine
{
    public static class RayExtensions
    {
        /// <summary>
        /// Find the closest between two infinite lines.
        /// Two non-parallel infinite lines which may or may not touch each other have a point on each line which are closest
        /// to each other. In this case, this function finds those two points and returns true. Otherwise, the lines are parallel, 
        /// the closest points are undefined and the function returns false.
        /// </summary>
        public static bool ClosestPoints(this in Ray self, in Ray other, out Vector3 closestPointOnSelf, out Vector3 closestPointOnOther)
        {
            var a = Vector3.Dot(self.direction, self.direction);
            var b = Vector3.Dot(self.direction, other.direction);
            var e = Vector3.Dot(other.direction, other.direction);
            var d = a * e - b * b;

            //lines are not parallel
            if (d != 0.0f)
            {
                var originDelta = self.origin - other.origin;
                var c = Vector3.Dot(self.direction, originDelta);
                var f = Vector3.Dot(other.direction, originDelta);
                var s = (b * f - c * e) / d;
                var t = (a * f - c * b) / d;

                closestPointOnSelf = self.origin + self.direction * s;
                closestPointOnOther = other.origin + other.direction * t;

                return true;
            }

            closestPointOnSelf = default;
            closestPointOnOther = default;

            return false;
        }

        /// <summary>
        /// Find the intersection point of two lines. Returns true if lines intersect in a single point, 
        /// otherwise false (intersection is undefined).
        /// Note that in 3d space two lines do not intersect most of the time. If the two lines are not in the 
        /// same plane, use <see cref="ClosestPoints"/> instead.
        /// </summary>
        public static bool TryGetIntersection(this in Ray self, in Ray other, out Vector3 intersection)
        {
            var delta = other.origin - self.origin;
            var cross12 = Vector3.Cross(self.direction, other.direction);
            var cross32 = Vector3.Cross(delta, other.direction);
            var factor = Vector3.Dot(delta, cross12);

            // is coplanar, and not parallel
            if (Mathf.Abs(factor) < 0.0001f && !cross12.IsZero())
            {
                var distance = Vector3.Dot(cross32, cross12) / cross12.sqrMagnitude;
                intersection = self.GetPoint(distance);
                return true;
            }
            else
            {
                intersection = Vector3.zero;
                return false;
            }
        }


    }
}
