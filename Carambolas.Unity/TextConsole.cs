using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

using UnityEngine;

using Resources = Carambolas.Internal.Resources;
using Strings = Carambolas.Internal.Strings;

namespace Carambolas.UnityEngine
{
    public sealed class TextConsole: Console
    {
        protected override void OnSingletonAwaking()
        {
            base.OnSingletonAwaking();
#if UNITY_EDITOR
            if (Application.isEditor)
                throw new InvalidOperationException(string.Format(string.Format(Resources.GetString(Strings.UnityEngine.SingletonBehaviour.CannotInstantiateInEditor), 
                    typeof(TextConsole).FullName, GetType().FullName, string.IsNullOrEmpty(name) ? string.Empty : $" ({name})")));
#endif

            if (!Application.isServerBuild)
                throw new InvalidOperationException(string.Format(string.Format(Resources.GetString(Strings.UnityEngine.SingletonBehaviour.NotInServerBuild),
                    typeof(TextConsole).FullName, GetType().FullName, string.IsNullOrEmpty(name) ? string.Empty : $" ({name})")));
        }

        protected override void OnSingletonAwake()
        {
            base.OnSingletonAwake();
            text = new StringBuilder();
        }

        protected override bool TryReadLine(out string value)
        {
            while (TryReadKey(out ConsoleKeyInfo keyInfo, true))
            {
                if (keyInfo.Key == ConsoleKey.Enter)
                {
                    value = text.ToString();
                    text.Clear();

                    cursorPos = 0;
                    cursorLimit = 0;
                    ResetAutoComplete();

                    return true;
                }

                Handle(keyInfo);
            }

            value = default;
            return false;
        }

        private static bool TryReadKey(out ConsoleKeyInfo keyInfo, bool intercept = false)
        {
            if (System.Console.KeyAvailable)
            {
                keyInfo = System.Console.ReadKey(intercept);
                return true;
            }

            keyInfo = default;
            return false;
        }

        #region Console Input handling

        private int cursorPos;
        private int cursorLimit;

        private ArraySegment<string> completions;
        private int completionsIndex;

        private bool isStartOfLine => cursorPos == 0;
        private bool isEndOfLine => cursorPos == cursorLimit;
        private bool isStartOfBuffer => System.Console.CursorLeft == 0;
        private bool isEndOfBuffer => System.Console.CursorLeft == System.Console.BufferWidth - 1;
        private bool isAutoCompleting => completions.Count > 0;

        private StringBuilder text;

        private void Handle(ConsoleKeyInfo keyInfo)
        {
            // If in auto complete mode and Tab wasn't pressed
            if (isAutoCompleting && keyInfo.Key != ConsoleKey.Tab)
                ResetAutoComplete();

            switch (keyInfo.Key)
            {
                case ConsoleKey.Home:
                    MoveCursorHome();
                    return;
                case ConsoleKey.End:
                    MoveCursorEnd();
                    return;
                case ConsoleKey.LeftArrow:
                    MoveCursorLeft();
                    return;
                case ConsoleKey.RightArrow:
                    MoveCursorRight();
                    return;
                case ConsoleKey.UpArrow:
                    WritePreviousHistory();
                    return;
                case ConsoleKey.DownArrow:
                    WriteNextHistory();
                    return;
                case ConsoleKey.Backspace:
                    if (!isStartOfLine)
                        Backspace();
                    return;
                case ConsoleKey.Delete:
                    Delete();
                    return;
                case ConsoleKey.Escape:
                    ClearLine();
                    return;
                case ConsoleKey.A:
                    if (keyInfo.Modifiers == ConsoleModifiers.Control)
                    {
                        MoveCursorHome();
                        return;
                    }
                    break;
                case ConsoleKey.B:
                    if (keyInfo.Modifiers == ConsoleModifiers.Control)
                    {
                        MoveCursorLeft();
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
                        MoveCursorEnd();
                        return;
                    }
                    break;
                case ConsoleKey.F:
                    if (keyInfo.Modifiers == ConsoleModifiers.Control)
                    {
                        MoveCursorRight();
                        return;
                    }
                    break;
                case ConsoleKey.H:
                    if (keyInfo.Modifiers == ConsoleModifiers.Control)
                    {
                        if (!isStartOfLine)
                            Backspace();
                        return;
                    }
                    break;
                case ConsoleKey.L:
                    if (keyInfo.Modifiers == ConsoleModifiers.Control)
                    {
                        ClearLine();
                        return;
                    }
                    break;
                case ConsoleKey.K:
                    if (keyInfo.Modifiers == ConsoleModifiers.Control)
                    {
                        int pos = cursorPos;
                        MoveCursorEnd();
                        while (cursorPos > pos)
                            Backspace();

                        return;
                    }
                    break;
                case ConsoleKey.N:
                    if (keyInfo.Modifiers == ConsoleModifiers.Control)
                    {
                        WriteNextHistory();
                        return;
                    }
                    break;
                case ConsoleKey.P:
                    if (keyInfo.Modifiers == ConsoleModifiers.Control)
                    {
                        WritePreviousHistory();
                        return;
                    }
                    break;
                case ConsoleKey.T:
                    if (keyInfo.Modifiers == ConsoleModifiers.Control)
                    {
                        TransposeChars();
                        return;
                    }
                    break;
                case ConsoleKey.U:
                    if (keyInfo.Modifiers == ConsoleModifiers.Control)
                    {
                        if (!isStartOfLine)
                            Backspace();
                        return;
                    }
                    break;
                case ConsoleKey.W:
                    if (keyInfo.Modifiers == ConsoleModifiers.Control)
                    {
                        while (!isStartOfLine && !char.IsWhiteSpace(text[cursorPos - 1]))
                            Backspace();
                        return;
                    }
                    break;
                case ConsoleKey.Tab:
                    if (keyInfo.Modifiers == 0)
                    {
                        if (isAutoCompleting)
                        {
                            WriteNextAutoComplete();
                        }
                        else
                        {
                            if (!isEndOfLine)
                                return;

                            var prefix = text.ToString();
                            AutoComplete(prefix, out completions);
                            if (completions.Count == 0)
                                return;

                            StartAutoComplete();
                        }
                        return;
                    }
                    else if (keyInfo.Modifiers == ConsoleModifiers.Shift)
                    {
                        if (isAutoCompleting)
                            WritePreviousAutoComplete();

                        return;
                    }
                    break;
                default:
                    break;
            }

            WriteChar(keyInfo.KeyChar);
        }

