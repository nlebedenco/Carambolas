using System;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Carambolas.UnityEngine.Extensions
{
    public static class RangeExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 ToVector2(this in Range<float> range) => new Vector2(range.Min, range.Max);
    }
}
