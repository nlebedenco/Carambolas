using System;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Carambolas.UnityEngine
{
    public static class ComponentExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsRuntimePrefab(this Component self) => self.gameObject.IsRuntimePrefab();
    }
}
