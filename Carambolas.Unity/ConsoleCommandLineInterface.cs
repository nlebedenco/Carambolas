using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using UnityEngine;

using Resources = Carambolas.Internal.Resources;
using Strings = Carambolas.Internal.Strings;

namespace Carambolas.UnityEngine
{
    /// <summary>
    /// Implements a text mode command line interface based on <see cref="System.Console"/>.
    /// </summary>
    /// <remarks>
    /// Certain console features may not be available or may not work as expected in some linux terminals.
    /// Console.CursorLeft and Console.CursorTop for example may always return 0 on Linux if Mono.TermInfoDriver fails to assert that the underlying 
    /// terminal can handle the control sequence "\x1b[6n" (as is the case with Windows 10 WSL 2, thx Micro$oft) therefore this implementation cannot 
    /// safely rely on setting the cursor position directly.
    /// </remarks>
    public sealed class ConsoleCommandLineInterface: CommandLineInterface
    {
        private abstract class ConsoleDriver
        {
            public enum CursorStyle
            {
                Insert = 0,
                Overwrite = 1
            }

            public const int MaxLineSize = 32768;
            
            public readonly char[] Line = new char[MaxLineSize];
            public int Index { get; protected set; }
            public int Count { get; protected set; }

            protected abstract void Left(int n);
            protected abstract void Right(int n);

            protected abstract void Erase();

            public virtual void Reset() => (Index, Count) = (0, 0);

            public abstract void Clear();

            public abstract void Cancel();

            public void Home() => Left(Index);
            public void End() => Right(Count - Index);

            public void Left() => Left(1);
            public void Right() => Right(1);

            public void Delete() => Erase();

            public void Backspace()
            {
                Left();
                Erase();
            }

            public abstract void Insert(char c);

            public virtual void Write(char c)
            {
                if (Count < Line.Length)
                {
                    Console.Write(c);

                    Line[Index] = c;
                    if (Index == Count)
                        Count++;
                    Index++;
                }
            }

            public virtual void Write(string s)
            {
                if (string.IsNullOrEmpty(s))
                    return;

                var m = Count - Index;
                for (int i = 0, j = Index; i < m; ++i, ++j)
                {
                    var c = s[i];
                    Console.Write(c);

                    Line[j] = c;
                }

                Index += m;

                var n = Math.Min(s.Length - m, Line.Length - Count);
                for (int i = m, j = Index; i < n; ++i, ++j)
                {
                    var c = s[i];
                    Console.Write(c);
                    Line[j] = c;
                }

                Index += n;
                Count += n;
            }

            public abstract void SaveCursorStyle();
            public abstract void RestoreCursorStyle();

            public abstract void SetCursorStyle(CursorStyle style);
        }

        private sealed class WindowsConsoleDriver: ConsoleDriver
        {
            private int savedCursorSize;

            protected override void Left(int n)
            {
                if (Index > 0)
                {
                    if (n > Index)
                        n = Index;

                    var w = Console.BufferWidth;
                    var i = Console.CursorLeft - n;
                    var left = ((i % w) + w) % w;
                    
                    var p = ((((Console.CursorLeft - Index) % w) + w) % w) + Index;
                    var top = Console.CursorTop + ((p - n) / w) - (p / w); // this CANNOT be simplified to Console.CursorTop + (n/w) because of the integer divisions.
                    Console.SetCursorPosition(left, top);

                    Index -= n;
                }
            }

            protected override void Right(int n)
            {
                if (Index < Count)
                {
                    var m = Count - Index;
                    if (n > m)
                        n = m;

                    var w = Console.BufferWidth;
                    var i = Console.CursorLeft + n;
                    var left = i % w;
                    var top = Console.CursorTop + (i / w);
                    Console.SetCursorPosition(left, top);

                    Index += n;
                }
            }

            protected override void Erase()
            {
                if (Index < Count)
                {
                    var left = Console.CursorLeft;
                    var top = Console.CursorTop;
                    for (int i = Index + 1; i < Count; ++i)
                    {
                        var c = Line[i];
                        Line[i - 1] = c;
                        Console.Write(c);
                    }
                    Console.Write(' ');
                    Console.SetCursorPosition(left, top);
                    Count--;
                }
            }

            public override void Clear() => Console.Clear();

