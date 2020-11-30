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
                
        #if UNITY_SERVER
        public static readonly bool isServerBuild = true;
#else
        public static readonly bool isServerBuild = false;
#endif

        /// <summary>
        /// True if this is a server build and a command line interface has been requested in the command line arguments
        /// </summary>
        public static bool isInteractiveServerBuild { get; private set; }

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
        public static void OnAfterAssembliesLoaded()
        {
            isInteractiveServerBuild = commandLineArguments.Contains("cli");
        }

        private static void Warning(string msg)
        {
            if (isServerBuild)
                System.Console.WriteLine(msg);
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
            // It's just a waste of CPU to update too often. Anything above 1000fps is probably too much. 
            // As the update frequency approaches 1KHz, delta time becomes too small and ordinary code meant 
            // to just check state throughout the application becomes more and more expensive compared to the 
            // actual changes produced. As the frame rate moves beyond 1Khz we may reach a point where most CPU
            // time is wasted doing nothing or producing numerical anomalies due to an excessively small deltaTime.
            // Application.targetFrameRate should come to help, specially in the case of headless servers, but in
            // fact, as explained in https://forum.unity.com/threads/application-targetframerat-it-doesnt-work-in-version-2020-2-0b5-3233.982353/#post-6446847
            // it becomes increasingly unreliable for targets above 30fps. It also does not account for the
            // time taken by other threads. The result is that Application.targetFrameRate servers as an upper limit 
            // beyond which the frame rate will never pass but at the cost of an actual frame rate that is always 
            // lower than the target (for values > 30fps) even in situations when the target would be perfectly 
            // achievable.
            //
            // According to https://docs.unity3d.com/ScriptReference/Application-targetFrameRate.html:
            //
            // The default targetFrameRate is a special value of -1, which indicates that the game should render at 
            // the platform's default frame rate. This default rate depends on the platform:
            //
            // - On standalone platforms the default frame rate is the maximum achievable frame rate.
            // 
            // - On mobile platforms the default frame rate is less than the maximum achievable frame rate due to 
            // the need to conserve battery power. Typically on mobile platforms the default frame rate is 30 frames 
            // per second.
            // 
            // - All mobile platforms have a fix cap for their maximum achievable frame rate, that is equal to the 
            // refresh rate of the screen (60 Hz = 60 fps, 40 Hz = 40 fps, ...). Screen.currentResolution contains the
            // screen's refresh rate.
            // 
            // - Additionally, all mobile platforms can only display frames on a VBlank. Therefore, you should set the 
            // targetFrameRate to either -1, or a value equal to the screen's refresh rate, or the refresh rate divided by an integer. Otherwise, the resulting frame rate is always lower than targetFrameRate. Note: If you set the targetFrameRate to the refresh rate divided by an integer, the integer division leads to the same effective fps as setting QualitySettings.vSyncCount to the same value as that integer.
            // 
            // - iOS ignores the QualitySettings.vSyncCount setting. Instead, the device displays frames on the first
            // possible VBlank after the frame is ready and your application achieves the targetFrameRate.
            // 
            // - On WebGL the default value lets the browser choose the frame rate to match its render loop timing which
            // generally produces the smoothest results. Non-default values are only recommended if you want to throttle 
            // CPU usage on WebGL.
            // 
            // - When using VR Unity will use the target frame rate specified by the SDK and ignores values specified by the game.
            
            // This keeps FPS in a safe zone on platforms that are not automatically capped unless the user sets it explicitly (see comments above)
            if (!isConsolePlatform && !isMobilePlatform && targetFrameRate < 0)
                targetFrameRate = 1000;

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
                        System.Console.WriteLine($"Log level set from command-line: '{value}'");
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
                else if (System.Console.IsOutputRedirected && !string.IsNullOrEmpty(Application.consoleLogPath))
                {
                    isInteractiveServerBuild = false;
                    Debug.LogError($"Unity is redirecting standard output to {Application.consoleLogPath} which prevents the command line interface from working properly. Use -logOutput instead of -logFile to redirect only log messages and not the whole standard input/ouput. If you want to redirect standard input/output for the command line interface itself, use proper redirection operators when invoking the application instead of -logFile.");
                }
                else if (!(Debug.unityLogger.logHandler is NullLogHandler))
                {
                    // Suppress log messages so they don't interfere with the CLI.
                    Debug.unityLogger.logHandler = new NullLogHandler();
                    System.Console.WriteLine($"Suppressing log messages from the console so they do not interfere with the command line interface.");
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
            using (var sb = new Carambolas.Text.StringBuilder.Buffer())
            {
                sb.Append(Application.productName);
                sb.Append(" version: ");
                sb.Append(Application.version);
                if (Debug.isDebugBuild)
                    sb.Append(" (DEBUG)");
                if (isServerBuild)
                    sb.Append(" SERVER");
                sb.Append(' ');
                sb.Append(buildGUID);

                if (isServerBuild)
                    System.Console.Out.WriteLine(sb);

                Debug.Log(sb.ToString());
            }

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
