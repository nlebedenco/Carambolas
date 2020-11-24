using System;
using System.Diagnostics;
using System.Threading;

using UnityEngine;
using Carambolas.UI;
using System.IO;
using System.Collections.Generic;

using TextWriter = Carambolas.IO.TextWriter;

namespace Carambolas.UnityEngine
{
    public sealed class CommandLineInterface: Console
    {
        private sealed class ReplxxOutputWriter: TextWriter
        {
            private readonly Replxx replxx;

            public ReplxxOutputWriter(Replxx replxx) => this.replxx = replxx;

            public override void Write(char value) => replxx.Write(value);
            public override void Write(string value) => replxx.Write(value);
            public override void Write(char[] buffer) => replxx.Write(buffer);
            public override void Write(char[] buffer, int index, int count) => replxx.Write(buffer, index, count);

            public override void WriteLine() => replxx.Write('\n');

            public override void WriteLine(char value)
            {
                replxx.Write(value);
                replxx.Write('\n');
            }

            public override void WriteLine(string value)
            {
                replxx.Write(value);
                replxx.Write('\n');
            }

            public override void WriteLine(char[] buffer)
            {
                replxx.Write(buffer);
                replxx.Write('\n');
            }

            public override void WriteLine(char[] buffer, int index, int count)
            {
                replxx.Write(buffer, index, count);
                replxx.Write('\n');
            }
        }

        private sealed class ReplxxErrorWriter: TextWriter
        {
            private readonly Replxx replxx;

            public ReplxxErrorWriter(Replxx replxx) => this.replxx = replxx;

            public override void Write(char value)
            {
                replxx.Write("\x1b[1;31m");
                replxx.Write(value);
                replxx.Write("\x1b[0m");
            }

            public override void Write(string value)
            {
                replxx.Write("\x1b[1;31m");
                replxx.Write(value);
                replxx.Write("\x1b[0m");
            }

            public override void Write(char[] buffer)
            {
                replxx.Write("\x1b[1;31m");
                replxx.Write(buffer);
                replxx.Write("\x1b[0m");
            }

            public override void Write(char[] buffer, int index, int count)
            {
                replxx.Write("\x1b[1;31m");
                replxx.Write(buffer, index, count);
                replxx.Write("\x1b[0m");                
            }

            public override void WriteLine() => replxx.Write('\n');

            public override void WriteLine(char value)
            {
                Write(value);
                replxx.Write('\n');
            }

            public override void WriteLine(string value)
            {
                Write(value);
                replxx.Write('\n');
            }

            public override void WriteLine(char[] buffer)
            {
                Write(buffer);
                replxx.Write('\n');
            }

            public override void WriteLine(char[] buffer, int index, int count)
            {
                Write(buffer, index, count);
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
        private TextWriter error;
        private TextWriter output;

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
                foreach(var info in Commands.Values)
                    completions.Add(info.Name);
            }
            else if (contextLength > 0)
            {
                var startIndex = input.Length - contextLength;
                if (char.IsLetter(input[startIndex]))
                {
                    var prefix = startIndex == 0 ? input : input.Substring(startIndex, contextLength);
                    FindCommands(prefix, out IReadOnlyList<CommandInfo> list, out int index, out int count);
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
                FindCommands(prefix, out IReadOnlyList<CommandInfo> list, out int index, out int count);
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

        protected override TextWriter GetErrorWriter() => error;

        protected override TextWriter GetOutputWriter() => output;

        protected override bool TryReadLine(out string value)
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

            error = new ReplxxErrorWriter(replxx);
            output = new ReplxxOutputWriter(replxx);

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