            public override void Cancel()
            {
                if (Count > 0)
                {
                    var w = Console.BufferWidth;
                    var i = Console.CursorLeft - Index;
                    var left = ((i % w) + w) % w;

                    var top = Console.CursorTop + (left / w) - ((left + Index) / w); // this CANNOT be simplified to Console.CursorTop + (-Index/w) because of the integer divisions.
                    Console.SetCursorPosition(left, top);

                    var n = Count;
                    while (n > 0)
                    {
                        Console.Write(' ');
                        n--;
                    }

                    Console.SetCursorPosition(left, top);

                    Index = 0;
                    Count = 0;
                }
            }

            public override void Insert(char c)
            {
                if (Count < Line.Length)
                {
                    Console.Write(c);

                    var left = Console.CursorLeft;
                    var top = Console.CursorTop;

                    for (int i = Index; i < Count; ++i)
                        Console.Write(Line[i]);

                    Console.SetCursorPosition(left, top);

                    for (int i = Count; i > Index; --i)
                        Line[i] = Line[i - 1];

                    Line[Index] = c;

                    Count++;
                    Index++;
                }
            }

            public override void SaveCursorStyle() => savedCursorSize = Console.CursorSize;
            public override void RestoreCursorStyle() => Console.CursorSize = savedCursorSize;

            public override void SetCursorStyle(CursorStyle style)
            {
                switch (style)
                {
                    case CursorStyle.Insert:
                        Console.CursorSize = 10;
                        break;
                    case CursorStyle.Overwrite:
                        Console.CursorSize = 100;
                        break;
                    default:
                        break;
                }
            }
        }

        private sealed class XTermConsoleDriver: ConsoleDriver
        {
            protected override void Left(int n)
            {
                if (Index > 0)
                {
                    if (n > Index)
                        n = Index;

                    Console.Write($"\x1b[{n}D");
                    Index -= n;
                }
            }

            protected override void Right(int n)
            {
                if (Index < Count)
                {
                    var m = Count - Index;
                    if (n > m)
                        n = m;

                    Console.Write($"\x1b[{n}C");
                    Index += n;
                }
            }

            protected override void Erase()
            {
                if (Index < Count)
                {
                    Console.Write("\x1b[X");

                    for (int i = Index + 1; i < Count; ++i)
                        Line[i - 1] = Line[i];
                    
                    Count--;
                }
            }

            public override void Clear() => Console.Write("\x1b[2J");

            public override void Cancel()
            {
                if (Count > 0)
                {
                    Console.Write("\x1b[H\x1b2K");

                    Index = 0;
                    Count = 0;
                }
            }

            public override void Insert(char c)
            {
                if (Count < Line.Length)
                {
                    Console.Write("\x1b@");
                    Console.Write(c);

                    for (int i = Count; i > Index; --i)
                        Line[i] = Line[i - 1];

                    Line[Index] = c;

                    Count++;
                    Index++;
                }
            }

            public override void SaveCursorStyle() => Console.Write("\x1b7");
            public override void RestoreCursorStyle() => Console.Write("\x1b8");

            public override void SetCursorStyle(CursorStyle style)
            {
                switch (style)
                {
                    case CursorStyle.Insert:
                        Console.Write("\x1b[4 q");
                        break;
                    case CursorStyle.Overwrite:
                        Console.Write("\x1b[1 q");
                        break;
                    default:
                        break;
                }
            }
        }

        private sealed class ConsoleWriter: Writer
        {
            public override void Error(char c)
            {
                var color = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                try
                {
                    Console.Error.Write(c);
                }
                finally
                {
                    Console.ForegroundColor = color;
                }                
            }

            public override void Error(string s)
            {
                var color = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                try
                {
                    Console.Error.Write(s);
                }
                finally
                {
                    Console.ForegroundColor = color;
                }
            }

            public override void Write(char c) => Console.Write(c);
            public override void Write(string s) => Console.Write(s);
        }

        protected override void OnSingletonAwaking()
        {
            base.OnSingletonAwaking();
#if UNITY_EDITOR
            if (global::UnityEngine.Application.isEditor)
                throw new InvalidOperationException(string.Format(string.Format(Resources.GetString(Strings.UnityEngine.SingletonBehaviour.CannotInstantiateInEditor), 
                    typeof(ConsoleCommandLineInterface).FullName, GetType().FullName, string.IsNullOrEmpty(name) ? string.Empty : $" ({name})")));
#endif

            if (!Application.isServerBuild)
                throw new InvalidOperationException(string.Format(string.Format(Resources.GetString(Strings.UnityEngine.SingletonBehaviour.NotInServerBuild),
                    typeof(ConsoleCommandLineInterface).FullName, GetType().FullName, string.IsNullOrEmpty(name) ? string.Empty : $" ({name})")));
        }

