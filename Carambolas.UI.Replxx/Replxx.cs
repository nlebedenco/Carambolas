using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;

using Carambolas.Runtime.InteropServices;

namespace Carambolas.UI
{
    public sealed class Replxx: IDisposable
    {
        public enum Color: int
        {
            Black = 0,
            Red = 1,
            Green = 2,
            Brown = 3,
            Blue = 4,
            Magenta = 5,
            Cyan = 6,
            LightGray = 7,
            Gray = 8,
            BrightRed = 9,
            BrightGreen = 10,
            Yellow = 11,
            BrightBlue = 12,
            BrightMagenta = 13,
            BrightCyan = 14,
            White = 15,
            Normal = LightGray,
            Default = -1,
            Error = -2
        }

        public static class Key
        {
            public const uint Base = 0x0010ffff + 1;
            public const uint BaseShift = 0x01000000;
            public const uint BaseControl = 0x02000000;
            public const uint BaseMeta = 0x04000000;
            public const uint Escape = 27;
            public const uint PageUp = Base + 1;
            public const uint PageDown = PageUp + 1;
            public const uint Down = PageDown + 1;
            public const uint Up = Down + 1;
            public const uint Left = Up + 1;
            public const uint Right = Left + 1;
            public const uint Home = Right + 1;
            public const uint End = Home + 1;
            public const uint Delete = End + 1;
            public const uint Insert = Delete + 1;
            public const uint F1 = Insert + 1;
            public const uint F2 = F1 + 1;
            public const uint F3 = F2 + 1;
            public const uint F4 = F3 + 1;
            public const uint F5 = F4 + 1;
            public const uint F6 = F5 + 1;
            public const uint F7 = F6 + 1;
            public const uint F8 = F7 + 1;
            public const uint F9 = F8 + 1;
            public const uint F10 = F9 + 1;
            public const uint F11 = F10 + 1;
            public const uint F12 = F11 + 1;
            public const uint F13 = F12 + 1;
            public const uint F14 = F13 + 1;
            public const uint F15 = F14 + 1;
            public const uint F16 = F15 + 1;
            public const uint F17 = F16 + 1;
            public const uint F18 = F17 + 1;
            public const uint F19 = F18 + 1;
            public const uint F20 = F19 + 1;
            public const uint F21 = F20 + 1;
            public const uint F22 = F21 + 1;
            public const uint F23 = F22 + 1;
            public const uint F24 = F23 + 1;
            public const uint Mouse = F24 + 1;
            public const uint PasteStart = Mouse + 1;
            public const uint PasteFinish = PasteStart + 1; 

            public const uint Backspace = 'H' | BaseControl;
            public const uint Tab = 'I' | BaseControl;
            public const uint Enter = 'M' | BaseControl;

            public static uint Shift(uint key) => key | BaseShift;
            public static uint Control(uint key) => key | BaseControl;
            public static uint Meta(uint key) => key | BaseMeta;
        }

        public readonly ref struct Hints
        {
            private readonly IntPtr hints;

            internal Hints(IntPtr hints) => this.hints = hints;

            public void Add(string s) => Native.Hints.Add(hints, s);
        }

        public readonly ref struct Completions
        {
            private readonly IntPtr completions;

            internal Completions(IntPtr completions) => this.completions = completions;

            public void Add(string s) => Native.Completions.Add(completions, s);
            public void Add(string s, Color color) => Native.Completions.Add(completions, s, color);
        }

        private IntPtr replxx;

        public Replxx()
        {
            replxx = Native.Initialize();
            if (replxx == IntPtr.Zero)
                throw new ContextMarshalException("Initialization error");

            
            completionCallback = OnCompletionRequested;  // guard against GC
            Native.Completions.SetCallback(replxx, completionCallback);

            hintCallback = OnHintRequested; // guard against GC
            Native.Hints.SetCallback(replxx, hintCallback);

            highlightCallback = OnHighlightRequested;  // guard against GC
            Native.Highlight.SetCallback(replxx, highlightCallback);

        }

        public string Read(string prompt) => Native.Read(replxx, prompt);

        public void Write(string s) => Native.Write(replxx, s);

