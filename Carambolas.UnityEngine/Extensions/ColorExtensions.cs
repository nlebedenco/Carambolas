using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Carambolas.UnityEngine
{
    public static class ColorExtensions
    {       
        public static Color Desaturate(this in Color color, float alpha)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            s *= Mathf.Clamp01(alpha);
            return Color.HSVToRGB(h, s, v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToHtmlStringRGB(this in Color self) => ColorUtility.ToHtmlStringRGB(self);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToHtmlStringRGBA(this in Color self) => ColorUtility.ToHtmlStringRGBA(self);
    }
}
