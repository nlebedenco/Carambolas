using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Carambolas.UnityEngine
{
    /// <summary>
    /// Finite line segment defined by a start point and an end point.
    /// </summary>
    [Serializable]
    public struct Segment: IFormattable
    {
        public Segment(in Vector3 from, in Vector3 to) => (start, end) = (from, to);

        [SerializeField]
        private Vector3 start;

        /// <summary>
        /// The origin point of the line.
        /// </summary>
        public Vector3 Start { get => start; set => start = value; }

        [SerializeField]
        private Vector3 end;

        /// <summary>
        /// The end point of the line.
        /// </summary>
        public Vector3 End { get => end; set => end = value; }


        /// <summary>
        /// Returns a nicely formatted string for this line.
        /// </summary>
        public override string ToString() => $"[{start}; {end}]";

        /// <summary>
        /// Returns a nicely formatted string for this line.
        /// </summary>
        public string ToString(string format) => string.Format(format, start, end);

        public string ToString(string format, IFormatProvider provider) => string.Format(provider, format, start, end);

        public static explicit operator Vector3(in Segment other) => other.end - other.start;

        /// <summary>
        /// Return true if point is inside the segment given a certain tolerance.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(in Vector3 point, float epsilon = Vector3.kEpsilon) => Vector3.Cross((Vector3)this, point - Start).IsZero(epsilon);

        public Vector3 ClosestPointOnSegment(in Vector3 position)
        {
            var vector = (Vector3)this;
            var projectedPosition = Vector3.Project(position - Start, vector);

            if (Vector3.Dot(projectedPosition, vector) < 0f)
                return Start;
            else if (projectedPosition.sqrMagnitude > vector.sqrMagnitude)
                return End;
            else
                return Start + projectedPosition;
        }

        /// <summary>
        /// Find the closest two points between this segment and a sphere
        /// </summary>
        public void ClosestPointsOnSegment(in Vector3 sphereCenter, float sphereRadius, out Vector3 closestPointOnSelf, out Vector3 closestPointOnSphere)
        {
            var vector = (Vector3)this;
            var sqrMagnitude = vector.sqrMagnitude;

            // If the segment's length approaches 0 Start and End are considered equivalent and the ONLY point of the segment.
            if (sqrMagnitude < (Vector3.kEpsilon * (double)Vector3.kEpsilon))
            {
                closestPointOnSelf = Start;
            }
            else
            {
                var segmentLength = Mathf.Sqrt(sqrMagnitude);
                var segmentDirection = vector / segmentLength;

                var startToSphereCenter = sphereCenter - Start;
                var dot = Vector3.Dot(segmentDirection, startToSphereCenter);

                closestPointOnSelf = start + (segmentDirection * Mathf.Min(Mathf.Max(dot, 0f), segmentLength));
            }

            // If the sphere radius approaches 0 the center is considered the ONLY point of the sphere.
            if (Mathf.Approximately(0f, sphereRadius))
                closestPointOnSphere = sphereCenter;
            else
            {
                var delta = sphereCenter - closestPointOnSelf;
                var sqrDeltaMagnitude = delta.sqrMagnitude;
                // If the distance between the closest point on the segment and the sphere center approaches 0 they're the same point.
                if (sqrDeltaMagnitude < (Vector3.kEpsilon * (double)Vector3.kEpsilon))
                    closestPointOnSphere = closestPointOnSelf;
                else
                {
                    var deltaLength = Mathf.Sqrt(sqrDeltaMagnitude);
                    var deltaDirection = vector / deltaLength;
                    closestPointOnSphere = closestPointOnSelf + (deltaDirection * (deltaLength - sphereRadius));
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetDistanceToPoint(in Vector3 point) => (ClosestPointOnSegment(point) - point).magnitude;

        /// <summary>
        /// Find the closest points between this segment and another segment. If the segments are parallel they have an infinite 
        /// number of points within the same distance. Returns true if the two line segments are non-parallel; otherwise false. 
        /// Based on http://geomalgorithms.com/a07-_distance.html
        /// </summary>
        public bool TryGetClosestPointsOnSegment(in Segment other, out Vector3 closestPointOnSelf, out Vector3 closestPointOnOther)
        {
            var u = (Vector3)this;
            var v = (Vector3)other;
            var w = start - other.start;

            var a = Vector3.Dot(u, u);          // always >= 0
            var b = Vector3.Dot(u, v);
            var c = Vector3.Dot(v, v);          // always >= 0
            var d = Vector3.Dot(u, w);
            var e = Vector3.Dot(v, w);
            var D = a * c - b * b;              // always >= 0

            float sc, sN, sD = D;               // sc = sN / sD, default sD = D >= 0
            float tc, tN, tD = D;               // tc = tN / tD, default tD = D >= 0

            var areParallel = false;
            // compute the line parameters of the two closest points
            if (D < Vector3.kEpsilon)
            {
                // the lines are almost parallel
                sN = 0.0f;         // force using point P0 on segment S1
                sD = 1.0f;         // to prevent possible division by 0.0 later
                tN = e;
                tD = c;
                areParallel = true;
            }
            else
            {
                areParallel = false;

                // get the closest points on the infinite lines
                sN = (b * e - c * d);
                tN = (a * e - b * d);
                if (sN < 0.0f)
                {
                    // sc < 0 => the s=0 edge is visible
                    sN = 0.0f;
                    tN = e;
                    tD = c;
                }
                else if (sN > sD)
                {
                    // sc > 1  => the s=1 edge is visible
                    sN = sD;
                    tN = e + b;
                    tD = c;
                }
            }

            if (tN < 0.0f)
            {
                // tc < 0 => the t=0 edge is visible
                tN = 0.0f;

                // recompute sc for this edge
                if (-d < 0.0f)
                    sN = 0.0f;
                else if (-d > a)
                    sN = sD;
                else
                {
                    sN = -d;
                    sD = a;
                }
            }
            else if (tN > tD)
            {
                // tc > 1  => the t=1 edge is visible
                tN = tD;
                // recompute sc for this edge
                if ((-d + b) < 0.0)
                    sN = 0;
                else if ((-d + b) > a)
                    sN = sD;
                else
                {
                    sN = (-d + b);
                    sD = a;
                }
            }

            // finally do the division to get sc and tc
            sc = (Mathf.Abs(sN) < Vector3.kEpsilon ? 0.0f : sN / sD);
            tc = (Mathf.Abs(tN) < Vector3.kEpsilon ? 0.0f : tN / tD);

            // Delta the two closest points
            // Vector3 dP = w + (sc * u) - (tc * v);  // =  S1(sc) - S2(tc)

            closestPointOnSelf = start + sc * u;
            closestPointOnOther = other.start + tc * v;

            return !areParallel;
        }

        /// <summary>
        /// Find if the segment intersect with another segment and if true also return the intersection point.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetIntersection(in Segment other, out Vector3 intersection) => TryGetClosestPointsOnSegment(other, out intersection, out Vector3 reciprocal) && intersection == reciprocal;

        /// <summary>
        /// Find if the segment intersects with a sphere and return the intersection point that is closest to the line start
        /// as described in https://en.wikipedia.org/wiki/Line–sphere_intersection.
        /// </summary>
        public bool TryGetIntersection(in Vector3 sphereCenter, float sphereRadius, out Vector3 intersection)
        {
            var vector = (Vector3)this;
            var sqrMagnitude = vector.sqrMagnitude;

            // If the sphere radius approaches 0 check if sphereCenter belongs to the segment.
            if (Mathf.Approximately(0f, sphereRadius))
            {
                if (Contains(sphereCenter, sphereRadius))
                {
                    intersection = sphereCenter;
                    return true;
                }
            }
            else
            {
                // If the segment's length approaches 0 check if Start is in the sphere's surface instead.
                if (sqrMagnitude < (Vector3.kEpsilon * (double)Vector3.kEpsilon))
                {
                    if (Mathf.Approximately(sphereRadius * sphereRadius, (sphereCenter - Start).sqrMagnitude))
                    {
                        intersection = Start;
                        return true;
                    }
                }
                else
                {
                    var segmentLength = Mathf.Sqrt(sqrMagnitude);
                    var segmentDirection = vector / segmentLength;

                    var startToSphereCenter = sphereCenter - Start;
                    var dot = Vector3.Dot(segmentDirection, startToSphereCenter);
                    var delta = (dot * dot) - startToSphereCenter.sqrMagnitude + (sphereRadius * sphereRadius);

                    // If delta < 0 there's no solution (segment does not intersect)
                    // If delta == 0 there's a single intersection point
                    // If delta > 0 there are two intersection points
                    if (delta >= (Vector3.kEpsilon * (double)Vector3.kEpsilon))
                    {
                        var alpha = (dot - Mathf.Sqrt(delta)) / segmentLength;
                        if (alpha >= 0f && alpha <= 1.0f)
                        {
                            intersection = start + vector * alpha;
                            return true;
                        }
                    }

                }
            }
            intersection = default;
            return false;           
        }
    }
}