        public void Write(char c) => Native.Write(replxx, c);

        public void EmulateKeyPress(uint keycode) => Native.EmulateKeyPress(replxx, keycode);

        public void Clear()
        {
            // Replxx clear screen is currently broken on windows. 
            // Using System.Console.Clear on windows until the issue is resolved.
            // Check https://github.com/AmokHuginnsson/replxx/issues/89.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                System.Console.Clear();
            else
                Native.ClearScreen(replxx);
        }

        #region Settings

        public void SetPreloadBuffer(string value) => Native.SetPreloadBuffer(replxx, value);
        public void SetWordBreakCharacters(string value) => Native.SetWordBreakCharacters(replxx, value);
        public void SetNoColor(bool value) => Native.SetNoColor(replxx, value);
        public void EnableBracketedPaste() => Native.EnableBracketedPaste(replxx);
        public void DisableBracketedPaste() => Native.DisableBracketedPaste(replxx);

        #endregion

        #region History

        public void AddToHistory(string s) => Native.History.Add(replxx, s);
        public int GetHistorySize() => Native.History.GetSize(replxx);
        public void SetMaxHistorySize(int value) => Native.History.SetMaxSize(replxx, value);
        public void ClearHistory() => Native.History.Clear(replxx);
        public void SetUniqueHistory(bool value) => Native.History.SetUnique(replxx, value);

        #endregion       

        #region Completions 

        private readonly Native.Completions.Callback completionCallback;

        private void OnCompletionRequested(string input, IntPtr completions, ref int length, IntPtr userData)
        {
            var handler = CompletionRequested;
            handler?.Invoke(input, ref length, new Completions(completions));
        }

        public delegate void CompletionRequestDelegate(string input, ref int length, in Completions completions);

        public CompletionRequestDelegate CompletionRequested;

        public void SetCompletionCountCutOff(int value) => Native.Completions.SetCountCutOff(replxx, value);
        public void SetEmptyCompletion(bool value) => Native.Completions.SetEmpty(replxx, value);
        public void SetDoubleTabCompletion(bool value) => Native.Completions.SetDoubleTab(replxx, value);        
        public void SetImmediateCompletion(bool value) => Native.Completions.SetImmediate(replxx, value);
        public void SetBeepOnAmbiguousCompletion(bool value) => Native.Completions.SetBeepOnAmbiguous(replxx, value);

        #endregion

        #region Hints

        private readonly Native.Hints.Callback hintCallback;

        private void OnHintRequested(string input, IntPtr hints, ref int length, ref Color color, IntPtr userData)
        {
            var handler = HintRequested;
            handler?.Invoke(input, ref length, new Hints(hints));
        }

        public delegate void HintRequestDelegate(string input, ref int length, in Hints hints);

        public HintRequestDelegate HintRequested;

        public void SetMaxHintRows(int value) => Native.Hints.SetMaxRows(replxx, value);

        #endregion

        #region Highlight

        private readonly Native.Highlight.Callback highlightCallback;

        private void OnHighlightRequested(string input, IntPtr colors, int length, IntPtr userData)
        {
            var handler = HighlightRequested;
            if (handler != null)
            {
                if (length == 0 || colors == IntPtr.Zero)
                    handler.Invoke(input, Array.Empty<Color>());
                else
                {
                    var array = new Color[length];
                    for (int i = 0, offset = 0; i < length; ++i, offset += sizeof(int))
                        array[i] = (Color)Marshal.ReadInt32(colors, offset);

                    handler.Invoke(input, array);

                    for (int i = 0, offset = 0; i < length; ++i, offset += sizeof(int))
                        Marshal.WriteInt32(colors, offset, (int)array[i]);
                }
            }
        }

        public delegate void HighlightRequestDelegate(string input, Color[] colors);

        public HighlightRequestDelegate HighlightRequested;

        #endregion

        #region IDisposable

        private bool disposed = false;

        void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Dispose managed state (managed objects).
                }

                Native.Finalize(replxx);
                replxx = IntPtr.Zero;

