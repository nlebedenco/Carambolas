using System;
using System.Runtime.CompilerServices;

using Resources = Carambolas.Internal.Resources;
using Strings = Carambolas.Internal.Strings;

namespace Carambolas.UnityEngine
{
    public static partial class Scalar
    {
        public static class Interpolation
        {
            public const float PI = 3.141593f;

            // See: http://easings.net
            public enum Function
            {
                Linear = 0,
                Quad,
                Cubic,
                Quart,
                Quint,
                Sine,
                Expo,
                Circ,
                Elastic,
                Back,
                Bounce
            }

            public enum Mode
            {
                In = 0,
                Out,
                InOut
            }

            [Serializable]
            public struct Settings
            {
                public Function Function;
                public Mode Mode;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static float Calculate(float from, float to, float alpha, Function function, Mode mode) => Calculate(from, to, alpha, Select(function), mode);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float Calculate(float from, float to, float alpha, Func<float, float, float> function, Mode mode)
            {
                switch (mode)
                {
                    case Mode.In:
                        return In(function, alpha, from, to - from);
                    case Mode.Out:
                        return Out(function, alpha, from, to - from);
                    case Mode.InOut:
                        return InOut(function, alpha, from, to - from);
                    default:
                        return 0.0f;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float In(Func<float, float, float> easeFunc, float alpha, float b, float c, float d = 1.0f)
            {
                if (alpha >= d)
                    return b + c;

                if (alpha <= 0.0f)
                    return b;

                return c * easeFunc(alpha, d) + b;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float Out(Func<float, float, float> easeFunc, float alpha, float b, float c, float d = 1.0f)
            {
                if (alpha >= d)
                    return b + c;

                if (alpha <= 0.0f)
                    return b;

                return (b + c) - c * easeFunc(d - alpha, d);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float InOut(Func<float, float, float> easeFunc, float alpha, float b, float c, float d = 1.0f)
            {
                if (alpha >= d)
                    return b + c;

                if (alpha <= 0.0f)
                    return b;

                if (alpha < d / 2.0f)
                    return In(easeFunc, alpha * 2.0f, b, c / 2.0f, d);

                return Out(easeFunc, (alpha * 2.0f) - d, b + c / 2.0f, c / 2.0f, d);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Func<float, float, float> Select(Function function)
            {
                switch (function)
                {
                    case Function.Linear:
                        return Linear;
                    case Function.Quad:
                        return Quad;
                    case Function.Cubic:
                        return Cubic;
                    case Function.Quart:
                        return Quart;
                    case Function.Quint:
                        return Quint;
                    case Function.Sine:
                        return Sine;
                    case Function.Expo:
                        return Expo;
                    case Function.Circ:
                        return Circ;
                    case Function.Elastic:
                        return Elastic;
                    case Function.Back:
                        return Back;
                    case Function.Bounce:
                        return Bounce;
                    default:
                        throw new ArgumentException(string.Format(Resources.GetString(Strings.InvalidValue), function), nameof(function));
                }
            }

            private static float Linear(float alpha, float d = 1.0f) => alpha / d;

            private static float Quad(float alpha, float d = 1.0f) => (alpha /= d) * alpha;

            private static float Cubic(float alpha, float d = 1.0f) => (alpha /= d) * alpha * alpha;

            private static float Quart(float alpha, float d = 1.0f) => (alpha /= d) * alpha * alpha * alpha;

            private static float Quint(float alpha, float d = 1.0f) => (alpha /= d) * alpha * alpha * alpha * alpha;

            private static float Sine(float alpha, float d = 1.0f) => 1.0f - (float)Math.Cos(alpha / d * (PI / 2.0f));

            private static float Expo(float alpha, float d = 1.0f) => (float)Math.Pow(2.0f, 10.0f * (alpha / d - 1.0f));

            private static float Circ(float alpha, float d = 1.0f) => -(float)(Math.Sqrt(1.0f - (alpha /= d) * alpha) - 1.0f);

            private static float Elastic(float alpha, float d = 1.0f)
            {
                alpha /= d;
                float p = d * 0.3f;
                float s = p / 4.0f;

                return -(float)(Math.Pow(2.0f, 10.0f * (alpha -= 1.0f)) * Math.Sin((alpha * d - s) * (2.0f * PI) / p));
            }

            private static float Back(float alpha, float d = 1.0f) => (alpha /= d) * alpha * ((1.70158f + 1) * alpha - 1.70158f);

            private static float Bounce(float alpha, float d = 1.0f)
            {
                alpha = d - alpha;
                if ((alpha /= d) < (1 / 2.75f))
                    return 1.0f - (7.5625f * alpha * alpha);
                else if (alpha < (2.0f / 2.75f))
                    return 1.0f - (7.5625f * (alpha -= (1.5f / 2.75f)) * alpha + 0.75f);
                else if (alpha < (2.5f / 2.75f))
                    return 1.0f - (7.5625f * (alpha -= (2.25f / 2.75f)) * alpha + 0.9375f);

                return 1.0f - (7.5625f * (alpha -= (2.625f / 2.75f)) * alpha + 0.984375f);
            }
        }

        /// <summary>
        /// Interpolate between <paramref name="from"/> and <paramref name="to"/> by <paramref name="alpha"/>
        /// according to <paramref name="settings"/>. 
        /// See http://easings.net for more information about the functions employed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Interpolate(float from, float to, float alpha, in Interpolation.Settings settings) => Interpolate(from, to, alpha, settings.Function, settings.Mode);

        /// <summary>
        /// Interpolate between <paramref name="from"/> and <paramref name="to"/> by <paramref name="alpha"/>
        /// according to <paramref name="function"/> and <paramref name="mode"/>. 
        /// See http://easings.net for more information about the functions employed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Interpolate(float from, float to, float alpha, Interpolation.Function function, Interpolation.Mode mode) => Interpolation.Calculate(from, to, alpha, function, mode);

    }
}
