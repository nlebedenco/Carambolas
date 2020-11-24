using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Log = Carambolas.Internal.Log;
using ILogHandler = Carambolas.Internal.ILogHandler;

namespace Carambolas.UI.Tests.Application
{
    class CLI: IDisposable
    {
        private readonly string[] examples = new string[]
        {
            "db",
            "hello",
            "hallo",
            "hans",
            "hansekogge",
            "seamann",
            "quetzalcoatl",
            "quit",
            "power",
            "app",
            "app.info",
            "app.version",
            "app.size",
            "app.model",
            "clear"
        };

        private string prompt = "\x1b[1;32mreplxx\x1b[0m> ";

        private bool installCompletionCallback = true;
        private bool installHighlightCallback = true;
        private bool installHintCallback = true;

        private readonly Replxx replxx = new Replxx();

        private void ParseParameters()
        {
            if (Program.CommandLineArguments.TryGetValue("b", out string arg))
                replxx.SetBeepOnAmbiguousCompletion(Convert.ToBoolean(arg));

            if (Program.CommandLineArguments.TryGetValue("c", out arg))
                replxx.SetCompletionCountCutOff(Convert.ToInt32(arg));

            if (Program.CommandLineArguments.TryGetValue("e", out arg))
                replxx.SetEmptyCompletion(Convert.ToBoolean(arg));

            if (Program.CommandLineArguments.TryGetValue("d", out arg))
                replxx.SetDoubleTabCompletion(Convert.ToBoolean(arg));

            if (Program.CommandLineArguments.TryGetValue("h", out arg))
                replxx.SetMaxHintRows(Convert.ToInt32(arg));

            if (Program.CommandLineArguments.TryGetValue("s", out arg))
                replxx.SetMaxHistorySize(Convert.ToInt32(arg));

            if (Program.CommandLineArguments.TryGetValue("i", out arg))
                replxx.SetPreloadBuffer(arg.Replace('~', '\n'));

            if (Program.CommandLineArguments.TryGetValue("I", out arg))
                replxx.SetImmediateCompletion(Convert.ToBoolean(arg));

            if (Program.CommandLineArguments.TryGetValue("u", out arg))
                replxx.SetUniqueHistory(Convert.ToBoolean(arg));

            if (Program.CommandLineArguments.TryGetValue("w", out arg))
                replxx.SetWordBreakCharacters(arg);

            if (Program.CommandLineArguments.TryGetValue("m", out arg))
                replxx.SetNoColor(Convert.ToBoolean(arg));

            if (Program.CommandLineArguments.Contains("B"))
                replxx.EnableBracketedPaste();

            if (Program.CommandLineArguments.TryGetValue("p", out arg))
                prompt = arg.Replace('~', '\n');

            if (Program.CommandLineArguments.Contains("C"))
                installCompletionCallback = false;

            if (Program.CommandLineArguments.Contains("S"))
                installHighlightCallback = false;

            if (Program.CommandLineArguments.Contains("N"))
                installHintCallback = false;
        }

        private void OnCompletionRequested(string input, ref int length, in Replxx.Completions completions)
        {
            length = input.Length;
            foreach (var s in examples.Where(x => x.StartsWith(input)))
                completions.Add(s);
        }

        private void OnHighlightRequested(string input, Replxx.Color[] colors)
        {
            if (!string.IsNullOrEmpty(input))
            {
                int n = Math.Min(input.Length, colors.Length);
                for (int i = 0; i < n; ++i)
                    if (char.IsDigit(input[i]))
                        colors[i] = Replxx.Color.BrightCyan;

                if (n > 0)
                {
                    switch (input[n - 1])
                    {
                        case '(':
                            replxx.EmulateKeyPress(')');
                            replxx.EmulateKeyPress(Replxx.Key.Left);
                            break;
                        case '[':
                            replxx.EmulateKeyPress(']');
                            replxx.EmulateKeyPress(Replxx.Key.Left);
                            break;
                        case '{':
                            replxx.EmulateKeyPress('}');
                            replxx.EmulateKeyPress(Replxx.Key.Left);
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private void OnHintRequested(string input, ref int length, in Replxx.Hints hints)
        {
            if (!string.IsNullOrEmpty(input))
            {
                length = input.Length;
                foreach (var s in examples.Where(x => x.StartsWith(input)))
                    hints.Add(s);
            }
        }

        public int Run()
        {
            ParseParameters();

            if (installCompletionCallback)
                replxx.CompletionRequested = OnCompletionRequested;

            if (installHighlightCallback)
                replxx.HighlightRequested = OnHighlightRequested;

            if (installHintCallback)
                replxx.HintRequested = OnHintRequested;

            replxx.SetWordBreakCharacters(" \t\v\f\a\b\r\n");

            while (true)
            {
                string input;
                do
                    input = replxx.Read(prompt);
                while (string.IsNullOrEmpty(input));

                if (input == "exit")
                    break;

                if (input == "clear")
                    replxx.Clear();

                replxx.AddToHistory(input);
            }

            return 0;
        }

        public void Dispose()
        {
            replxx.Dispose();
        }
    }

    class Program
    {
        private class ConsoleLogHandler: ILogHandler
        {
            private static string timestamp => DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture);

            public void Error(string s) => Console.WriteLine($"{timestamp} [ERROR] {s}");

            public void Exception(Exception e) => Console.WriteLine($"{timestamp} [EXCEPTION] {e}");

            public void Info(string s) => Console.WriteLine($"{timestamp} {s}");

            public void Warn(string s) => Console.WriteLine($"{timestamp} [WARN] {s}");
        }

        public static CommandLineArguments CommandLineArguments = new CommandLineArguments();

        private static int Main(string[] args)
        {
            using (var cli = new CLI())
            {
                return cli.Run();
            }
        }
    }
}
