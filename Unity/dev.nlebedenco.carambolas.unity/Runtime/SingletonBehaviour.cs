using System;
using System.Diagnostics;
using System.Reflection;

using UnityEngine;
using UnityObject = UnityEngine.Object;

using Resources = Carambolas.Internal.Resources;
using Strings = Carambolas.Internal.Strings;
using System.Collections.Generic;

namespace Carambolas.UnityEngine
{
    public abstract class SingletonBehaviour: MonoBehaviour
    {
        private protected SingletonBehaviour() { }
    }

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
        /// <summary>
        /// Resturn true if this singleton is short lived and may be destroyed any time or 
        /// false if it must live until the application is terminated. Default is false.
        /// </summary>
        protected virtual bool Transient => false;

        public static T Instance { get; private set; }

        public static GameObject SingletonObject => Instance?.gameObject;

        public event Action<T> Destroyed;

        private void OnDestroyed()
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
                    Debug.LogError(string.Format("Unexpected destruction of non-transient singleton {0}{1}. Application will be terminated.", 
                        GetType().FullName, string.IsNullOrEmpty(name) ? string.Empty : $" ({name})"), this);

                    Application.Quit();
                }
            }
        }

#if UNITY_EDITOR
        protected virtual void Reset() { }

        protected virtual void OnValidate() { }
#endif

        protected void Awake()
        {
            try
            {
                if (Instance is null)
                {                 
                    ValidateRequiredComponents();

#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        throw new InvalidOperationException(string.Format(Resources.GetString(Strings.UnityEngine.SingletonBehaviour.NotInPlayMode), 
                            typeof(T).FullName, GetType().FullName, string.IsNullOrEmpty(name) ? string.Empty : $" ({name})"));
#endif
                    OnSingletonAwaking();
                }
                else
                {
                    throw new InvalidOperationException(string.Format(Resources.GetString(Strings.UnityEngine.SingletonBehaviour.CannotHaveMultipleInstances), 
                        typeof(T).FullName, GetType().FullName, string.IsNullOrEmpty(name) ? string.Empty : $" ({name})"));
                }                
            }
            catch (Exception e)
            {
                if (!string.IsNullOrEmpty(e.Message))
                    Debug.LogException(e, this);
                SelfDestruct();
            }

            if (this) // not destroyed
            {
                Instance = this as T;
                DontDestroyOnLoad(gameObject);
                transform.hideFlags = HideFlags.NotEditable | HideFlags.HideInInspector;

                Debug.Log(string.Format("{0}{1} singleton{2} instantiated.",
                     GetType().FullName,
                     string.IsNullOrEmpty(name) ? string.Empty : $" ({name})",
                     GetType() == typeof(T) ? "" : $" derived from {typeof(T).FullName}"));

                OnSingletonAwake();
            }
        }

        private void SelfDestruct()
        {
            if (!Application.isPlaying)
                DestroyImmediate(this);
            else
                Destroy(this);
        }

        private static readonly List<Component> componentsOnDestroy = new List<Component>();

        protected void OnDestroy()
        {
            if (ReferenceEquals(Instance, this))
            {
                try
                {
                    OnSingletonDestroy();
                }
                finally
                {
                    Instance = null;
                    Debug.Log(string.Format("{0}{1} singleton{2} destroyed.",
                        GetType().FullName,
                        string.IsNullOrEmpty(name) ? string.Empty : $" ({name})",
                        GetType() == typeof(T) ? "" : $" derived from {typeof(T).FullName}"));
                }
            }

            // Destroy game object if this was the last component (other than Transform)
            if (!gameObject.IsNullOrDestroyed())
            {
                gameObject.GetComponents(componentsOnDestroy);
                if (componentsOnDestroy.Count <= 2)
                    Destroy(gameObject);
            }

            OnDestroyed();
        }

        protected virtual void OnSingletonAwaking() { }

        protected virtual void OnSingletonAwake() { }

        protected virtual void OnSingletonDestroy() { }

        protected virtual void ValidateRequiredComponents()
        {
            var type = GetType();
            var attributes = type.GetCustomAttributes<RequireComponent>();
            foreach (var attr in attributes)
            {
                ThrowIfRequiredComponentIsMissing(type, attr.m_Type0);
                ThrowIfRequiredComponentIsMissing(type, attr.m_Type1);
                ThrowIfRequiredComponentIsMissing(type, attr.m_Type2);
            }
        }

        private void ThrowIfRequiredComponentIsMissing(Type self, Type required)
        {
            if (required != null && !GetComponent(required))
                throw new InvalidOperationException(string.Format(Resources.GetString(Strings.UnityEngine.SingletonBehaviour.MissingRequiredComponent),
                    typeof(T).FullName, required.FullName, self.FullName, string.IsNullOrEmpty(name) ? string.Empty : $" ({name})"));
        }
    }
}

