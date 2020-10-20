using System;
using System.Runtime.CompilerServices;

namespace Carambolas
{
    public static class DelegateExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetMethodName(this Delegate func) => func.Method.Name;
    }
}
