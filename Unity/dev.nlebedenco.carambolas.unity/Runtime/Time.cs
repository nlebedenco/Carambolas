using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

using UnityTime = UnityEngine.Time;

using Resources = Carambolas.Internal.Resources;
using Strings = Carambolas.Internal.Strings;

namespace Carambolas.UnityEngine
{
    public class Time: UnityTime
    {
        public const float SmoothFrameRateUpdateInterval = 1.0f;

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
      
        public static int frameRate { get; private set; }

        private static float frameRateVariance;
        private static float frameRateEstimate;
        private static float frameRateElapsed;

        public static int smoothFrameRate { get; private set; }
        
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

            var sample = 1.0f / unscaledDeltaTime;
            var delta = sample - frameRateEstimate;
            if (delta < 0)
                delta = -delta;

            frameRate = (int)(sample + 0.5f);
            frameRateVariance = Math.Max(0.001f, (3f * frameRateVariance + delta) * 0.25f);
            frameRateEstimate = Math.Max(0.001f, (7f * frameRateEstimate + sample) * 0.125f);

            // Once per SmoothFrameRateUpdateInterval if the delta from frameRateEstimate to smoothFrameRate 
            // is greater than the frameRateVariance update smoothFrameRate.
            frameRateElapsed += unscaledDeltaTime;
            if (frameRateElapsed >= SmoothFrameRateUpdateInterval)
            {
                if (Math.Abs(frameRateEstimate - smoothFrameRate) > frameRateVariance)
                    smoothFrameRate = (int)(frameRateEstimate + 0.5f);

                frameRateElapsed = 0f;
            }
        }

        private static class TimeCommands
        {

            [Command(
                Name = "time.timeScale",
                Description = "Displays or sets the current time scale.",
                Help = "Usage: time.timeScale [VALUE]\n\n  VALUE         optional value to assign (must be a valid floating point value)"
            )]
            private static void TimeScale(Carambolas.IO.TextWriter writer, IReadOnlyList<string> args)
            {
                var n = args.Count;
                if (n == 0)
                    writer.WriteLine(Time.timeScale.ToString());
                else if (n == 1 && float.TryParse(args[0], out var value))
                    Time.timeScale = value;
                else
                    throw new ArgumentException(Resources.GetString(Strings.InvalidArguments));
            }

            [Command(
                Name = "time.scaledTime",
                Description = "Displays the running time since application start scaled by time.timeScale.",
                Help = "Usage: time.scaledTime\n\nNote that a composite command line may execute across multiple frames so consecutive executions of this command even in the same line may display different values."
            )]
            private static void TimeScaled(Carambolas.IO.TextWriter writer) => writer.WriteLine(TimeSpan.FromSeconds(Time.time).ToString());

            [Command(
                Name = "time.unscaledTime",
                Description = "Displays the running time since application start unscaled.",
                Help = "Usage: time.unscaledTime\n\nNote that a composite command line may execute across multiple frames so consecutive executions of this command even in the same line may display different values."
            )]
            private static void ScaledTime(Carambolas.IO.TextWriter writer) => writer.WriteLine(TimeSpan.FromSeconds(Time.unscaledTime).ToString());

            [Command(
                Name = "time.realtimeSinceStartup",
                Description = "Displays the real time since application start unscaled.",
                Help = "Usage: time.realtimeSinceStartup\n\nNote that a composite command line may execute across multiple frames so consecutive executions of this command even in the same line may display different values."
            )]
            private static void RealTimeSinceStartup(Carambolas.IO.TextWriter writer) => writer.WriteLine(TimeSpan.FromSeconds(Time.realtimeSinceStartup).ToString());

            [Command(
                Name = "time.startupTime",
                Description = "Displays the date/time of application startup in UTC."
            )]
            private static void StartupTime(Carambolas.IO.TextWriter writer) => writer.WriteLine(Time.startupTime.ToString());

            [Command(
                Name = "time.timeSinceLevelLoad",
                Description = "Displays the time since the last scene load scaled by time.timeScale.",
                Help = "Usage: time.timeSinceLevelLoad\n\nNote that a composite command line may execute across multiple frames so consecutive executions of this command even in the same line may display different values."
            )]
            private static void TimSinceLevelLoad(Carambolas.IO.TextWriter writer) => writer.WriteLine(TimeSpan.FromSeconds(Time.timeSinceLevelLoad).ToString());

            [Command(
                Name = "time.frameCount",
                Description = "Displays the current frame count.",
                Help = "Usage: time.frameCount\n\nNote that a composite command line may execute across multiple frames so consecutive executions of this command even in the same line may display different values."
            )]
            private static void TimeFrameCount(Carambolas.IO.TextWriter writer) => writer.WriteLine(Time.frameCount.ToString());

            [Command(
                Name = "time.renderedFrameCount",
                Description = "Displays the current rendered frame count.",
                Help = "Usage: time.renderedFrameCount\n\nNote that a composite command line may execute across multiple frames so consecutive executions of this command even in the same line may display different values."
            )]
            private static void TimeRenderedFrameCount(Carambolas.IO.TextWriter writer) => writer.WriteLine(Time.renderedFrameCount.ToString());

            [Command(
                Name = "time.frameRate",
                Description = "Displays the instant frame rate.",
                Help = "Usage: time.frameRate\n\nNote that a composite command line may execute across multiple frames so consecutive executions of this command even in the same line may display different values."
            )]
            private static void TimeFrameRate(Carambolas.IO.TextWriter writer) => writer.WriteLine(Time.frameRate.ToString());

            [Command(
                Name = "time.smoothFrameRate",
                Description = "Displays a smoothed frame rate.",
                Help = "Usage: time.smoothFrameRate\n\nNote that a composite command line may execute across multiple frames so consecutive executions of this command even in the same line may display different values."
            )]
            private static void TimeRenderedFrameRate(Carambolas.IO.TextWriter writer) => writer.WriteLine(Time.smoothFrameRate.ToString());
        }
    }
}
