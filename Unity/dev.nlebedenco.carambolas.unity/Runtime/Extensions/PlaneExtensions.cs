using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using UnityEngine;

namespace Carambolas.UnityEngine
{
    public static class PlaneExtensions
    {
        /// <summary>
        /// Return true if point is inside the plane given a certain tolerance.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains(this in Plane plane, in Vector3 point, float epsilon = Vector3.kEpsilon) => Mathf.Abs(plane.GetDistanceToPoint(point)) < epsilon;

        /// <summary>
        /// Find the intersection with a segment. Returns true if line intersects plane in a single point.
        /// Otherwise false (intersection is undefined - either none or infinite points).
        /// </summary>
        public static bool TryGetIntersection(this in Plane plane, in Segment segment, out Vector3 intersection)
        {
            var displacement = (segment.End - segment.Start);
            var lineProjection = Vector3.Dot(displacement, plane.normal);

            // Check if line is not parallel to plane
            if (Mathf.Abs(lineProjection) > Vector3.kEpsilon)
            {
                var alpha = (plane.distance - Vector3.Dot(segment.Start, plane.normal)) / lineProjection;
                if (alpha < 0.0f || alpha > 1.0f)
                {
                    // line is not parallel but does not intersect the plane
                    intersection = Vector3.zero;
                    return false;
                }

                intersection = segment.Start + displacement * alpha;
                return true;
            }

            // line is parallel to plane intersection is undefined (either non-existant or infinite)
            intersection = Vector3.zero;
            return false;
        }
    }
}
