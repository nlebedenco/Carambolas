using System;
using System.Diagnostics;
using System.Threading;

using UnityEngine;
using Carambolas.UI;
using System.IO;
using System.Collections.Generic;

namespace Carambolas.UnityEngine
{
    public sealed class CommandLineInterface: Repl
    {
        private class ReplxxWriter: Writer
        {
            private readonly Replxx replxx;

            public ReplxxWriter(Replxx replxx) => this.replxx = replxx;

            public override void Error(char c)
            {
                replxx.Write("\x1b[1;31m");
                replxx.Write(c);
                replxx.Write("\x1b[0m");
            }

            public override void Error(string s)
            {
                replxx.Write("\x1b[1;31m");
                replxx.Write(s);
                replxx.Write("\x1b[0m");
            }

            public override void ErrorLine(string s)
            {
                replxx.Write("\x1b[1;31m");
                replxx.Write(s);
                replxx.Write("\x1b[0m\n");
            }

            public override void Write(char c)
            {
                replxx.Write(c);
            }

            public override void Write(string s)
            {
                replxx.Write(s);
            }

            public override void WriteLine(string s)
            {
                replxx.Write(s);
                replxx.Write('\n');
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoad()
        {
            if (Application.isInteractiveServerBuild)
                ComponentUtility.Create<CommandLineInterface>();
        }

        private string prompt;
        private Replxx replxx;
               
        private volatile bool terminated;
        private volatile string line;
        private AutoResetEvent processed;
        private Thread thread;

        #region Replxx thread

        private void Reader()
        {
            while (!terminated)
            {
                var s = replxx.Read(prompt);
                if (string.IsNullOrEmpty(s))
                    continue;

                line = s;
                processed.WaitOne();                
            }
        }

        private void OnCompletionRequested(string input, ref int contextLength, in Replxx.Completions completions)
        {
            if (string.IsNullOrEmpty(input))
            {
                foreach(var info in Commands)
                    completions.Add(info.Name);
            }
            else if (contextLength > 0)
            {
                var startIndex = input.Length - contextLength;
                if (char.IsLetter(input[startIndex]))
                {
                    var prefix = startIndex == 0 ? input : input.Substring(startIndex, contextLength);
                    FindCommands(prefix, out IReadOnlyList<Shell.CommandInfo> list, out int index, out int count);
                    if (count > 0)
                    {
                        var n = index + count;
                        for (int i = index; i < n; ++i)
                            completions.Add(list[i].Name);
                    }
                }
            }
        }

        private void OnHintRequested(string input, ref int contextLength, in Replxx.Hints hints)
        {
            if (!string.IsNullOrEmpty(input) && contextLength > 0)
            {
                var startIndex = input.Length - contextLength;
                var prefix = startIndex == 0 ? input : input.Substring(startIndex, contextLength);
                FindCommands(prefix, out IReadOnlyList<Shell.CommandInfo> list, out int index, out int count);
                if (count > 0)
                {
                    var n = index + count;
                    for (int i = index; i < n; ++i)
                        hints.Add(list[i].Name);
                }
            }
        }

        #endregion

        protected override void Clear() => replxx?.Clear();

        protected override Writer GetWriter() => new ReplxxWriter(replxx);

        protected override bool TryRead(out string value)
        {
            value = line;
            if (string.IsNullOrEmpty(value))
                return false;

            line = default;
            replxx.AddToHistory(value);
            return true;
        }

        protected override void OnProcessed()
        {
            base.OnProcessed();
            processed.Set();
        }

        protected override void OnSingletonAwake()
        {
            base.OnSingletonAwake();

            using (var process = Process.GetCurrentProcess())
            {                
                prompt = $"\x1b[1;32m{Application.productName}@{Environment.MachineName}[{process.Id}]\x1b[0m> ";
            }
                
            replxx = new Replxx();
            replxx.SetWordBreakCharacters(" \t\v\f\a\b\r\n");
            replxx.SetMaxHistorySize(512);
            replxx.CompletionRequested = OnCompletionRequested;
            replxx.HintRequested = OnHintRequested;

            processed = new AutoResetEvent(false);
            thread = new Thread(Reader) { Name = $"CLI", IsBackground = true };
            thread.Start();
        }

        protected override void OnSingletonDestroy()
        {
            terminated = true;
            replxx.EmulateKeyPress(Replxx.Key.Control('D'));
            processed.Set();            
            thread.Wait();
            thread = null;

            processed.Dispose();
            processed = null;

            replxx.Dispose();
            replxx = null;

            base.OnSingletonDestroy();
        }
    }
}
