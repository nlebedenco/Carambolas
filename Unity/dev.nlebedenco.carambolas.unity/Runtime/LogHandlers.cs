using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Carambolas.UnityEngine
{
    internal sealed class NullLogHandler: ILogHandler
    {
        public void LogException(Exception e, UnityObject context) { }

        public void LogFormat(LogType logType, UnityObject context, string format, params object[] args) { }
    }

    internal sealed class FileLogHandler: ILogHandler, IDisposable
    {
        private ILogHandler next;
        private StreamWriter writer;

        public FileLogHandler(ILogHandler loghandler, string filename) => (this.next, writer) = (loghandler, new StreamWriter(filename, false) { AutoFlush = true });
        public FileLogHandler(ILogHandler loghandler, string filename, Encoding encoding) => (this.next, this.writer) = (loghandler, new StreamWriter(filename, false, encoding) { AutoFlush = true });
        public FileLogHandler(ILogHandler loghandler, StreamWriter writer) => (this.next, this.writer) = (loghandler, writer);

        public void Dispose()
        {
            if (writer != null)
            {
                writer.Dispose();
                writer = null;

                next = null;
            }
        }

        public void LogException(Exception e, UnityObject context)
        {
            writer.WriteLine(e.ToString());
            next.LogException(e, context);
        }

        public void LogFormat(LogType logType, UnityObject context, string format, params object[] args)
        {
            writer.WriteLine(format, args);
            next.LogFormat(logType, context, format, args);
        }
    }

    internal sealed class TimestampLogHandler: ILogHandler
    {
        private readonly ILogHandler next;

        public TimestampLogHandler(ILogHandler loghandler) => this.next = loghandler;

        private string LogTypeToString(LogType logType)
        {
            switch (logType)
            {
                case LogType.Error:
                    return "ERROR";
                case LogType.Assert:
                    return "ASSERTION";
                case LogType.Warning:
                    return "WARNING";
                case LogType.Log:
                    return "INFO";
                case LogType.Exception:
                    return "EXCEPTION";
                default:
                    return string.Empty;
            }
        }

        public void LogException(Exception e, UnityObject context)
        {
            var now = DateTime.UtcNow.ToString("o");

            // HACK: workaround for StackTraceLogType ignored for LogType.Exception and a stack trace always printed (as of Unity 2019.1.11f),
            var msg = Application.GetStackTraceLogType(LogType.Exception) == StackTraceLogType.None ? e.Message : StackTraceUtility.ExtractStringFromException(e);

            if (context == null)
                next.LogFormat(LogType.Error, context, $"{now} [{LogTypeToString(LogType.Exception)}] {msg}");
            else
                next.LogFormat(LogType.Error, context, $"{now} [{LogTypeToString(LogType.Exception)}] ({context.GetType().FullName}) {msg}");
        }

        public void LogFormat(LogType logType, UnityObject context, string format, params object[] args)
        {
            var now = DateTime.UtcNow.ToString("o");

            // HACK: Unity will replace every LF with CRLF before writing to the file (at least on Windows) so make sure our output string contains no CR
            var msg = string
                .Format(format, args)
                .Replace("\r", string.Empty)
                .TrimEnd();

            if (Application.GetStackTraceLogType(logType) != StackTraceLogType.None)
                msg = $"{msg}\n{StackTraceUtility.ExtractStackTrace()}";

            if (context == null)
                next.LogFormat(logType, context, $"{now} [{LogTypeToString(logType)}] {msg}");
            else
                next.LogFormat(logType, context, $"{now} [{LogTypeToString(logType)}] ({context.GetType().FullName}) {msg}");
        }
    }
}
