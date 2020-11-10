using System;
using System.IO;
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

      

        #pragma warning disable IDE1006

        public static bool terminated { get; private set; }

        public static bool isServerBuild { get; internal set; }

        /// <summary>
        /// True if this is a server build and a command line interface has been requested in the command line arguments
        /// </summary>
        public static bool isInteractiveServerBuild { get; private set; }

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
        public static void OnAfterAssembliesLoaded()
        {
            isInteractiveServerBuild = commandLineArguments.Contains("cli");
        }

        private static void Warning(string msg)
        {
            if (isServerBuild)
                Console.WriteLine(msg);
            else
                Debug.LogWarning(msg);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        public static void OnBeforeSplashScreen()
        {
            // Set default log level according to build
            Debug.unityLogger.filterLogType = Debug.isDebugBuild ? LogType.Log : LogType.Warning;
            
            Carambolas.Internal.Log.Handler = new RedirectCarambolasInternalLogToUnityDebugLog();

#if !UNITY_EDITOR
            if (commandLineArguments.Contains("nolog")) // unity seems to ignore -nolog on server builds, so force it here
            {
                Debug.unityLogger.logEnabled = false;
                Debug.unityLogger.logHandler = new NullLogHandler();
            }
            else 
            {
                // Set log level override from command-line
                if (commandLineArguments.TryGetValue("logLevel", out string logLevel) && !string.IsNullOrEmpty(logLevel))
                {
                    if (Enum.TryParse(logLevel, out LogLevel value))
                    {
                        Console.WriteLine($"Log level set from command-line: '{value}'");
                        Debug.logLevel = value;
                    }
                    else
                    {
                        Warning($"Invalid -logLevel argument value: {logLevel}");
                    }
                }
            }

            if (isInteractiveServerBuild)
            {
                if (!isServerBuild)
                {
                    isInteractiveServerBuild = false;        
                    Warning("Ignoring -cli argument. Command line interface can only be used in server builds");
                    
                }
                else if (Console.IsOutputRedirected && !string.IsNullOrEmpty(Application.consoleLogPath))
                {
                    isInteractiveServerBuild = false;
                    Debug.LogError($"Unity is redirecting standard output to {Application.consoleLogPath} which prevents the command line interface from working properly. Use -logOutput instead of -logFile to redirect only log messages and not the whole standard input/ouput. If you want to redirect standard input/output for the command line interface itself, use proper redirection operators when invoking the application instead of -logFile.");
                }
                else if (!(Debug.unityLogger.logHandler is NullLogHandler))
                {
                    // Suppress log messages so they don't interfere with the CLI.
                    Debug.unityLogger.logHandler = new NullLogHandler(); 
                    Console.WriteLine($"Suppressing log messages from the console so they do not interfere with the command line interface.");
                }
            }            
#endif

            if (commandLineArguments.TryGetValue("logOutput", out string filename))
            {
                if (Application.isConsolePlatform || Application.isMobilePlatform || Application.isEditor)
                    Debug.LogWarning($"Ignoring -logOutput argument. Not supported on {Application.platform}.");
                else if (!Debug.unityLogger.logEnabled)
                    Debug.LogWarning($"Ignoring -logOutput argument. Log is disabled.");
                else
                {
                    var hasConsoleLogPath = !string.IsNullOrEmpty(Application.consoleLogPath);
                    if (string.IsNullOrWhiteSpace(filename) && hasConsoleLogPath)
                        filename = Path.ChangeExtension(Application.consoleLogPath, "log.output");

                    if (string.IsNullOrWhiteSpace(filename))
                    {
                        Warning("Ignoring -logOutput argument. Value is empty and a default cannot be determined.");
                    }
                    else
                    {
                        try
                        {
                            var writer = new StreamWriter(filename);

                            Warning($"Log output set from command line: '{filename}'");
                            if (!(Debug.unityLogger.logHandler is NullLogHandler))
                            {
                                if (hasConsoleLogPath) // in this case stdout is expected to be redirected to the file
                                    Debug.LogWarning($"Suppressing log messages from {Application.consoleLogPath}");

                                Debug.unityLogger.logHandler = new NullLogHandler();
                            }

                            Debug.unityLogger.logHandler = new FileLogHandler(Debug.unityLogger.logHandler, writer);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError(e.Message);
                        }
                    }
                }
            }

#if !UNITY_EDITOR
            // Setup a timestamp log handler for more detailed messages
            if (!commandLineArguments.Contains("nologtimestamp"))
                Debug.unityLogger.logHandler = new TimestampLogHandler(Debug.unityLogger.logHandler);
            Application.quitting += OnApplicationQuitting;            
#endif

            var build = Debug.isDebugBuild ? "DEBUG" : "RELEASE";
            var server = isServerBuild ? "SERVER" : "";
            var info = $"{Application.productName} version: {Application.version}:{buildGUID} ({build}) {server}";
            if (isServerBuild)
                Console.WriteLine(info);

            Debug.Log(info);

#if UNITY_EDITOR
            global::UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
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
            Debug.Log("Application terminated.");
        }
    }
}
