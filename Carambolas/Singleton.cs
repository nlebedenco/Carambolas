using System;

namespace Carambolas
{
    public abstract class Singleton { }

    public abstract class Singleton<T>: Singleton, IDisposable
        where T: Singleton<T>
    {
        public static T Instance { get; private set; }

        protected Singleton()
        {
            if (Instance != null)
                throw new InvalidOperationException(GetType() == Instance.GetType()
                    ? string.Format(Resources.GetString(Strings.Singleton.InstanceAlreadyExists), GetType().FullName)
                    : string.Format(Resources.GetString(Strings.Singleton.InstanceOfSubtypeAlreadyExists), GetType().FullName, Instance.GetType()));

            Instance = this as T;
        }

        #region IDisposable

        private bool disposed = false;

        /// <summary>
        /// Implement to dispose managed resources or disposable objects.
        /// This class is not meant to handle unmanaged resources directly.
        /// </summary>
        protected virtual void OnDispose() { }

        /// <summary>
        /// Release this object and any managed objects referenced by it.
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                OnDispose();

                Instance = null;
                disposed = true;
            }
        }

        #endregion
    }
}
