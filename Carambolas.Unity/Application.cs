using System;

using UnityEngine;
using UnityApplication = UnityEngine.Application;
using UnityObject = UnityEngine.Object;

namespace Carambolas.UnityEngine
{
    public class Application: UnityApplication
    {
        protected Application() { }

        private sealed class RedirectCarambolasInternalLogToUnityDebugLog: Carambolas.Internal.ILogHandler
        {
            public void Error(string s) => Debug.LogError(s);

            public void Exception(Exception e) => Debug.LogException(e);

            public void Info(string s) => Debug.Log(s);

            public void Warn(string s) => Debug.LogWarning(s);
        }

        private sealed class DetailLogHandler: ILogHandler
        {
            private ILogHandler loghandler;

            public DetailLogHandler(ILogHandler loghandler) => this.loghandler = loghandler;

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
            global::UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityApplication.Quit();
#endif
        }

        public static new void Quit(int exitcode)
        {
#if UNITY_EDITOR
            global::UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityApplication.Quit(exitcode);
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void OnAfterAssembliesLoaded()
        {
            // Set default log level according to build
            Debug.unityLogger.filterLogType = Debug.isDebugBuild ? LogType.Log : LogType.Warning;

            Carambolas.Internal.Log.Handler = new RedirectCarambolasInternalLogToUnityDebugLog();

#if !UNITY_EDITOR
            // Set log level override from command-line
            if (commandLineArguments.TryGetValue("loglevel", out string logLevel) && !string.IsNullOrEmpty(logLevel))
            {
                if (Enum.TryParse(logLevel, out LogLevel value))
                {
                    System.Console.WriteLine($"Log level set from command-line: '{value}'\n");
                    Debug.logLevel = value;
                }
                else
                {
                    Debug.LogWarning($"Invalid -loglevel argument value: {logLevel}");
                }
            }
#endif

            var build = Debug.isDebugBuild ? "DEBUG" : "RELEASE";
            Debug.Log($"{Application.productName} version: {Application.version} ({build})");

#if UNITY_EDITOR
            global::UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif

#if !UNITY_EDITOR
            Debug.unityLogger.logHandler = new DetailLogHandler(Debug.unityLogger.logHandler);
            Application.quitting += OnApplicationQuitting;            
#endif
        }

#if UNITY_EDITOR
        private static void OnPlayModeStateChanged(global::UnityEditor.PlayModeStateChange s)
        {
            if (s == global::UnityEditor.PlayModeStateChange.ExitingPlayMode)
                OnApplicationQuitting();
        }
#endif

        private static void OnApplicationQuitting()
        {
            terminated = true;
        }
    }
}