        private ConsoleDriver driver;

        protected override void OnSingletonAwake()
        {
            base.OnSingletonAwake();

            // User a temporary so that OnSingletonDestroy don't try to restore the console if there's an exception
            // trying to create the console driver itself.
            ConsoleDriver instance;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                instance = new WindowsConsoleDriver();
            else
                instance = new XTermConsoleDriver();

            driver = instance;

            Debug.Log($"Using {driver.GetType().FullName}");

            driver.SaveCursorStyle();
            driver.SetCursorStyle(ConsoleDriver.CursorStyle.Insert);

            using (var process = Process.GetCurrentProcess())
            {
                // When the associated process is executing on the local machine, the Process.MachineName property returns a period(".") for the machine name. 
                // You should use the Environment.MachineName property to get the correct machine name.
                // https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.process.machinename?view=netcore-2.0
                // Micro$oft is so freaking awesome...
                prompt = $"{Path.GetFileName(process.ProcessName)}[{process.Id}]@{Environment.MachineName}$ ";                
            }
        }

        protected override void OnSingletonDestroy()
        {
            base.OnSingletonDestroy();

            // The application may quit before the next frame has a chance to call invoke OnProcessed
            // or the console may be destroyed due to a critial exception. Anyway make sure the cursor 
            // is restored. In windows consoles the cursor seems to be automatically restored after the 
            // application quits but on linux the cursor remains in its latest state.
            driver?.RestoreCursorStyle();
        }

        protected override void OnStarted() => Console.Write(prompt);
        protected override void OnProcessing() => Console.WriteLine();
        protected override void OnProcessed() => Console.Write(prompt);

        protected override void Clear()
        {
            driver.Clear();
            driver.Reset();
            AutoCompleteReset();
        }

        protected override Writer StartWriter() => new ConsoleWriter();

        protected override bool TryRead(out string value)
        {
            while (TryReadKey(out ConsoleKeyInfo keyInfo, true))
            {
                if (keyInfo.Key == ConsoleKey.Enter)
                {
                    value = driver.Count > 0 ? new string(driver.Line, 0, driver.Count) : "";
                    driver.Reset();
                    AutoCompleteReset();
                    return true;
                }

                Handle(keyInfo);
            }

            value = default;
            return false;
        }

        private static bool TryReadKey(out ConsoleKeyInfo keyInfo, bool intercept = false)
        {
            if (Console.KeyAvailable)
            {
                keyInfo = Console.ReadKey(intercept);
                return true;
            }

            keyInfo = default;
            return false;
        }

        #region Console Input handling

        private string prompt;
        
        /// <summary>
        /// True if input in is overwrite mode; otherwise input is in insert mode.
        /// </summary>
        private bool overwrite;
                
