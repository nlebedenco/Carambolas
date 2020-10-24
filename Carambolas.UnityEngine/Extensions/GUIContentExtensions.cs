using System;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Carambolas.UnityEngine
{
    public static class GUIContentExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEmpty(this GUIContent self) => string.IsNullOrEmpty(self.text) && self.image == null;
    }
}
