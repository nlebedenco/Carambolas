using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Carambolas.UnityEngine
{
    public static class Vector3Extensions
    {
        /// <summary>
        /// Check if this vector is zero (optionally given a tolerance).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsZero(this in Vector3 self, float epsilon = Vector3.kEpsilon) => self.sqrMagnitude < (epsilon * (double)epsilon);

        /// <summary>
        /// Compare two vectors given a tolerance. For default tolerance use operator == instead.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Equals(this in Vector3 self, in Vector3 other, float epsilon) => Vector3.SqrMagnitude(self - other) < (epsilon * (double)epsilon);

        /// <summary>
        /// Check if this vector has a magnitude of one (optionally given a tolerance).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNormalized(this in Vector3 self, float epsilon = Vector3.kEpsilon) => Math.Abs(1.0f - self.sqrMagnitude) < (epsilon * (double)epsilon);

        /// <summary>
        /// Check if the position represented by this vector is inside the <paramref name="camera"/> frustum.
        /// Note that the position although inside the camera frustum may not be visible due to obstruction. 
        /// Use a ray cast if you want to test for visibility.
        /// </summary>
        public static bool IsOnCamera(this in Vector3 self, Camera camera)
        {
            if (camera == null)
                return false;

            var screenPosition = camera.WorldToScreenPoint(self);
            return camera.pixelRect.Contains(screenPosition) && screenPosition.z > 0f;
        }

        /// <summary>
        /// Treat the vector components as euler angles and wrap in the range [-180 to 180]. 
        /// This is important when working with headings as operations with angles in the 
        /// range [0, 360) may yield unexpected results. For example:
        /// (-10 + 10) / 2 = 0   : as expected
        /// (350 + 10) / 2 = 180 : not as expected 
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 NormalizedAngles(this in Vector3 angles) => new Vector3(angles.x.WrapAngleTo180(), angles.y.WrapAngleTo180(), angles.z.WrapAngleTo180());

        /// <summary>
        /// Find two vectors that are orthogonal to this vector. If this vector should be normalized as well use 
        /// UnityEngine.Vector3.OrthoNormalize(ref v, ref up, ref right) instead where v is this vector and up and right
        /// are the orthogonals previously initialized to Vector3.up and Vector3.right respectively.
        /// </summary>
        public static void FindOrthogonals(this in Vector3 self, out Vector3 upwards, out Vector3 right)
        {
            upwards = Vector3.up;
            right = Vector3.right;
            var normal = self;
            Vector3.OrthoNormalize(ref normal, ref upwards, ref right);
        }

        /// <summary>
        ///  Returns the signed angle in degrees between from and to. Unlike <see cref="Vector3.SignedAngle(Vector3, Vector3, Vector3)"/>,
        ///  this method returns 0 instead of 90 if any of the vectors is zero after normalization.
        /// </summary>
        /// <param name="from">The angle extends round from this vector.</param>
        /// <param name="to">The angle extends round to this vector.</param>
        /// <param name="upwards">The normal to plane where the angle is to be measured.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SignedAngleTo(this in Vector3 from, in Vector3 to, in Vector3 upwards) => Mathf.Atan2(Vector3.Dot(upwards, Vector3.Cross(from, to)), Vector3.Dot(from, to)) * Mathf.Rad2Deg;

        /// <summary>
        /// Project both <paramref name="from"/> and <paramref name="to"/> vectors on the floor plane orthogonal 
        /// to the <paramref name="upwards"/> vector and returns the shortest angle between the projections in degrees wrapped to [-180, 180].
        /// </summary>
        public static float HorizontalSignedAngleTo(this in Vector3 from, in Vector3 to, in Vector3 upwards)
        {
            Debug.AssertFormat(upwards.IsNormalized(), "Up vector must be normalized.");

            from.Normalize();
            to.Normalize();

            // Find vector projections on the floor plane orthogonal to the up vector.
            var fromProjection = (from - (upwards * Vector3.Dot(from, upwards))).normalized;
            var toProjection = (to - (upwards * Vector3.Dot(to, upwards))).normalized;

            return fromProjection.SignedAngleTo(toProjection, upwards);
        }

        /// <summary>
        /// Project both <paramref name="from"/> and <paramref name="to"/> vectors on the floor plane orthogonal 
        /// to the <paramref name="upwards"/> vector and returns the delta of the projections.
        /// </summary>
        public static Vector3 HorizontalDeltaTo(this in Vector3 from, in Vector3 to, in Vector3 upwards)
        {
            Debug.AssertFormat(upwards.IsNormalized(), "Up vector must be normalized.");

            from.Normalize();
            to.Normalize();

            // Find vector projections on the floor plane orthogonal to the up vector.
            var fromProjection = (from - (upwards * Vector3.Dot(from, upwards))).normalized;
            var toProjection = (to - (upwards * Vector3.Dot(to, upwards))).normalized;

            return toProjection - fromProjection;
        }

        /// <summary>
        /// Project both <paramref name="from"/> and <paramref name="to"/> vectors on the floor plane orthogonal 
        /// to the <paramref name="upwards"/> vector and returns the distance between the projections.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float HorizontalDistanceTo(this in Vector3 from, in Vector3 to, in Vector3 upwards) => from.HorizontalDeltaTo(to, upwards).magnitude;

        /// <summary>
        /// Calculate the dot product distance of a <paramref name="direction"/> to the axes in a coordinate system defined as 
        /// O(<paramref name="right"/>,<paramref name="upwards"/>,<paramref name="forward"/>).
        /// Consider 'O' the first person view of the player and <paramref name="direction"/> a vector pointing to an enemy.
        /// The resulting <paramref name="dotDistance"/> is then in the form (Sin(Elevation), Cos(Azimuth)).
        /// Positive elevation (pitch) means enemy is on top of crosshair, negative means below.
        /// Positive azimuth (yaw) means enemy is on the right of crosshair, negative means left.
        /// Note that azimuth sign represents left/right and not front/behind. Front/behind is the funtion's return value.
        /// </summary>
        /// <returns>
        /// true if direction is facing forward (direction dot forward >= 0f)
        /// </returns>
        public static bool TryGetDotDistance(this in Vector3 direction, in Vector3 forward, in Vector3 right, in Vector3 upwards, out Vector2 dotDistance)
        {
            direction.Normalize();

            // Find projected vector on the floor plane orthogonal to the up vector.
            var floorProjection = (direction - (upwards * Vector3.Dot(direction, upwards))).normalized;

            // Figure out if projection is on right or left.
            var azimuthSign = (Vector3.Dot(floorProjection, right) < 0f) ? -1f : 1f;

            dotDistance.x = Vector3.Dot(direction, upwards);

            var dotForward = Vector3.Dot(floorProjection, forward);
            dotDistance.y = azimuthSign * Mathf.Abs(dotForward);

            return dotForward >= 0f;
        }

        /// <summary>
        /// Calculate Elevation and Azimuth of a <paramref name="direction"/> to the axes in a coordinate system defined as
        /// O(<paramref name="right"/>,<paramref name="upwards"/>,<paramref name="forward"/>).
        /// Consider 'O' the first person view of the player and <paramref name="direction"/> a vector pointing to an enemy.
        /// Positive elevation (pitch) means enemy is on top of crosshair, negative means below.
        /// Positive azimuth (yaw) means enemy is on the right of crosshair, negative means left.
        /// Note that the azimuth sign represents left/right and not front/behind.
        /// </summary>
        /// <returns>
        /// (Elevation, Azimuth) in radians.
        /// </returns>
        public static Vector2 AzimuthAndElevation(this in Vector3 direction, in Vector3 forward, in Vector3 right, in Vector3 upwards)
        {
            direction.Normalize();

            // Find projected vector on the floor plane orthogonal to the up vector.
            var floorProjection = (direction - (upwards * Vector3.Dot(direction, upwards))).normalized;

            // Figure out if projection is on right or left.
            var azimuthSign = (Vector3.Dot(floorProjection, right) < 0f) ? -1f : 1f;
            var elevationSin = Vector3.Dot(direction, upwards);
            var azimuthCos = Vector3.Dot(direction, forward);

            // Convert to Angles (in Radians).
            return new Vector2(Mathf.Asin(elevationSin), Mathf.Acos(azimuthCos) * azimuthSign);
        }


        #region Rotation

        /// <summary>
        /// An alternative to <see cref="Quaternion.FromToRotation"/> that can be passed a tolerance corresponding to the Acos of the max acceptable angle
        /// for the quaternions to be considered equal. Default value of the <paramref name="epsilon"/> argument corresponds to 0.001 degrees. 
        /// </summary>
        public static Quaternion FromToRotation(this in Vector3 from, in Vector3 to, double epsilon = 0.999999999847691)
        {
            from.Normalize();
            to.Normalize();

            if (from.IsZero() || to.IsZero())
                return Quaternion.identity;

            var dot = Vector3.Dot(from, to);

            if (dot > epsilon)
                return Quaternion.identity;

            float sinTheta, cosTheta;
            Vector3 axis;

            if (dot < -epsilon)
            {
                sinTheta = 1f;
                cosTheta = 0f;
                axis = Quaternion.LookRotation(from).Up();
            }
            else
            {
                var theta = Mathf.Acos(dot) * 0.5f;
                sinTheta = Mathf.Sin(theta);
                cosTheta = Mathf.Cos(theta);
                axis = Vector3.Cross(from, to).normalized;
            }

            return new Quaternion(axis.x * sinTheta, axis.y * sinTheta, axis.z * sinTheta, cosTheta);
        }

        /// <summary>
        /// An alternative to <see cref="Quaternion.FromToRotation"/> that can be passed an upwards axis and a specific tolerance corresponding to the Acos of the max acceptable angle
        /// for the quaternions to be considered equal. Default value of the <paramref name="epsilon"/> argument corresponds to 0.001 degrees. 
        /// </summary>
        public static Quaternion FromToRotation(this in Vector3 from, in Vector3 to, in Vector3 upwards, double epsilon = 0.999999999847691)
        {
            from.Normalize();
            to.Normalize();

            if (from.IsZero() || to.IsZero())
                return Quaternion.identity;

            var dot = Vector3.Dot(from, to);

            if (dot > epsilon)
                return Quaternion.identity;

            var fromRotation = Quaternion.LookRotation(from, upwards);

            if (dot < -epsilon)
            {
                var axis = fromRotation.Up();
                return new Quaternion(axis.x, axis.y, axis.z, 0f);
            }

            var toRotation = Quaternion.LookRotation(to, upwards);
            return fromRotation.RotationTo(toRotation);
        }


        #endregion

        #region Closest Point

        /// <summary>
        /// Finds the closest point on a segment
        /// </summary>
        public static Vector3 ClosestPointOnSegment(this in Vector3 position, in Segment segment) => segment.ClosestPointOnSegment(in position);
      

        /// <summary>
        /// Find the closest point on a plane
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ClosestPointOnPlane(this in Vector3 position, in Plane plane) => plane.ClosestPointOnPlane(position);

        /// <summary>
        /// Find the closest point on a segment defined in a transform local space.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ClosestPointOnSegment(this in Vector3 position, Transform transform, in Segment segment) => transform.TransformPoint(transform.InverseTransformPoint(position).ClosestPointOnSegment(segment));

        /// <summary>
        /// Finds the closest point on a triangle defined by the vertices {a, b, c}
        /// </summary>
        public static Vector3 ClosestPointOnTriangle(this in Vector3 position, in Vector3 vertexA, in Vector3 vertexB, in Vector3 vertexC)
        {
            //Check if P in vertex region outside A
            var ab = vertexB - vertexA;
            var ac = vertexC - vertexA;
            var ap = position - vertexA;

            var d1 = Vector3.Dot(ab, ap);
            var d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0.0f && d2 <= 0.0f)
                return vertexA;

            //Check if P in vertex region outside B
            var bp = position - vertexB;
            var d3 = Vector3.Dot(ab, bp);
            var d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0.0f && d4 <= d3)
                return vertexB; // barycentric coordinates (0,1,0)

            //Check if P in edge region of AB, if so return projection of P onto AB
            var vc = d1 * d4 - d3 * d2;
            if (vc <= 0.0f && d1 >= 0.0f && d3 <= 0.0f)
                return vertexA + (d1 / (d1 - d3)) * ab; //Barycentric coordinates (1-v,v,0)

            //Check if P in vertex region outside C
            var cp = position - vertexC;
            var d5 = Vector3.Dot(ab, cp);
            var d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0.0f && d5 <= d6)
                return vertexC; //Barycentric coordinates (0,0,1)

            //Check if P in edge region of AC, if so return projection of P onto AC
            var vb = d5 * d2 - d1 * d6;
            if (vb <= 0.0f && d2 >= 0.0f && d6 <= 0.0f)
                return vertexA + (d2 / (d2 - d6)) * ac; //Barycentric coordinates (1-w,0,w)

            //Check if P in edge region of BC, if so return projection of P onto BC
            var va = d3 * d6 - d5 * d4;
            if (va <= 0.0f && (d4 - d3) >= 0.0f && (d5 - d6) >= 0.0f)
                return vertexB + ((d4 - d3) / ((d4 - d3) + (d5 - d6))) * (vertexC - vertexB); //Barycentric coordinates (0,1-w,w)

            //P inside face region. Compute Q through its barycentric coordinates (u,v,w)
            var denom = 1.0f / (va + vb + vc);
            var v2 = vb * denom;
            var w2 = vc * denom;
            return vertexA + ab * v2 + ac * w2; //= u*A + v*B + w*C, u = va * denom = 1.0f - v - w
        }

        /// <summary>
        /// Find the closest point on a triangle defined in a transform local space.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ClosestPointOnTriangle(this in Vector3 position, Transform transform, in Vector3 vertexA, in Vector3 vertexB, in Vector3 vertexC)
            => transform.TransformPoint(transform.InverseTransformPoint(position).ClosestPointOnTriangle(vertexA, vertexB, vertexC));

        /// <summary>
        /// Find the closest point on the surface of a sphere.
        /// </summary>
        public static Vector3 ClosestPointOnSphere(this in Vector3 position, in Vector3 sphereCenter, float sphereRadius)
        {
            var direction = Vector3.Normalize(position - sphereCenter);
            var localPosition = direction * sphereRadius;

            return sphereCenter + localPosition;
        }

        /// <summary>
        /// Find the closest point on the surface of a sphere defined in a transform local space.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ClosestPointOnSphere(this in Vector3 position, Transform transform, Vector3 localSphereCenter, float localSphereRadius)
            => transform.TransformPoint(transform.InverseTransformPoint(position).ClosestPointOnSphere(localSphereCenter, localSphereRadius));

        /// <summary>
        /// Find the closest point on the surface of a box
        /// </summary>
        public static Vector3 ClosestPointOnBox(this in Vector3 self, in Vector3 boxCenter, in Vector3 boxSize)
        {
            // The transform will fit the point into the right scale
            var halfSize = boxSize * 0.5f;

            var position = self;
            // Check if we're outside the box
            if (position.x < -halfSize.x
             || position.x > halfSize.x
             || position.y < -halfSize.y
             || position.y > halfSize.y
             || position.z < -halfSize.z
             || position.z > halfSize.z)
            {
                // We're going to force the local point into the box so we can clamp
                // it to the boundary of the box.
                position -= boxCenter;

                // Clamp to the box boundary
                position.x = Mathf.Clamp(position.x, -halfSize.x, halfSize.x);
                position.y = Mathf.Clamp(position.y, -halfSize.y, halfSize.y);
                position.z = Mathf.Clamp(position.z, -halfSize.z, halfSize.z);

                // Put the position back where it was
                position += boxCenter;
            }
            // If we're inside the box, move to the closest plane
            else
            {
                var local = new Vector3(
                    halfSize.x - Mathf.Abs(position.x),
                    halfSize.y - Mathf.Abs(position.y),
                    halfSize.z - Mathf.Abs(position.z));

                if (local.x < local.y && local.x < local.z)
                {
                    position.x = (position.x < 0f ? -halfSize.x : halfSize.x);
                }
                else if (local.y < local.x && local.y < local.z)
                {
                    position.y = (position.y < 0f ? -halfSize.y : halfSize.y);
                }
                else
                {
                    position.z = (position.z < 0f ? -halfSize.z : halfSize.z);
                }
            }

            return position;
        }

        /// <summary>
        /// Find the closest point on the surface of a box defined in a transform local space.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ClosestPointOnBox(this in Vector3 position, Transform transform, in Vector3 localBoxCenter, in Vector3 localBoxSize)
            => transform.TransformPoint(transform.InverseTransformPoint(position).ClosestPointOnBox(localBoxCenter, localBoxSize));

        /// <summary>
        /// Find the closest point on the surface of a capsule
        /// </summary>
        public static Vector3 ClosestPointOnCapsule(this in Vector3 position, in Vector3 capsuleStart, in Vector3 capsuleEnd, float capsuleRadius)
        {
            // If distance from start to end is less than twice the radius this is degenerate capsule so treat as a sphere.
            var displacement = capsuleEnd - capsuleStart;
            if (displacement.sqrMagnitude <= 4f * capsuleRadius * capsuleRadius)
                return position.ClosestPointOnSphere(displacement * 0.5f, capsuleRadius);

            var hemisphere = displacement.normalized * capsuleRadius;
            var start = capsuleStart + hemisphere;
            var end = capsuleEnd - hemisphere;
            displacement = end - start;

            var projectedPosition = Vector3.Project(position - start, displacement);
            Vector3 closestPoint;

            if (Vector3.Dot(projectedPosition, displacement) < 0f)
                closestPoint = capsuleStart;
            else if (projectedPosition.sqrMagnitude > displacement.sqrMagnitude)
                closestPoint = capsuleEnd;
            else
                closestPoint = projectedPosition + capsuleStart;

            return closestPoint + ((position - closestPoint).normalized * capsuleRadius);
        }

        /// <summary>
        /// Find the closest point on the surface of a capsule defined in a transform local space.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ClosestPointOnCapsule(this in Vector3 position, Transform transform, in Vector3 localCapsuleStart, in Vector3 localCapsuleEnd, float localCapsuleRadius)
            => transform.TransformPoint(transform.InverseTransformPoint(position).ClosestPointOnCapsule(localCapsuleStart, localCapsuleEnd, localCapsuleRadius));

        /// <summary>
        /// Find the closest point on the surface of a sphere collider.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ClosestPointOnCollider(this in Vector3 position, SphereCollider collider) => collider.ClosestPoint(position);

        /// <summary>
        /// Find the closest point on the surface of a box collider.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ClosestPointOnCollider(this in Vector3 position, BoxCollider collider) => collider.ClosestPoint(position);

        /// <summary>
        /// Find the closest point on the surface of a capsule collider.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ClosestPointOnCollider(this in Vector3 position, CapsuleCollider collider) => collider.ClosestPoint(position);

        /// <summary>
        /// Find the closest point on the surface of a convex mesh collider.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ClosestPointOnCollider(this in Vector3 position, MeshCollider collider) => collider.ClosestPoint(position);

        #endregion

        #region Geometry Test

        /// <summary>
        /// Return true if a point is inside a cone defined by a central line and the radius in each end. Also outputs a penetrationRate
        /// indicating how close the point is to the central line in respect to the cone radius (1 = on central line; 0 = on the surface or out of the cone).
        /// </summary>
        public static bool IsInsideCone(this in Vector3 position, in Segment centralSegment, float startRadius, float endRadius, out float penetrationRate)
        {
            Debug.Assert(startRadius >= 0f && endRadius >= 0f);
            // First we'll draw out a line from the cone start point down to the cone end. We'll find the closest point on that line to position.
            // If we're outside the max distance, or behind the Start Point, we bail out as that means we have no chance to be in the cone.

            var closestPointInCentralLine = position.ClosestPointOnSegment(centralSegment);
            
            penetrationRate = 0f; // start assuming we're outside cone until proven otherwise.

            var deltaToStart = centralSegment.Start - closestPointInCentralLine;
            var deltaToEnd = centralSegment.Start + centralSegment.End - closestPointInCentralLine;

            var sqrCentralSegmentLength = (centralSegment.End - centralSegment.Start).sqrMagnitude;
            var sqrDistanceToStart = deltaToStart.sqrMagnitude;
            var sqrDistanceToEnd = deltaToEnd.sqrMagnitude;

            if (sqrDistanceToStart > sqrCentralSegmentLength || sqrDistanceToEnd > sqrCentralSegmentLength)
                return false; // Outside of the cone

            var percentAlongCone = Mathf.Sqrt(sqrDistanceToStart) / Mathf.Sqrt(sqrCentralSegmentLength); // don't have to catch outside 0->1 due to above code (saves 2 sqrts if outside)
            var radiusAtPoint = startRadius + ((endRadius - startRadius) * percentAlongCone);

            var distanceToCentralLine = Vector3.Distance(position, closestPointInCentralLine);
            if (distanceToCentralLine > radiusAtPoint) // target is farther from the line than the radius at that distance)
                return false;

            penetrationRate = radiusAtPoint > 0.0f ? (radiusAtPoint - distanceToCentralLine) / radiusAtPoint : 1f;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInsideSphere(this in Vector3 position, in Vector3 sphereCenter, float sphereRadius)
            => Mathf.Approximately(0f, sphereRadius) // If the sphere radius is equivalent to 0 the center is the ONLY point 
                ? sphereCenter == position 
                : (position - sphereCenter).sqrMagnitude <= sphereRadius * sphereRadius; 

        #endregion

        #region Raycast

        private const int InitialRaycastHitBufferSize = 20;

        private static RaycastHit[] raycastHitBuffer = new RaycastHit[InitialRaycastHitBufferSize];

        /// <summary>
        /// A non-alloc version of <see cref="Physics.Raycast(Vector3, Vector3, float, int, QueryTriggerInteraction)"/> that can ignore specific transforms and triggers.
        /// Note that this is not as efficient as using layers but there is no penalty for using a null or empty list
        /// since it reverts to unity's standard implementation. Internally uses a non-alloc raycast that computes multiple hits in a shared buffer. 
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Raycast(this in Vector3 origin, in Vector3 direction, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction, IList<Transform> ignoredTransforms, int bufferSize = 40)
            => Raycast(origin, direction, out RaycastHit hitInfo, maxDistance, layerMask, queryTriggerInteraction, ignoredTransforms, bufferSize);

        /// <summary>
        /// A non-alloc version of <see cref="Physics.Raycast(Vector3, Vector3, out RaycastHit, float, int, QueryTriggerInteraction)"/> that can ignore specific transforms and triggers.
        /// Note that this is not as efficient as using layers only but there is no penalty for using a null or empty list
        /// since it reverts to unity's standard implementation. Internally uses a non-alloc raycast that computes multiple hits in a shared buffer. 
        /// </summary>
        public static bool Raycast(this in Vector3 origin, in Vector3 direction, out RaycastHit hitInfo, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction, IList<Transform> ignoredTransforms, int bufferSize = 40)
        {
            // Early out if there is no need to filter out ignored transforms
            if (ignoredTransforms == null || ignoredTransforms.Count == 0)
                return Physics.Raycast(origin, direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);

            if (raycastHitBuffer.Length < bufferSize)
                Array.Resize(ref raycastHitBuffer, bufferSize);

            var hitCount = Physics.RaycastNonAlloc(origin, direction, raycastHitBuffer, maxDistance, layerMask, queryTriggerInteraction);
            var ignoredCount = ignoredTransforms.Count;
            for (int i = 0; i < hitCount; ++i)
            {
                hitInfo = raycastHitBuffer[i];
                var ignored = false;
                for (int j = 0; !ignored && j < ignoredCount; ++j)
                    ignored = hitInfo.transform.IsChildOf(ignoredTransforms[j]);

                if (!ignored)
                    return true;
            }

            hitInfo = default;
            return false;
        }

        /// <summary>
        /// A version of <see cref="Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction)"/> that can ignore specific transforms and triggers.
        /// Note that this is not as efficient as using layers only but there is no penalty for using a null or empty list
        /// since it reverts to unity's standard implementation.
        /// </summary>
        public static int RaycastNonAlloc(this in Vector3 origin, in Vector3 direction, RaycastHit[] results, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction, List<Transform> ignoredTransforms)
        {
            var hitCount = Physics.RaycastNonAlloc(origin, direction, results, maxDistance, layerMask, queryTriggerInteraction);

            // Early out if there is need to filter out ignored transforms
            if (ignoredTransforms == null || ignoredTransforms.Count == 0)
                return hitCount;

            var ignoredCount = ignoredTransforms.Count;
            for (int i = 0; i < hitCount; ++i)
            {
                RaycastHit hitInfo = results[i];
                for (int j = 0; j < ignoredCount; ++j)
                {
                    if (hitInfo.transform.IsChildOf(ignoredTransforms[j]))
                    {
                        results[i] = default;
                        break;
                    }
                }
            }

            // Second pass to shift all non-ignored hits to the beginning of the results array
            var resultCount = 0;
            var index = 0;
            while (index < hitCount)
            {
                while (index < hitCount && results[index].transform == null)
                    index++;

                if (resultCount < index)
                    results[resultCount] = results[index];

                index++;
                resultCount++;
            }

            return resultCount;
        }

        /// <summary>
        /// A non-alloc version of <see cref="Physics.SphereCast(Ray, float, float, int, QueryTriggerInteraction)"/> that can ignore specific transforms and triggers.
        /// Note that this is not as efficient as using layers only but there is no penalty for using a null or empty list
        /// since it reverts to unity's standard implementation. Internally uses a non-alloc raycast that computes multiple hits in a shared buffer. 
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SphereCast(this in Vector3 origin, float radius, Vector3 direction, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction, List<Transform> ignoredTransforms, int bufferSize = 40)
            => SphereCast(origin, radius, direction, out RaycastHit hitInfo, maxDistance, layerMask, queryTriggerInteraction, ignoredTransforms, bufferSize);

        /// <summary>
        /// A non-alloc version of <see cref="Physics.SphereCast(Ray, float, out RaycastHit, float, int, QueryTriggerInteraction)"/> that can ignore specific transforms and triggers.
        /// Note that this is not as efficient as using layers only but there is no penalty for using a null or empty list
        /// since it reverts to unity's standard implementation. Internally uses a non-alloc raycast that computes multiple hits in a shared buffer. 
        /// </summary>
        public static bool SphereCast(this in Vector3 origin, float radius, Vector3 direction, out RaycastHit hitInfo, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction, List<Transform> ignoredTransforms, int bufferSize = 40)
        {
            // Early out if there is need to filter out ignored transforms
            if (ignoredTransforms == null || ignoredTransforms.Count == 0)
                return Physics.SphereCast(origin, radius, direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);

            if (raycastHitBuffer.Length < bufferSize)
                Array.Resize(ref raycastHitBuffer, bufferSize);

            var hitCount = Physics.SphereCastNonAlloc(origin, radius, direction, raycastHitBuffer, maxDistance, layerMask, queryTriggerInteraction);
            var ignoredCount = ignoredTransforms.Count;
            for (int i = 0; i < hitCount; ++i)
            {
                hitInfo = raycastHitBuffer[i];
                var ignored = false;
                for (int j = 0; !ignored && j < ignoredCount; ++j)
                    ignored = hitInfo.transform.IsChildOf(ignoredTransforms[j]);

                if (!ignored)
                    return true;
            }

            hitInfo = new RaycastHit();
            return false;
        }

        /// <summary>
        /// A version of <see cref="Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction)"/> that can ignore specific transforms and triggers.
        /// Note that this is not as efficient as using layers only but there is no penalty for using a null or empty list
        /// since it reverts to unity's standard implementation.
        /// </summary>
        public static int SphereCastNonAlloc(this in Vector3 origin, float radius, Vector3 direction, RaycastHit[] results, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction, List<Transform> ignoredTransforms)
        {
            var hitCount = Physics.SphereCastNonAlloc(origin, radius, direction, results, maxDistance, layerMask, queryTriggerInteraction);

            // Early out if there is need to filter out ignored transforms
            if (ignoredTransforms == null || ignoredTransforms.Count == 0)
                return hitCount;

            var ignoredCount = ignoredTransforms.Count;
            for (int i = 0; i < hitCount; ++i)
            {
                RaycastHit hitInfo = results[i];
                for (int j = 0; j < ignoredCount; ++j)
                {
                    if (hitInfo.transform.IsChildOf(ignoredTransforms[j]))
                    {
                        results[i] = default;
                        break;
                    }
                }
            }

            // Second pass to shift all non-ignored hits to the beginning of the results array
            var resultCount = 0;
            var index = 0;
            while (index < hitCount)
            {
                while (index < hitCount && results[index].transform == null)
                    index++;

                if (resultCount < index)
                    results[resultCount] = results[index];

                index++;
                resultCount++;
            }

            return resultCount;
        }

        /// <summary>
        /// A version of <see cref="Physics.OverlapSphereNonAlloc(Vector3, float, Collider[], int, QueryTriggerInteraction)"/> that can ignore specific transforms and triggers.
        /// Note that this is not as efficient as using layers only but there is no penalty for using a null or empty list
        /// since it reverts to unity's standard implementation.
        /// </summary>
        public static int OverlapSphereNonAlloc(this in Vector3 origin, float radius, Collider[] results, int layerMask, QueryTriggerInteraction queryTriggerInteraction, List<Transform> ignoredTransforms)
        {
            var hitCount = Physics.OverlapSphereNonAlloc(origin, radius, results, layerMask, queryTriggerInteraction);

            // Early out if there is need to filter out ignored transforms
            if (ignoredTransforms == null || ignoredTransforms.Count == 0)
                return hitCount;

            var ignoredCount = ignoredTransforms.Count;
            for (int i = 0; i < hitCount; ++i)
            {
                Collider collider = results[i];
                for (int j = 0; j < ignoredCount; ++j)
                {
                    if (collider.transform.IsChildOf(ignoredTransforms[j]))
                    {
                        results[i] = null;
                        break;
                    }
                }
            }

            // Second pass to shift all non-ignored hits to the beginning of the results array
            var resultCount = 0;
            var index = 0;
            while (index < hitCount)
            {
                while (index < hitCount && results[index] == null)
                    index++;

                if (resultCount < index)
                    results[resultCount] = results[index];

                index++;
                resultCount++;
            }

            return resultCount;
        }

        /// <summary>
        /// Cast several rays in an arc from point <paramref name="origin"/> centered in direction <paramref name="direction"/>, with length <paramref name="maxDistance"/>, against all colliders in the scene.
        /// Note that this is not as efficient as using <see cref="Physics.OverlapSphere(Vector3, float, int, QueryTriggerInteraction)"/>.
        /// Aperture is defined by <paramref name="angleWidth"/>. Number of actual ray casts is <paramref name="segments"/> + 1.   
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ArcCast(this in Vector3 origin, in Vector3 direction, in Vector3 upwards, float maxDistance = 1000f, float angleWidth = 60f, int segments = 6, int layerMask = Physics.DefaultRaycastLayers, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
            => ArcCast(origin, direction, upwards, out RaycastHit hitInfo, maxDistance, angleWidth, segments, layerMask, queryTriggerInteraction);

        /// <summary>
        /// Cast several rays in an arc from point <paramref name="origin"/> centered in the given <paramref name="direction"/>, with length equal to <paramref name="maxDistance"/>, against all colliders in the scene
        /// and return detailed information on what was hit. 
        /// Note that this is not as efficient as using <see cref="Physics.OverlapSphereNonAlloc(Vector3, float, Collider[], int, QueryTriggerInteraction)"/>.
        /// The arc will have a central angle of <paramref name="angularWidth"/> in degrees divided in one or more <paramref name="segments"/>.
        /// The actual number of ray casts will be equal to <paramref name="segments"/>.   
        /// </summary>
        public static bool ArcCast(this in Vector3 origin, in Vector3 direction, in Vector3 upwards, out RaycastHit hitInfo, float maxDistance = 1000f, float angularWidth = 60f, int segments = 6, int layerMask = Physics.DefaultRaycastLayers, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
        {
            angularWidth = Mathf.Clamp(angularWidth, 0f, 360f);
            segments = Mathf.Max(2, segments);

            var step = angularWidth / segments;
            var angleExtent = angularWidth * 0.5f;

            var currentRotation = Quaternion.AngleAxis(-angleExtent, upwards);
            var targetRotation = Quaternion.AngleAxis(angleExtent, upwards);

            for (; segments > 0; --segments)
            {
                if (Physics.Raycast(origin, currentRotation * direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction))
                    return true;

                currentRotation = Quaternion.RotateTowards(currentRotation, targetRotation, step);
            }

            hitInfo = default;
            return false;
        }

        #endregion

    }
}