        private void MoveCursorLeft()
        {
            if (isStartOfLine)
                return;

            if (isStartOfBuffer)
                System.Console.SetCursorPosition(System.Console.BufferWidth - 1, System.Console.CursorTop - 1);
            else
                System.Console.SetCursorPosition(System.Console.CursorLeft - 1, System.Console.CursorTop);

            cursorPos--;
        }

        private void MoveCursorHome()
        {
            while (!isStartOfLine)
                MoveCursorLeft();
        }

        private void MoveCursorRight()
        {
            if (isEndOfLine)
                return;

            if (isEndOfBuffer)
                System.Console.SetCursorPosition(0, System.Console.CursorTop + 1);
            else
                System.Console.SetCursorPosition(System.Console.CursorLeft + 1, System.Console.CursorTop);

            cursorPos++;
        }

        private void MoveCursorEnd()
        {
            while (!isEndOfLine)
                MoveCursorRight();
        }

        private void ClearLine()
        {
            MoveCursorEnd();
            while (!isStartOfLine)
                Backspace();
        }

        private void WriteNewString(string s)
        {
            ClearLine();
            WriteString(s);
        }

        private void WriteString(string s)
        {
            if (!string.IsNullOrEmpty(s))
                foreach (var character in s)
                    WriteChar(character);
        }

        private void WriteChar(char c)
        {
            if (isEndOfLine)
            {
                text.Append(c);
                System.Console.Write(c);
                cursorPos++;
            }
            else
            {
                var left = System.Console.CursorLeft;
                var top = System.Console.CursorTop;
                var str = text.ToString().Substring(cursorPos);
                text.Insert(cursorPos, c);
                System.Console.Write(c);
                System.Console.Write(str);
                System.Console.SetCursorPosition(left, top);
                MoveCursorRight();
            }

            cursorLimit++;
        }

        private void Backspace()
        {
            Debug.Assert(!isStartOfLine);

            MoveCursorLeft();
            var index = cursorPos;
            text.Remove(index, 1);
            var replacement = text.ToString().Substring(index);
            var left = System.Console.CursorLeft;
            var top = System.Console.CursorTop;
            System.Console.Write(string.Format("{0} ", replacement));
            System.Console.SetCursorPosition(left, top);
            cursorLimit--;
        }

        private void Delete()
        {
            if (isEndOfLine)
                return;

            var index = cursorPos;
            text.Remove(index, 1);
            var replacement = text.ToString().Substring(index);
            var left = System.Console.CursorLeft;
            var top = System.Console.CursorTop;
            System.Console.Write(string.Format("{0} ", replacement));
            System.Console.SetCursorPosition(left, top);
            cursorLimit--;
        }

        private void TransposeChars()
        {
            if (isStartOfLine)
                return;

            var firstIdx = isEndOfLine ? cursorPos - 2 : cursorPos - 1;
            var secondIdx = isEndOfLine ? cursorPos - 1 : cursorPos;

            var secondChar = text[secondIdx];
            text[secondIdx] = text[firstIdx];
            text[firstIdx] = secondChar;

            var almostEndOfLine = (cursorLimit - cursorPos) == 1;
            var left = almostEndOfLine ? System.Console.CursorLeft + 1 : System.Console.CursorLeft;
            var cursorPosition = almostEndOfLine ? cursorPos + 1 : cursorPos;

            WriteNewString(text.ToString());

            System.Console.SetCursorPosition(left, System.Console.CursorTop);
            cursorPos = cursorPosition;

            MoveCursorRight();
        }

        private void StartAutoComplete()
        {
            while (!isStartOfLine)
                Backspace();

            completionsIndex = 0;

            WriteString(completions.Array[completions.Offset + completionsIndex]);
        }

        private void WriteNextAutoComplete()
        {
            while (!isStartOfLine)
                Backspace();

            completionsIndex++;

            if (completionsIndex == completions.Count)
                completionsIndex = 0;

            WriteString(completions.Array[completions.Offset + completionsIndex]);
        }

        private void WritePreviousAutoComplete()
        {
            while (!isStartOfLine)
                Backspace();

            completionsIndex--;

            if (completionsIndex == -1)
                completionsIndex = completions.Count - 1;

            WriteString(completions.Array[completions.Offset + completionsIndex]);
        }

        private void WritePreviousHistory()
        {
            if (!History.IsFirst)
                WriteNewString(History.GoToPrevious());
        }

        private void WriteNextHistory()
        {
            if (!History.IsLast)
                WriteNewString(History.GoToNext());
        }

        private void ResetAutoComplete()
        {
            completions = default;
            completionsIndex = 0;
        }

        #endregion
    }
}
