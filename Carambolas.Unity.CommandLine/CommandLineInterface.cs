using System;
using System.Diagnostics;
using System.Threading;

using UnityEngine;
using Carambolas.UI;

namespace Carambolas.UnityEngine
{
    public sealed class ComandLineInterface: Repl
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
            if (Application.isServerBuild && !Application.commandLineArguments.Contains("noconsole"))
                ComponentUtility.Create<ComandLineInterface>();
        }

        private string prompt;
        private Replxx replxx;
        private string line;
        private AutoResetEvent processed;
        private bool terminated;
        private Thread thread;

        #region Replxx thread

        private void Reader()
        {
            while (!terminated)
            {
                do
                {
                    line = replxx.Read(prompt);
                }
                while (string.IsNullOrEmpty(line) && !terminated);

                processed.WaitOne();                
            }
        }

        private void OnCompletionRequested(string input, ref int length, in Replxx.Completions completions)
        {
            if (string.IsNullOrEmpty(input))
            {
                GetCommandNames(out ArraySegment<string> commands);
                if (commands.Array != null)
                    for (int i = commands.Offset; i < commands.Count; ++i)
                        completions.Add(commands.Array[commands.Offset + i]);
            }
            else if (length > 0)
            {
                var n = input.Length;
                var startIndex = n - length;
                if (char.IsLetter(input[startIndex]))
                {
                    var prefix = startIndex == 0 ? input : input.Substring(startIndex, length);
                    GetCommandNames(prefix, out ArraySegment<string> commands);
                    if (commands.Array != null)
                        for (int i = 0; i < commands.Count; ++i)
                            completions.Add(commands.Array[commands.Offset + i]);
                }
            }
        }

        private void OnHintRequested(string input, ref int length, in Replxx.Hints hints)
        {
            if (!string.IsNullOrEmpty(input) && length > 0)
            {
                var prefix = input.Substring(input.Length - length, length);
                GetCommandNames(prefix, out ArraySegment<string> commands);
                if (commands.Array != null)
                    for (int i = 0; i < commands.Count; ++i)
                        hints.Add(commands.Array[commands.Offset + i]);
            }
        }

        #endregion

        protected override void Clear() => replxx?.Clear();

        protected override Writer GetWriter() => new ReplxxWriter(replxx);

        protected override bool TryRead(out string value)
        {
            var s = line;
            if (string.IsNullOrEmpty(s))
            {
                value = default;
                return false;
            }

            line = default;
            replxx.AddToHistory(s);
            value = s;
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
