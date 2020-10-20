using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Carambolas.UnityEngine
{
    using Interval = Range<float>;

    [Serializable]
    public struct Blast
    {
        /// <summary>
        /// Intensity of the blast at ground zero.
        /// </summary>
        public float Magnitude;

        /// <summary>
        /// Position of the blast in world space.
        /// </summary>
        public Vector3 Origin;

        /// <summary>
        /// Defines two circular areas: one limited by an inner radius corresponding to <see cref="Interval.Min"/> 
        /// and another limited by an outer radius corresponding to <see cref="Interval.Max"/>. 
        /// Blast intensity is always maximal inside the inner radius. Between the inner radius and the outer radius
        /// it suffers a fall off effect calculated according to <see cref="Falloff"/>
        /// </summary>
        public Interval Zone;

        /// <summary>
        /// Defines how intensity decays beyond the inner radius of the blast.
        /// </summary>
        public Scalar.Interpolation.Settings Falloff;

        public Blast(float magnitude, in Vector3 origin)
        {
            this.Magnitude = magnitude;
            this.Origin = origin;
            this.Zone = new Interval(0f, 1f);
            this.Falloff = default;
        }

        public Blast(float magnitude, in Vector3 origin, in Interval radius)
        {
            this.Magnitude = magnitude;
            this.Origin = origin;
            this.Zone = radius;
            this.Falloff = default;
        }

        public Blast(float magnitude, in Vector3 origin, in Interval radius, in Scalar.Interpolation.Settings falloff)
        {
            this.Magnitude = magnitude;
            this.Origin = origin;
            this.Zone = radius;
            this.Falloff = falloff;
        }

        private float CalculateScale(float distance)
        {
            distance = Math.Max(0f, distance);

            var innerRadius = Math.Max(0f, Zone.Min);
            var outerRadius = Math.Max(innerRadius, Zone.Max);

            if (distance < innerRadius)
                return 1f;
            else if (distance < outerRadius)
                return 1f - ((distance - innerRadius) / (outerRadius - innerRadius));
            else
                return 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float IntensityAt(float x, float y, float z) => IntensityAt(new Vector3(x, y, z));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float IntensityAt(in Vector3 position) => IntensityAt(Vector3.Distance(Origin, position));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float IntensityAt(float radius) => Magnitude * Scalar.Interpolate(0f, 1f, CalculateScale(radius), Falloff);
    }
}