                disposed = true;
            }
        }

        ~Replxx()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion      

        /// <summary>
        /// Wrapper over https://github.com/AmokHuginnsson/replxx
        /// </summary>
        private class Native
        {
            private const string NativeLibraryName = "replxx";

            [DllImport(NativeLibraryName, EntryPoint = "replxx_init", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            public static extern IntPtr Initialize();

            [DllImport(NativeLibraryName, EntryPoint = "replxx_end", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            public static extern void Finalize(IntPtr replxx);

            // NOTE: comment in replxx source code (replxx.cxx) indicating that the pointer returned by replxx_input must be freed by the caller is wrong. This memory is 
            // tracked by replxx and must be left alone.
            [DllImport(NativeLibraryName, EntryPoint = "replxx_input", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(InputUtf8Marshaler))]
            public static extern string Read(
                IntPtr replxx,
                [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(OutputUtf8Marshaler))] string prompt);

            [DllImport(NativeLibraryName, EntryPoint = "replxx_write", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            private static extern int Write(IntPtr replxx, byte[] utf8,  int length);
            public static int Write(IntPtr replxx, string s)
            {
                if (string.IsNullOrEmpty(s))
                    return 0;
            
                var n = Encoding.UTF8.GetMaxByteCount(s.Length) + 1;
                var utf8 = ArrayPool<byte>.Shared.Rent(n);
                var length = Encoding.UTF8.GetBytes(s, 0, s.Length, utf8, 0);
                try
                {
                    return Write(replxx, utf8, length);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(utf8);
                }
            }

            [DllImport(NativeLibraryName, EntryPoint = "replxx_write", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true)]
            private static extern int Write(IntPtr replxx, ref char c, int length);
            public static int Write(IntPtr replxx, char c) => Write(replxx, ref c, sizeof(char));

            [DllImport(NativeLibraryName, EntryPoint = "replxx_clear_screen", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            public static extern void ClearScreen(IntPtr replxx);

            [DllImport(NativeLibraryName, EntryPoint = "replxx_emulate_key_press", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            public static extern void EmulateKeyPress(IntPtr replxx, uint keycode);

            [DllImport(NativeLibraryName, EntryPoint = "replxx_set_preload_buffer", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true)]
            public static extern void SetPreloadBuffer(
                IntPtr replxx, 
                [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(OutputUtf8Marshaler))] string s);

            /// <summary>
            /// This setting influences word based cursor movement and line editing capabilities.
            /// </summary>
            /// <param name="replxx">Native handle.</param>
            /// <param name="value">7-bit ASCII set of word breaking characters.</param>
            [DllImport(NativeLibraryName, EntryPoint = "replxx_set_word_break_characters", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi,  ExactSpelling = true)]
            public static extern void SetWordBreakCharacters(IntPtr replxx, string value);

            [DllImport(NativeLibraryName, EntryPoint = "replxx_set_no_color", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            public static extern void SetNoColor(
                IntPtr replxx, 
                [MarshalAs(UnmanagedType.Bool)] bool value);

            [DllImport(NativeLibraryName, EntryPoint = "replxx_enable_bracketed_paste", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            public static extern void EnableBracketedPaste(IntPtr replxx);

            [DllImport(NativeLibraryName, EntryPoint = "replxx_disable_bracketed_paste", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            public static extern void DisableBracketedPaste(IntPtr replxx);

            public static class Completions
            {
                public delegate void Callback(
                    [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(InputUtf8Marshaler))] string input,
                    IntPtr completions,
                    ref int contextLength,
                    IntPtr userData);

                [DllImport(NativeLibraryName, EntryPoint = "replxx_set_completion_callback", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
                private static extern void SetCallback(IntPtr replxx, Callback callback, IntPtr userData);
                public static void SetCallback(IntPtr replxx, Callback callback) => SetCallback(replxx, callback, IntPtr.Zero);

                [DllImport(NativeLibraryName, EntryPoint = "replxx_add_completion", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
                public static extern void Add(
                    IntPtr completions,
                    [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(OutputUtf8Marshaler))] string s);

                [DllImport(NativeLibraryName, EntryPoint = "replxx_add_color_completion", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true)]
                public static extern void Add(
                    IntPtr completions, 
                    [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(OutputUtf8Marshaler))] string s, 
                    Color color);

                [DllImport(NativeLibraryName, EntryPoint = "replxx_set_completion_count_cutoff", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
                public static extern void SetCountCutOff(IntPtr replxx, int value);

                [DllImport(NativeLibraryName, EntryPoint = "replxx_set_complete_on_empty", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
                public static extern void SetEmpty(
                    IntPtr replxx, 
                    [MarshalAs(UnmanagedType.Bool)] bool value);

                [DllImport(NativeLibraryName, EntryPoint = "replxx_set_double_tab_completion", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
                public static extern void SetDoubleTab(
                    IntPtr replxx, 
                    [MarshalAs(UnmanagedType.Bool)] bool value);

                [DllImport(NativeLibraryName, EntryPoint = "replxx_set_immediate_completion", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
                public static extern void SetImmediate(
                    IntPtr replxx, 
                    [MarshalAs(UnmanagedType.Bool)] bool value);

                [DllImport(NativeLibraryName, EntryPoint = "replxx_set_beep_on_ambiguous_completion", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
                public static extern void SetBeepOnAmbiguous(
                    IntPtr replxx, 
                    [MarshalAs(UnmanagedType.Bool)] bool value);
            }

            public static class Hints
            {
                public delegate void Callback(
                    [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(InputUtf8Marshaler))] string input,
                    IntPtr hints,
                    ref int contextLength,
                    ref Color color,
                    IntPtr userData);

                [DllImport(NativeLibraryName, EntryPoint = "replxx_set_hint_callback", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
                private static extern void SetCallback(IntPtr replxx, Callback callback, IntPtr userData);
                public static void SetCallback(IntPtr replxx, Callback callback) => SetCallback(replxx, callback, IntPtr.Zero);

                [DllImport(NativeLibraryName, EntryPoint = "replxx_add_hint", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
                public static extern void Add(
                    IntPtr hints,
                    [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(OutputUtf8Marshaler))] string s);

                [DllImport(NativeLibraryName, EntryPoint = "replxx_set_max_hint_rows", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
                public static extern void SetMaxRows(IntPtr replxx, int value);
            }

            public static class Highlight
            {
                public delegate void Callback(
                    [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(InputUtf8Marshaler))] string input,
                    IntPtr colors, // we can't simply declare [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] Color[] here because the parameter may be null and the marshaler for LPArray does not like null pointers.
                    int length,
                    IntPtr userData);

                [DllImport(NativeLibraryName, EntryPoint = "replxx_set_highlighter_callback", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
                private static extern void SetCallback(IntPtr replxx, Callback callback, IntPtr userData);
                public static void SetCallback(IntPtr replxx, Callback callback) => SetCallback(replxx, callback, IntPtr.Zero);
            }

            public static class History
            {
                [DllImport(NativeLibraryName, EntryPoint = "replxx_history_add", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
                public static extern void Add(
                    IntPtr replxx, 
                    [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(OutputUtf8Marshaler))] string s);

                [DllImport(NativeLibraryName, EntryPoint = "replxx_history_size", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
                public static extern int GetSize(IntPtr replxx);

                [DllImport(NativeLibraryName, EntryPoint = "replxx_history_clear", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
                public static extern void Clear(IntPtr replxx);

                [DllImport(NativeLibraryName, EntryPoint = "replxx_history_save", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true)]
                public static extern int Save(
                    IntPtr replxx, 
                    [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(OutputUtf8Marshaler))] string filename);

                [DllImport(NativeLibraryName, EntryPoint = "replxx_history_load", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true)]
                public static extern int Load(
                    IntPtr replxx, 
                    [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(OutputUtf8Marshaler))] string filename);

                [DllImport(NativeLibraryName, EntryPoint = "replxx_set_max_history_size", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
                public static extern void SetMaxSize(IntPtr replxx, int value);

                [DllImport(NativeLibraryName, EntryPoint = "replxx_set_unique_history", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
                public static extern void SetUnique(
                    IntPtr replxx, 
                    [MarshalAs(UnmanagedType.Bool)] bool value);
            }

        }
    }
}
