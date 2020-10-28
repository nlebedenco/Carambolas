using System;

using UnityEngine;
using UnityEngine.SceneManagement;

using UnityTime = UnityEngine.Time;

namespace Carambolas.UnityEngine
{
    public class Time: UnityTime
    {
        protected Time() { }

        [DefaultExecutionOrder(TimeManager.DefaultExecutionOrder)]
        [DisallowMultipleComponent]
        private sealed class TimeManager: SingletonBehaviour<TimeManager>
        {
            public const int DefaultExecutionOrder = int.MinValue;

            protected override void OnSingletonAwake()
            {
                base.OnSingletonAwake();
                gameObject.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            }

            private void FixedUpdate() => Time.FixedUpdate();

            private void Update() => Time.Update();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnRuntimeInitialize()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;

            accumulatedTime = -Time.deltaTime;
            fixedTime = -Time.fixedDeltaTime;

            accumulatedTimeSinceLevelLoad = accumulatedTime;
            fixedTimeSinceLevelLoad = fixedTime;

            unscaledTime = UnityTime.unscaledTime;
            fixedUnscaledTime = UnityTime.fixedUnscaledTime;

            startupTime = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(UnityTime.realtimeSinceStartup));

            if (!TimeManager.Instance)
                ComponentUtility.Create<TimeManager>();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            accumulatedTimeSinceLevelLoad = 0;
            fixedTimeSinceLevelLoad = 0;
        }

        #pragma warning disable IDE1006

        /// <summary>
        ///  The time this frame has started. This is the time in seconds since the start of the game.
        /// </summary>
        public static new double time { get; private set; }

        /// <summary>
        /// The time this frame has started. This is the time in seconds since the last level has been loaded.
        /// </summary>
        public static new double timeSinceLevelLoad { get; private set; }

        /// <summary>
        /// The real time this frame has started. This is the time in seconds since the start of the game unaffected by time scale.
        /// </summary>
        public static new double unscaledTime { get; private set; }

        /// <summary>
        /// The time corresponding to this fixed frame.
        /// </summary>
        public static new double fixedTime { get; private set; }

        /// <summary>
        /// The real time corresponding to this fixed frame.
        /// </summary>
        public static new double fixedUnscaledTime { get; private set; }

        /// <summary>
        /// The real time in seconds since the game started. May be affected by modifications in the system's time clock.
        /// Depending on the platform and the hardware, accuracy may vary from 1ms to tens of milliseconds so 
        /// the same value may be reported in several consecutive frames. If you're dividing something by time difference, 
        /// take this into account as this calculated difference may be zero or consider using <see cref="unscaledTime"/> 
        /// instead.
        /// </summary>
        public static new double realtimeSinceStartup => (DateTime.UtcNow - startupTime).TotalSeconds;

        /// <summary>
        /// UTC date time when the game started. 
        /// </summary>
        public static DateTime startupTime { get; private set; }

        #pragma warning restore IDE1006

        private static double accumulatedTime;
        private static double accumulatedUnscaledTime;
        private static double accumulatedTimeSinceLevelLoad;

        private static double fixedTimeSinceLevelLoad;

        private static void FixedUpdate()
        {
            fixedTime += Time.deltaTime;
            fixedUnscaledTime += Time.unscaledDeltaTime;
            fixedTimeSinceLevelLoad += Time.deltaTime;

            time = fixedTime;
            unscaledTime = fixedUnscaledTime;
            timeSinceLevelLoad = fixedTimeSinceLevelLoad;
        }

        private static void Update()
        {
            accumulatedTime += Time.deltaTime;
            accumulatedUnscaledTime += Time.unscaledDeltaTime;
            accumulatedTimeSinceLevelLoad += Time.deltaTime;

            time = accumulatedTime;
            unscaledTime = accumulatedUnscaledTime;
            timeSinceLevelLoad = accumulatedTimeSinceLevelLoad;
        }

        private static class TimeCommands
        {
            [Console(Name = "time.scale")]
            private static void TimeScale(Console.Writer writer) => writer.Out.WriteLine(Time.timeScale);

            [Console(Name = "time.scaled_time")]
            private static void TimeScaled(Console.Writer writer) => writer.Out.WriteLine(TimeSpan.FromSeconds(Time.time));

            [Console(Name = "time.unscaled_time")]
            private static void ScaledTime(Console.Writer writer) => writer.Out.WriteLine(TimeSpan.FromSeconds(Time.unscaledTime));

            [Console(Name = "time.real_time")]
            private static void RealTimeSinceStartup(Console.Writer writer) => writer.Out.WriteLine(TimeSpan.FromSeconds(Time.realtimeSinceStartup));

            [Console(Name = "time.startup")]
            private static void StartupTime(Console.Writer writer) => writer.Out.WriteLine(Time.startupTime);

            [Console(Name = "time.loaded_time")]
            private static void TimSinceLevelLoad(Console.Writer writer) => writer.Out.WriteLine(TimeSpan.FromSeconds(Time.timeSinceLevelLoad));
        }
    }
}
