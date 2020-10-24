using System;
using System.Diagnostics;
using System.Reflection;

using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Carambolas.UnityEngine
{
    public sealed class SilentAbortException: Exception { }

    public abstract class SingletonBehaviour: MonoBehaviour { }

    /// <summary>
    /// A strict singleton behaviour that destroys itself in case an instance already exists in order to guarantee uniqueness.
    /// </summary>
    /// 
    /// <remarks>
    /// Note that <see cref="SingletonBehaviour{T}.Instance"/> does not create a new instance by default on purpose.
    /// Creation must be under total control of the user.
    /// A simple idiom for retrieveing the current instance or creating one is:
    /// 
    /// <code><![CDATA[
    /// MySingletonComponent.Instance.OrNull() ?? Component<MySingletonComponent>.Create();
    /// ]]></code>
    /// 
    /// Also note that this is not provided in a convenient generic method because C# does not allow us to invoke static methods
    /// from generic type parameters. A naive solution like the following DOES NOT not work for types that are not immediately
    /// derived from <see cref="SingletonBehaviour{T}"/>.
    /// 
    /// <code><![CDATA[
    /// public static T Create() where T: SingletonBehaviour<T> => SingletonBehaviour<T>.Instance.OrNull() ?? Component<T>.Create(); "/>
    /// ]]></code>
    ///
    /// As a rule of thumb, it's better to avoid having singletons saved to a scene. In most cases there won't be a problem
    /// but some puzzling error messages may appear sometimes. For example, if we try to reload a scene that has saved singletons
    /// in it we'll soon realize that the current singletons were moved to DontDestroyOnLoad and won't be destroyed by the scene unloading.
    /// As a consequence, the reloaded scene will try to instantiate those singletons again and will display a lot of errors in the
    /// process about instances already existing. It can be particular challenging to understand situations like this
    /// specially when Unity itself does display a very consistent behaviour. For example, as of Unity 2019.1.11f if we call
    /// <see cref="global::UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(string)"/> passing a null or empty string Unity 
    /// will NOT raise an error. It will instead silently perform a scene reload.
    /// </remarks>
    public abstract class SingletonBehaviour<T> : SingletonBehaviour where T: SingletonBehaviour<T>
    {
        private bool created;

        /// <summary>
        /// Resturn true if this singleton is short lived and may be destroyed any time or 
        /// false if it must live until the application is terminated.
        /// </summary>
        protected abstract bool Transient { get; }

        public static T Instance { get; protected set; }

        public event Action<T> Destroyed;

        protected void OnDestroyed()
        {
            var handler = Destroyed;
            Destroyed = null;

            try
            {
                handler?.Invoke(this as T);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
            finally
            {
                if (!Transient && !Application.terminated)
                {
                    Debug.LogErrorFormat(this, "Unexpected destruction of non-transient singleton {0}{1}. Application will be terminated.", GetType().FullName, string.IsNullOrEmpty(name) ? string.Empty : $" ({name})");
                    Application.Quit();
                }
            }
        }

        protected virtual void Reset() { }

        protected virtual void OnValidate() { }

        protected void Awake()
        {
            try
            {
                OnSingletonAwaking();
                ValidateRequiredComponents();
                Instance = this as T;
                DontDestroyOnLoad(gameObject);
                transform.hideFlags = HideFlags.NotEditable | HideFlags.HideInInspector;
                Debug.LogFormat(this, "{0}{1} created.", GetType().FullName, string.IsNullOrEmpty(name) ? string.Empty : $" ({name})");
                OnSingletonAwake();
                created = true;
            }
            catch (Exception e)
            {                
                if (!Application.isPlaying)
                    DestroyImmediate(this);
                else
                    Destroy(this);

                switch (e)
                {
                    case null:
                    case SilentAbortException silent:
                        break;
                    default:
                        Debug.LogException(e, this);
                        break;
                }
            }
        }

        protected void OnDestroy()
        {
            if (created)
            {
                Debug.LogFormat(this, "{0}{1} destroyed.", GetType().FullName, string.IsNullOrEmpty(name) ? string.Empty : $" ({name})");

                if (ReferenceEquals(Instance, this))
                    Instance = null;

                try
                {
                    OnSingletonDestroy();
                }
                catch(Exception e)
                {
                    Debug.LogException(e, this);
                }

                OnDestroyed();
            }

            if (gameObject.GetComponents<Component>().Length <= 2) // 2 => this component + transform component
                Destroy(gameObject);
        }

        /// <summary>
        /// Executed before <see cref="OnSingletonAwake"/> this method can be used to
        /// prepare or validate the singleton creation.
        /// </summary>
        /// <remarks>The whole process is aborted
        /// if this method throws an exception. Throw a <see cref="SilentAbortException"/>
        /// if you want to abort but does not consider this an error.
        /// </remarks>
        protected virtual void OnSingletonAwaking()
        {
            ThrowIfNotPlaying();

            if (Instance != null && Instance != this)
                throw new InvalidOperationException(string.Format(Resources.GetString(Strings.UnityEngine.SingletonBehaviour.CannotHaveMultipleInstances), typeof(T).FullName, GetType().FullName, string.IsNullOrEmpty(name) ? string.Empty : $" ({name})"));
        }

        protected virtual void OnSingletonAwake() { }

        protected virtual void OnSingletonDestroy() { }

        [Conditional("UNITY_EDITOR")]
        private void DstroyImmediateIfNotPlaying()
        {

        }

        [Conditional("UNITY_EDITOR")]
        private void ThrowIfNotPlaying()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException(string.Format(Resources.GetString(Strings.UnityEngine.SingletonBehaviour.NotInPlayMode), typeof(T).FullName, GetType().FullName, string.IsNullOrEmpty(name) ? string.Empty : $" ({name})"));
        }

        private void ThrowIfComponentIsMissing(Type required)
        {
            if (required != null && !GetComponent(required))
                throw new InvalidOperationException(string.Format(Resources.GetString(Strings.UnityEngine.SingletonBehaviour.MissingRequiredComponent), typeof(T).FullName, required.FullName, GetType().FullName, string.IsNullOrEmpty(name) ? string.Empty : $" ({name})"));
        }

        protected virtual void ValidateRequiredComponents()
        {
            var attributes = GetType().GetCustomAttributes<RequireComponent>();
            foreach (var attr in attributes)
            {
                ThrowIfComponentIsMissing(attr.m_Type0);
                ThrowIfComponentIsMissing(attr.m_Type1);
                ThrowIfComponentIsMissing(attr.m_Type2);
            }
        }
    }
}

