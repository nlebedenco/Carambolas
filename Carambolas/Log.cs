using System;
using System.Runtime.CompilerServices;

namespace Carambolas
{
    public interface ILogHandler
    {
        void Info(string s);
        void Warn(string s);
        void Error(string s);
        void Exception(Exception e);
    }

    public static class Log
    {
        private class NullLogHandler: ILogHandler
        {
            public void Info(string s) { }
            public void Warn(string s) { }
            public void Error(string s) { }
            public void Exception(Exception e) { }
        }

        public static ILogHandler DefaultHandler = new NullLogHandler();

        private static ILogHandler handler;

        public static ILogHandler Handler
        {
            get => handler;
            set => handler = value ?? DefaultHandler;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Info(string s) => handler.Info(s);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Warn(string s) => handler.Warn(s);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Error(string s) => handler.Error(s);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Exception(Exception e) => handler.Exception(e);
    }
}
