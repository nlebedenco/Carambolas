using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Carambolas.UnityEngine
{
    public static class MonoBehaviourExtensions
    {
        /// <summary>
        /// Starts a coroutine that might throw an exception.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Coroutine StartCoroutine(this MonoBehaviour self, IEnumerator enumerator, Action<Exception> onCompleted) => self.StartCoroutine(enumerator.Catch(onCompleted));

        /// <summary>
        /// Starts a coroutine that might throw an exception.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Coroutine StartCoroutine<T>(this MonoBehaviour self, IEnumerator<T> enumerator, Action<Exception> onCompleted) => self.StartCoroutine(enumerator.Catch(onCompleted));
    }
}
