using System;

using UnityEngine;
using UnityApplication = UnityEngine.Application;
using UnityObject = UnityEngine.Object;
using IUnityLogHandler = UnityEngine.ILogHandler;

// TODO: address the issue of UNITY_EDITOR ifdefs not making much sense in a precompiled library

namespace Carambolas.UnityEngine
{
    public class Application: UnityApplication
    {
        protected Application() { }

        private sealed class PlayerLogHandler: IUnityLogHandler
        {
            private IUnityLogHandler loghandler;

            public PlayerLogHandler(IUnityLogHandler loghandler) => this.loghandler = loghandler;

            private string LogTypeToString(LogType logType)
            {
                switch (logType)
                {
                    case LogType.Error:
                        return "[ERROR] ";
                    case LogType.Assert:
                        return "[ASSERTION FAILED] ";
                    case LogType.Warning:
                        return "[WARNING] ";
                    case LogType.Log:
                        return "[INFO] ";
                    case LogType.Exception:
                        return "[EXCEPTION] ";
                    default:
                        return string.Empty;
                }
            }

            public void LogException(Exception e, UnityObject context)
            {
                // HACK: Workaround for StackTraceLogType ignored for LogType.Exception and a stack trace always printed (as of Unity 2019.1.11f),
                loghandler.LogFormat(LogType.Error, context,
                    string.Format("{0} {1}{2}{3}",
                        DateTime.UtcNow.ToString("o"),
                        LogTypeToString(LogType.Exception),
                        context ? $"({context.GetType().FullName})" : "",
                        Application.GetStackTraceLogType(LogType.Exception) == StackTraceLogType.None ? e.Message : e.ToString()));
            }

            public void LogFormat(LogType logType, UnityObject context, string format, params object[] args)
            {
                // HACK: Unity will replace every LF with CRLF before writing to the file (at least on Windows) so make sure our output string contains no CR
                var msg = string
                    .Format(format, args)
                    .Replace("\r", string.Empty)
                    .TrimEnd();

                loghandler.LogFormat(logType, context,
                    string.Format("{0} {1}{2}{3}",
                        DateTime.UtcNow.ToString("o"),
                        LogTypeToString(logType),
                        context ? $"({context.GetType().FullName})" : "",
                        msg));
            }
        }

        #pragma warning disable IDE1006

        public static bool terminated { get; private set; }

        public static readonly CommandLineArguments commandLineArguments = new CommandLineArguments();

        #pragma warning restore IDE1006

        public static new void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityApplication.Quit();
#endif
        }

        public static new void Quit(int exitcode)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityApplication.Quit(exitcode);
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void OnRuntimeInitialize()
        {
            // Set default log level according to build
            Debug.unityLogger.filterLogType = Debug.isDebugBuild ? LogType.Log : LogType.Warning;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#else
            Application.quitting += OnApplicationQuitting;
            Debug.unityLogger.logHandler = new PlayerLogHandler(Debug.unityLogger.logHandler);
#endif
            var build = Debug.isDebugBuild ? "DEBUG" : "RELEASE";
            System.Console.WriteLine($"Application version: {Application.version} ({build})");

            // Sanity check
            if (terminated) 
                return;

            LogInit();
        }

#if UNITY_EDITOR
        // As of Unity 2019.1.11f the editor does not execute BeforeSpashScreen
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnRuntimeInitializeBeforeSceneLoad() => OnRuntimeInitialize();
#endif

        private static void OnApplicationQuitting() => terminated = true;

#if UNITY_EDITOR
        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange s)
        {
            if (s == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                OnApplicationQuitting();
        }
#endif

        private static void LogInit()
        {
#if !UNITY_EDITOR
            // Set log level override from command-line
            if (commandLineArguments.TryGetValue("loglevel", out string logLevel) && !string.IsNullOrEmpty(logLevel))
            {
                if (Enum.TryParse(logLevel, out LogLevel value))
                {
                    System.Console.WriteLine(string.Format("Log level set from command-line: '{0}'\n", value));
                    Debug.logLevel = value;
                }
                else
                {
                    Debug.LogWarningFormat("Invalid -loglevel argument value: {0}", logLevel);
                }
            }
#endif
        }
    }
}
