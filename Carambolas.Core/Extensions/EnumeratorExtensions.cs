using System;
using System.Collections;
using System.Collections.Generic;

namespace Carambolas
{
    public static class EnumeratorExtenions
    {
        /// <summary>
        /// Run an iterator function that might throw an exception. Call the callback with the exception if it throws.
        /// </summary>
        /// <returns>An enumerator that runs the given enumerator inside a try..catch block</returns>
        public static IEnumerator Catch(this IEnumerator enumerator, Action<Exception> onException, Action onComplete = null)
        {
            while (true)
            {
                object current;
                try
                {
                    if (!enumerator.MoveNext())
                        break;

                    current = enumerator.Current;
                }
                catch (Exception e)
                {
                    onException(e);
                    yield break;
                }

                yield return current;
            }

            onComplete?.Invoke();
        }

        /// <summary>
        /// Run an iterator function that might throw an exception. Call the callback with the exception if it throws.
        /// </summary>
        /// <returns>An enumerator that runs the given enumerator inside a try..catch block</returns>
        public static IEnumerator<T> Catch<T>(this IEnumerator<T> enumerator, Action<Exception> onException, Action onComplete = null)
        {
            while (true)
            {
                T current;
                try
                {
                    if (!enumerator.MoveNext())
                        break;

                    current = enumerator.Current;
                }
                catch (Exception e)
                {
                    onException(e);
                    yield break;
                }

                yield return current;
            }

            onComplete?.Invoke();
        }
    }
}