        private void Handle(ConsoleKeyInfo keyInfo)
        {
            // If in auto complete mode and Tab wasn't pressed
            if (hasAutoCompleteValues && keyInfo.Key != ConsoleKey.Tab)
                AutoCompleteReset();

            switch (keyInfo.Key)
            {
                case ConsoleKey.Insert:
                    overwrite = !overwrite;
                    if (overwrite)
                        driver.SetCursorStyle(ConsoleDriver.CursorStyle.Overwrite);
                    else
                        driver.SetCursorStyle(ConsoleDriver.CursorStyle.Insert);
                    return;
                case ConsoleKey.Home:
                    Home();
                    return;
                case ConsoleKey.End:
                    End();
                    return;
                case ConsoleKey.LeftArrow:
                    Left();
                    return;
                case ConsoleKey.RightArrow:
                    Right();
                    return;
                case ConsoleKey.UpArrow:
                    PreviousHistory();
                    return;
                case ConsoleKey.DownArrow:
                    NextHistory();
                    return;
                case ConsoleKey.Backspace:
                    Backspace();
                    return;
                case ConsoleKey.Delete:
                    Delete();
                    return;
                case ConsoleKey.Escape:
                    Escape();
                    History.Reset();
                    return;
                case ConsoleKey.A:
                    if (keyInfo.Modifiers == ConsoleModifiers.Control)
                    {
                        Home();
                        return;
                    }
                    break;
                case ConsoleKey.B:
                    if (keyInfo.Modifiers == ConsoleModifiers.Control)
                    {
                        Left();
                        return;
                    }
                    break;
                case ConsoleKey.C:
                    if (keyInfo.Modifiers == ConsoleModifiers.Control)
                    {
                        Application.Quit();
                        return;
                    }
                    break;
                case ConsoleKey.D:
                    if (keyInfo.Modifiers == ConsoleModifiers.Control)
                    {
                        Delete();
                        return;
                    }
                    break;
                case ConsoleKey.E:
                    if (keyInfo.Modifiers == ConsoleModifiers.Control)
                    {
                        End();
                        return;
                    }
                    break;
                case ConsoleKey.F:
                    if (keyInfo.Modifiers == ConsoleModifiers.Control)
                    {
                        Right();
                        return;
                    }
                    break;
                case ConsoleKey.H:
                    if (keyInfo.Modifiers == ConsoleModifiers.Control)
                    {
                        Backspace();
                        return;
                    }
                    break;
                case ConsoleKey.L:
                    if (keyInfo.Modifiers == ConsoleModifiers.Control)
                    {
                        Escape();
                        History.Reset();
                        return;
                    }
                    break;
                case ConsoleKey.N:
                    if (keyInfo.Modifiers == ConsoleModifiers.Control)
                    {
                        NextHistory();
                        return;
                    }
                    break;
                case ConsoleKey.P:
                    if (keyInfo.Modifiers == ConsoleModifiers.Control)
                    {
                        PreviousHistory();
                        return;
                    }
                    break;
                case ConsoleKey.Tab:
                    if (keyInfo.Modifiers == 0)
                    {
                        if (hasAutoCompleteValues)
                        {
                            AutoCompleteNext();
                        }
                        else
                        {
                            if (driver.Index > 0 && driver.Index < driver.Count)
                                return;

                            var prefix = new string(driver.Line, 0, driver.Count);
                            Shell.FindCommandNamesStartingWith(prefix, out autoCompleteValues);
                            if (autoCompleteValues.Count == 0)
                                return;

                            AutoCompleteStart();
                        }
                        return;
                    }
                    else if (keyInfo.Modifiers == ConsoleModifiers.Shift)
                    {
                        if (hasAutoCompleteValues)
                            AutoCompletePrevious();

                        return;
                    }
                    break;
                default:
                    break;
            }

            if (overwrite)
                Write(keyInfo.KeyChar);
            else
                Insert(keyInfo.KeyChar);
        }
    
        private void PreviousHistory()
        {
            if (!History.IsFirst)
            {
                driver.Cancel();
                driver.Write(History.GoToPrevious());
            }
        }

        private void NextHistory()
        {
            if (!History.IsLast)
            {
                driver.Cancel();
                driver.Write(History.GoToNext());
            }
        }

        private void Left() => driver.Left();

        private void Right() => driver.Right();

        private void Home() => driver.Home();

        private void End() => driver.End();

        private void Write(char c) => driver.Write(c);

        private void Insert(char c) => driver.Insert(c);

        private void Escape() => driver.Cancel();

        private void Backspace() => driver.Backspace();

        private void Delete() => driver.Delete();

        #region Auto Complete

        private ArraySegment<string> autoCompleteValues;
        private int autoCompleteIndex;
        private bool hasAutoCompleteValues => autoCompleteValues.Count > 0;

        private void AutoComplete()
        {
            driver.Cancel();
            driver.Write(autoCompleteValues.Array[autoCompleteValues.Offset + autoCompleteIndex]);
        }

        private void AutoCompleteStart()
        {
            autoCompleteIndex = 0;
            AutoComplete();
        }

        private void AutoCompleteNext()
        {
            autoCompleteIndex = (autoCompleteIndex + 1) % autoCompleteValues.Count;
            AutoComplete();
        }

        private void AutoCompletePrevious()
        {
            autoCompleteIndex = (autoCompleteIndex - 1 + autoCompleteValues.Count) % autoCompleteValues.Count;
            AutoComplete();
        }

        private void AutoCompleteReset()
        {
            autoCompleteValues = default;
            autoCompleteIndex = 0;
        }

        #endregion

        #endregion
    }
}
