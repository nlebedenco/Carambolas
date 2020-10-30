using System;

using UnityEngine;

namespace Carambolas.UnityEngine
{
    internal static class Runtime
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void AfterAssembliesLoaded()
        {
#if UNITY_SERVER
            Application.isServerBuild = true;
#else
            Application.isServerBuild = false;
#endif
        }
    }
}