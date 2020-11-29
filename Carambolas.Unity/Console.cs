using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Runtime.CompilerServices;

using UnityEngine;
using UnityEngine.Scripting;

using Carambolas.Text;
using StringBuilder = Carambolas.Text.StringBuilder;

using Carambolas.IO;
using TextWriter = Carambolas.IO.TextWriter;
using StringWriter = Carambolas.IO.StringWriter;

// TODO: Change exception string messages for internal resource strings
// TODO: Check all array boundaries checks for when start = length and count = 0 as this may or may not be an issue

using Resources = Carambolas.Internal.Resources;
using Strings = Carambolas.Internal.Strings;

namespace Carambolas.UnityEngine
{
    using System.Diagnostics;
    using Carambolas.Collections.Generic;

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class CommandAttribute: PreserveAttribute
    {
        public string Name;
        public string Description;
        public string Help;

        private static bool IsValidNameCharacter(char c) => char.IsLetterOrDigit(c) || "_.-".IndexOf(c) >= 0;

        public static bool IsValidName(string name)
        {
            foreach (var c in name)
                if (!IsValidNameCharacter(c))
                    return false;

            return true;
        }

        public static bool IsValidName(string name, int start, int count)
        {
            if (start < 0
             || start > name.Length
             || count <= 0
             || start > name.Length - count)
                return false;

            var n = start + count;
            for (int i = start; i < n; ++i)
                if (!IsValidNameCharacter(name[i]))
                    return false;

            return true;
        }
    }

    public delegate object CommandHandler(TextWriter writer, IReadOnlyList<string> args);

    public readonly struct CommandInfo
    {
        public readonly string Name;
        public readonly string Description;
        public readonly string Help;
        public readonly CommandHandler Handler;

        internal CommandInfo(string name, string description, string help, CommandHandler handler) => (Name, Description, Help, Handler) = (name, description, help, handler);
    }

    [DisallowMultipleComponent]
    public abstract class Console: SingletonBehaviour<Console>
    {
        public static IDictionary<string, CommandInfo> Commands => Shell.Commands;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FindCommands(string prefix, out IReadOnlyList<CommandInfo> list, out int index, out int count) => Shell.FindCommands(prefix, out list, out index, out count);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Register(Assembly assembly) => Shell.Register(assembly);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LoadCommands() => Shell.LoadCommands();

        private static class Shell
        {
            private static readonly HashSet<Assembly> assemblies = new HashSet<Assembly> { typeof(Console).Assembly };

            public static readonly SortedList<string, CommandInfo> Commands = new SortedList<string, CommandInfo>();

            private static readonly MethodInfo BoxedFuncMaker = typeof(Console).GetMethod("GenericMakeMethod", BindingFlags.NonPublic | BindingFlags.Static);
            private static readonly MethodInfo BoxedFuncWithoutArgsMaker = typeof(Console).GetMethod("GenericMakeMethodWithoutArgs", BindingFlags.NonPublic | BindingFlags.Static);
            private static readonly MethodInfo BoxedFuncWithoutWriterMaker = typeof(Console).GetMethod("GenericMakeMethodWithoutWriter", BindingFlags.NonPublic | BindingFlags.Static);
            private static readonly MethodInfo BoxedFuncWithoutWriterAndArgsMaker = typeof(Console).GetMethod("GenericMakeMethodWithoutWriterAndArgs", BindingFlags.NonPublic | BindingFlags.Static);

            private static CommandHandler GenericMakeMethod<U>(MethodInfo method) => (Delegate.CreateDelegate(typeof(Func<TextWriter, IReadOnlyList<string>, U>), method, false) is Func<TextWriter, IReadOnlyList<string>, U> func) ? (writer, args) => func(writer, args) : (CommandHandler)null;

            private static CommandHandler GenericMakeMethodWithoutArgs<U>(MethodInfo method) => (Delegate.CreateDelegate(typeof(Func<TextWriter, U>), method, false) is Func<TextWriter, U> func) ? (writer, args) => func(writer) : (CommandHandler)null;

            private static CommandHandler GenericMakeMethodWithoutWriter<U>(MethodInfo method) => (Delegate.CreateDelegate(typeof(Func<IReadOnlyList<string>, U>), method, false) is Func<IReadOnlyList<string>, U> func) ? (writer, args) => func(args) : (CommandHandler)null;

            private static CommandHandler GenericMakeMethodWithoutWriterAndArgs<U>(MethodInfo method) => (Delegate.CreateDelegate(typeof(Func<U>), method, false) is Func<U> func) ? (writer, args) => func() : (CommandHandler)null;

            private static readonly object[] genericMakeArgs = new object[1];

            private static CommandHandler MakeDelegate(MethodInfo method)
            {
                if (Delegate.CreateDelegate(typeof(CommandHandler), method, false) is CommandHandler func)
                    return func;

                if (Delegate.CreateDelegate(typeof(Action<TextWriter, IReadOnlyList<string>>), method, false) is Action<TextWriter, IReadOnlyList<string>> action)
                    return (writer, args) => { action(writer, args); return null; };

                if (Delegate.CreateDelegate(typeof(Action<TextWriter>), method, false) is Action<TextWriter> actionWithoutArgs)
                    return (writer, args) => { actionWithoutArgs(writer); return null; };

                if (Delegate.CreateDelegate(typeof(Action<IReadOnlyList<string>>), method, false) is Action<IReadOnlyList<string>> actionWithoutWriter)
                    return (writer, args) => { actionWithoutWriter(args); return null; };

                if (Delegate.CreateDelegate(typeof(Action), method, false) is Action actionWithoutWriterAndArgs)
                    return (writer, args) => { actionWithoutWriterAndArgs(); return null; };

                if (Delegate.CreateDelegate(typeof(Func<TextWriter, object>), method, false) is Func<TextWriter, object> funcWithoutArgs)
                    return (writer, args) => funcWithoutArgs(writer);

                if (Delegate.CreateDelegate(typeof(Func<IReadOnlyList<string>, object>), method, false) is Func<IReadOnlyList<string>, object> funcWithoutWriter)
                    return (writer, args) => funcWithoutWriter(args);

                if (Delegate.CreateDelegate(typeof(Func<object>), method, false) is Func<object> funcWithoutWriterAndArgs)
                    return (writer, args) => funcWithoutWriterAndArgs();

                genericMakeArgs[0] = method;
                if (BoxedFuncMaker.MakeGenericMethod(method.ReturnType).Invoke(null, genericMakeArgs) is CommandHandler funcBoxed)
                    return funcBoxed;

                if (BoxedFuncWithoutArgsMaker.MakeGenericMethod(method.ReturnType).Invoke(null, genericMakeArgs) is CommandHandler funcBoxedWithoutArgs)
                    return funcBoxedWithoutArgs;

                if (BoxedFuncWithoutWriterMaker.MakeGenericMethod(method.ReturnType).Invoke(null, genericMakeArgs) is CommandHandler funcBoxedWithoutWriter)
                    return funcBoxedWithoutWriter;

                if (BoxedFuncWithoutWriterAndArgsMaker.MakeGenericMethod(method.ReturnType).Invoke(null, genericMakeArgs) is CommandHandler funcBoxedWithoutWriterAndArgs)
                    return funcBoxedWithoutWriterAndArgs;

                return null;
            }

            public static void FindCommands(string prefix, out IReadOnlyList<CommandInfo> list, out int index, out int count)
            {
                if (string.IsNullOrWhiteSpace(prefix))
                    (list, index, count) = (Commands.Values, -1, 0);

                list = Commands.Values;
                index = Commands.IndexOfKey(prefix);
                if (index < 0) // if not found take the next element larger than prefix
                    index = ~index;

                var i = index;
                var n = list.Count;
                while (i < n && list[i].Name.StartsWith(prefix))
                    ++i;

                count = i - index;
            }

            /// <summary>
            /// Register an assembly for command loading.
            /// </summary>
            /// <param name="assembly"></param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Register(Assembly assembly) => assemblies.Add(assembly);

            /// <summary>
            /// Load commands defined in the registered assemblies that have not been loaded yet.
            /// Command loading is cumulative. 
            /// </summary>
            public static void LoadCommands()
            {
                if (assemblies.Count == 0)
                    return;

                foreach (var assembly in assemblies)
                    Inspect(assembly);

                assemblies.Clear();
            }

            private static void Inspect(Assembly assembly)
            {
                Debug.Log($"Inspecting {assembly} for console commands.");

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                var found = 0;
                foreach (var type in types)
                {
                    foreach (var method in type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        try
                        {
                            var attribute = method.GetCustomAttribute<CommandAttribute>();
                            if (attribute != null)
                            {
                                if (!method.IsStatic)
                                {
                                    Debug.LogError($"Method decorated with {typeof(CommandAttribute).Name} must be static: {method}");
                                }
                                else
                                {
                                    var name = attribute.Name ?? method.Name;
                                    if (!CommandAttribute.IsValidName(name))
                                    {
                                        Debug.LogError($"Error trying to register command '{name}' with handler {method}. Command  name contains invalid characters.");
                                    }
                                    else
                                    {
                                        var description = attribute.Description;
                                        var help = attribute.Help;

                                        if (Commands.ContainsKey(name))
                                            Debug.LogError($"Error trying to register command '{name}' with handler {method}. This command has already been registered.");
                                        else
                                        {
                                            var handler = MakeDelegate(method);
                                            if (handler == null)
                                                Debug.LogError($"Method decorated with {typeof(CommandAttribute).Name} is not supported: {method}");
                                            else
                                            {
                                                Commands.Add(name, new CommandInfo(name, description, help, handler));
                                                found++;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }
                    }
                }

                if (found > 0)
                    Debug.Log($"Found {found} console commands");
                else
                    Debug.Log($"No console commands found.");
            }

            private static class StandardCommands
            {
                [Command(
                    Name = "clear", 
                    Description = "Clears the screen.",
                    Help = "Usage: clear"
                )]
                private static void Clear(TextWriter writer) => Console.Instance.Clear();

                [Command(
                    Name = "date", 
                    Description = "Displays the system date/time.", 
                    Help = "Usage: date [OPTION]\n\n  -u            print date/time in UTC"
                )]
                private static void Date(TextWriter writer, IReadOnlyList<string> args)
                {
                    var n = args.Count;
                    if (n == 0)
                    {
                        writer.WriteLine(DateTime.Now.ToString());
                        return;
                    }

                    if (n == 1)
                    {
                        if (args[0] == "-u")
                        {
                            writer.WriteLine(DateTime.UtcNow.ToString());
                            return;
                        }
                    }

                    throw new ArgumentException(Resources.GetString(Strings.InvalidArguments));
                }

                [Command(
                    Name = "application.version", 
                    Description = "Displays the application version as defined in the project settings.",
                    Help = "Usage: application.version"
                )]
                private static void Version(TextWriter writer) => writer.WriteLine(Application.version);

                [Command(
                    Name = "application.productName", 
                    Description = "Displays the application name as defined by the product name in the project settings.",
                    Help = "Usage: application.productName"
                )]
                private static void AppName(TextWriter writer) => writer.WriteLine(Application.productName);

                [Command(
                    Name = "application.unityVersion",
                    Description = "Displays the unity version used to build the application.",
                    Help = "Usage: application.unityVersion"
                )]
                private static void AppInfo(TextWriter writer) => writer.WriteLine(Application.unityVersion);

                [Command(
                    Name = "application.buildGuid",
                    Description = "Displays a unique id assigned when the application was built.",
                    Help = "Usage: application.buildGuid"
                )]
                private static void AppBuildGuid(TextWriter writer) => writer.WriteLine(Application.buildGUID);

                [Command(
                    Name = "application.targetFrameRate",
                    Description = "Displays or sets the current target frame rate.",
                    Help = "Usage: app.targetFrameRate [VALUE]\n\n  VALUE         optional value to assign (must be a valid integer)\n\nNote that several factors may affect the actual application frame rate and some platforms (in particular mobile) may enforce a frame rate cap considerably below the target."
                )]
                private static void AppTargetFrameRate(TextWriter writer, IReadOnlyList<string> args)
                {
                    var n = args.Count;
                    if (n == 0)
                        writer.WriteLine(Application.targetFrameRate.ToString());
                    else if (n == 1 && int.TryParse(args[0], out var value))
                        Application.targetFrameRate = value;
                    else
                        throw new ArgumentException(Resources.GetString(Strings.InvalidArguments));
                }

                [Command(
                    Name = "echo",
                    Description = "Displays a message.",
                    Help = "Usage: echo [MESSAGE]\n\n  MESSAGE       optional message."
                )]
                private static void Echo(TextWriter writer, IReadOnlyList<string> args)
                {
                    var n = args.Count;
                    if (n > 0)
                    {
                        writer.Write(args[0]);
                        for (int i = 1; i < n; ++i)
                        {
                            writer.Write(' ');
                            writer.Write(args[i]);
                        }
                    }

                    writer.WriteLine();
                }

                [Command(
                    Name = "fail",
                    Description = "Displays an error message and returns a failure.",
                    Help = "Usage: fail [MESSAGE]\n\n  MESSAGE       optional error message"
                )]
                private static void Fail(TextWriter writer, IReadOnlyList<string> args) => throw new Exception(StringBuilder.Join(' ', args));

                [Command(
                    Name = "exit",
                    Description = "Quits the application.",
                    Help = "Usage: exit [CODE]\n\n  CODE          optional exit code"
                )]
                private static void Quit(TextWriter writer, IReadOnlyList<string> args)
                {
                    var n = args.Count;
                    if (n == 0)
                    {
                        Application.Quit();
                        return;
                    }

                    if (n == 1)
                    {
                        if (int.TryParse(args[0], out var exitcode))
                        { 
                            Application.Quit(exitcode);
                            return;
                        }
                    }

                    throw new ArgumentException(Resources.GetString(Strings.InvalidArguments));
                }

                [Command(
                    Name = "env",
                    Description = "Displays console variables.",
                    Help = "Usage: env"
                    )]
                private static void Env(TextWriter writer)
                {
                    foreach(var kv in Console.Instance.Variables)
                    {
                        writer.Write(kv.Key);
                        writer.Write('=');
                        writer.WriteLine(kv.Value);
                    }
                }

                [Command(
                    Name = "set",
                    Description = "Sets or clears the value of a console variable.",
                    Help = "Usage: set VARIABLE [VALUE]\n\n  VARIABLE      variable name\n  VALUE         optional value to assign (variable will be cleared if ommited)"
                )]
                private static void Set(TextWriter writer, IReadOnlyList<string> args)
                {
                    var n = args.Count;
                    if (n == 0)
                        throw new ArgumentException(Resources.GetString(Strings.NotEnoughArguments));

                    var name = args[0];
                    if (n == 1)
                        Console.Instance.Variables.Remove(name);
                    else
                        Console.Instance.Variables[name] = StringBuilder.Join(' ', args.Skip(1));
                }

                [Command(
                    Name = "help",
                    Description = "Displays help information.",
                    Help = "Usage: help [COMMAND]\n\n  COMMAND       optional command name or prefix to obtain information about."
                )]
                private static void Help(TextWriter writer, IReadOnlyList<string> args)
                {
                    var n = args.Count;
                    if (n  == 0)
                    {
                        foreach (var info in Shell.Commands.Values)
                        {
                            if (string.IsNullOrEmpty(info.Description))
                                writer.WriteLine(info.Name);
                            else
                            {
                                writer.Write(info.Name);
                                writer.Write(": ");
                                writer.WriteLine(info.Description);
                            }
                        }
                        return;
                    }

                    if (n == 1)
                    {
                        var name = args[0];
                        Shell.FindCommands(name, out var list, out var index, out var count);
                        if (count == 0)
                            throw new KeyNotFoundException(string.Format("No command found with prefix '{0}'", name));

                        if (count == 1)
                        {
                            var info = list[index];
                            var description = info.Description;
                            var help = info.Help;

                            var hasDescription = !string.IsNullOrEmpty(description);
                            var hasHelp = !string.IsNullOrEmpty(help);

                            if (!hasDescription && !hasHelp)
                                writer.WriteLine("No help information found.");
                            else
                            {
                                if (hasDescription)
                                    writer.WriteLine(description);
                                if (hasHelp)
                                    writer.WriteLine(help);
                            }
                        }
                        else
                        {
                            var m = index + count;
                            for (int i = index; i < m; ++i)
                            {
                                var info = list[i];
                                if (string.IsNullOrEmpty(info.Description))
                                    writer.WriteLine(info.Name);
                                else
                                {
                                    writer.Write(info.Name);
                                    writer.Write(": ");
                                    writer.WriteLine(info.Description);
                                }
                            }
                        }

                        return;
                    }

                    throw new ArgumentException(Resources.GetString(Strings.InvalidArguments));
                }
            }
        }

        /// <summary>
        /// An ad-hoc LALR parser that builds a graph representing the input sentece to be executed. 
        /// A syntax similar to that of the unix shell is supported including quoted literals, multiple 
        /// command combinations using AND/OR, command groups, variable expansion and command substitutions.
        /// </summary>
        private class Parser: IDisposable
        {
            private const char EscapeSymbol = '\\';

            private const char SingleQuoteSymbol = '\'';
            private const char DoubleQuoteSymbol = '"';

            private const char GroupOpenSymbol = '(';
            private const char GroupCloseSymbol = ')';

            private const char IdentifierOpenSymbol = '{';
            private const char IdentifierCloseSymbol = '}';

            private const string CommandSubstitutionOpenSymbol = "$(";
            private const string VariableSubstitutionOpenSymbol = "${";

            private const string ContinueSymbol = ";";
            private const string StopOnFailureSymbol = "&&";
            private const string StopOnSuccessSymbol = "||";

            public abstract class Token: IDisposable
            {
                public abstract void Dispose();

                public override string ToString() => throw new NotSupportedException();

                public abstract class Separator
                {
                    public sealed class WhiteSpace: Token
                    {
                        public override string ToString() => " ";

                        public override void Dispose() { }
                    }
                }

                public abstract class Delimiter: Token
                {
                    public new sealed class Continue: Delimiter
                    {
                        public override string ToString() => ContinueSymbol;
                    }

                    public new sealed class StopOnFailure: Delimiter
                    {
                        public override string ToString() => StopOnFailureSymbol;
                    }

                    public new sealed class StopOnSuccess: Delimiter
                    {
                        public override string ToString() => StopOnSuccessSymbol;
                    }

                    public override void Dispose() { }
                }

                public static readonly Token.Separator.WhiteSpace WhiteSpace = new Token.Separator.WhiteSpace();
                public static readonly Token.Delimiter.Continue Continue = new Token.Delimiter.Continue();
                public static readonly Token.Delimiter.StopOnFailure StopOnFailure = new Token.Delimiter.StopOnFailure();
                public static readonly Token.Delimiter.StopOnSuccess StopOnSuccess = new Token.Delimiter.StopOnSuccess();

                public enum ElementType
                {
                    Literal = 0,
                    VariableSubstitution
                }

                public sealed class Element: Token
                {
                    [DebuggerDisplay("Count = {queue.Count}")]
                    public sealed class Pool: Pool<Element>
                    {
                        protected override Element Create() => new Element() { Return = Return };

                        protected override void OnReturn(Element instance)
                        {
                            instance.Value.Reset();
                        }

                        public Element Take(ElementType type, bool isQuoted = false)
                        {
                            var token = Take();
                            token.isQuoted = isQuoted;
                            token.Type = type;
                            return token;
                        }
                    }

                    public StringBuilder.Buffer Value;

                    private bool isQuoted;

                    public ElementType Type { get; private set;}

                    private Element() { }

                    public override void Dispose() => Return(this);

                    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
                    private Action<Element> Return;

                    public override string ToString()
                    {
                        switch (Type)
                        {
                            case ElementType.Literal:
                                return isQuoted ? StringBuilder.Concat(SingleQuoteSymbol, Value, SingleQuoteSymbol) : Value.ToString();
                            case ElementType.VariableSubstitution:
                                return StringBuilder.Concat(VariableSubstitutionOpenSymbol, Value, IdentifierCloseSymbol);
                            default:
                                throw new NotSupportedException();
                        }
                    }
                }

                public abstract class Branch: Token
                {
                    public readonly List<Token> Items = new List<Token>();                    

                    public override string ToString() => StringBuilder.Concat(Items);

                    public override void Dispose()
                    {
                        foreach (var item in Items)
                            item.Dispose();
                    }
                }

                public enum ClauseType
                {
                    String = 0,
                    Command
                }

                public sealed class Clause: Branch
                {
                    [DebuggerDisplay("Count = {queue.Count}")]
                    public sealed class Pool: Pool<Clause>
                    {
                        protected override Clause Create() => new Clause() { Return = Return };

                        protected override void OnReturn(Clause instance)
                        {
                            instance.Items.Clear();
                        }

                        public Clause Take(ClauseType type)
                        {
                            var token = Take();
                            token.Type = type;
                            return token;
                        }
                    }

                    public ClauseType Type;

                    private Clause() { }

                    public override void Dispose()
                    {
                        base.Dispose();
                        Return(this);                        
                    }

                    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
                    private Action<Clause> Return;

                    public override string ToString()
                    {
                        switch (Type)
                        {
                            case ClauseType.String: 
                                return StringBuilder.Concat(DoubleQuoteSymbol, base.ToString(), DoubleQuoteSymbol);
                            case ClauseType.Command:
                                return base.ToString();
                            default:
                                throw new NotSupportedException();
                        }
                    }
                }
               
                public enum SentenceType
                {
                    CommandGroup = 0,
                    CommandSubstitution
                }

                public sealed class Sentence: Branch
                {
                    [DebuggerDisplay("Count = {queue.Count}")]
                    public sealed class Pool: Pool<Sentence>
                    {
                        protected override Sentence Create() => new Sentence() { Return = Return };

                        protected override void OnReturn(Sentence instance)
                        {
                            instance.Items.Clear();
                        }

                        public Sentence Take(SentenceType type)
                        {
                            var token = Take();
                            token.Type = type;
                            return token;
                        }
                    }

                    public SentenceType Type;

                    private Sentence() { }

                    public override void Dispose()
                    {
                        base.Dispose();
                        Return(this);
                    }

                    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
                    private Action<Sentence> Return;

                    public override string ToString()
                    {
                        switch (Type)
                        {
                            case SentenceType.CommandGroup:
                                return StringBuilder.Concat(GroupOpenSymbol, base.ToString(), GroupCloseSymbol);
                            case SentenceType.CommandSubstitution:
                                return StringBuilder.Concat(CommandSubstitutionOpenSymbol, base.ToString(), GroupCloseSymbol);
                            default:
                                throw new NotSupportedException();
                        }
                    }
                }
            }

            private enum State
            {
                SeekingEndOfRoot = 0,               // parsing root group
                SeekingSingleQuote,                 // parsing single-quoted string into a literal
                SeekingDoubleQuote,                 // parsing double-quoted string into a composition
                SeekingEndOfGroup,                  // parsing group
                SeekingEndOfCommandInRoot,          // parsing command in root
                SeekingEndOfCommandInGroup,         // parsing command in group
                SeekingEndOfCommandSubstitution,    // parsing command substitution
                SeekingEndOfVariableSubstitution,   // parsing variable substitution
                SeekingEndOfLiteralInRoot,          // parsing literal in root
                SeekingEndOfLiteralInComposition,   // parsing literal in a composition (double-quoted string)
                SeekingEndOfLiteralInGroup,         // parsing literal in group
                SeekingEndOfSpace,                  // parsing sequence of white spaces
                Accepted
            }

            private struct Level
            {
                public State State;
                public Token Token;

                public Level(State state, Token token)
                {
                    this.State = state;
                    this.Token = token;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool Match(char value, string source, int index = 0) => index < source.Length && value == source[index];

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool Match(string value, string source, int index = 0)
            {
                if (index + value.Length > source.Length)
                    return false;

                for (int i = 0; i < value.Length; ++i)
                    if (value[i] != source[index + i])
                        return false;

                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int LengthOf(char c) => 1;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int LengthOf(string s) => s.Length;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Level NextLevel(Stack<Level> stack, Level current, State state, Token token)
            {
                stack.Push(current);
                var group = current.Token as Token.Branch;
                group.Items.Add(token);
                return new Level(state, token);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void Escape(string input, ref int index)
            {
                if (input[index] == EscapeSymbol)
                {
                    index++;
                    if (index == input.Length)
                        throw new FormatException($"Syntax error near position {index}: invalid escape sequence {EscapeSymbol}");
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void RemoveTrailingSpace(Token.Branch group)
            {
                if (group.Items.Count > 0 && group.Items.Last() is Token.Separator.WhiteSpace)
                    group.Items.RemoveAt(group.Items.Count - 1);
            }

            public static IEnumerator<(int Index, int Count)> Split(string value)
            {
                var n = value.Length;                
                var i = 0;
                while(i < n)
                {
                    if (char.IsWhiteSpace(value[i]))
                    {
                        yield return (0, 0);
                        do
                            ++i;
                        while (i < n && char.IsWhiteSpace(value[i]));
                    }
                    else
                    {
                        var j = i;
                        do
                            ++i;
                        while (i < n && !char.IsWhiteSpace(value[i]));
                        yield return (j, i - j);
                    }
                }
            }

            public static IEnumerator<(int Index, int Count)> Split(StringBuilder value)
            {
                var n = value.Length;
                var i = 0;
                while (i < n)
                {
                    if (char.IsWhiteSpace(value[i]))
                    {
                        yield return (0, 0);
                        do
                            ++i;
                        while (i < n && char.IsWhiteSpace(value[i]));
                    }
                    else
                    {
                        var j = i;
                        do
                            ++i;
                        while (i < n && !char.IsWhiteSpace(value[i]));
                        yield return (j, i - j);
                    }
                }
            }

            private Token.Element.Pool elementPool = new Token.Element.Pool();
            private Token.Clause.Pool clausePool = new Token.Clause.Pool();
            private Token.Sentence.Pool sentencePool = new Token.Sentence.Pool();

            private readonly Stack<Level> stack = new Stack<Level>();

            public Token Parse(string input)
            {
                try
                {
                    var level = new Level(State.SeekingEndOfRoot, sentencePool.Take(Token.SentenceType.CommandGroup));
                    var index = 0;

                    while (level.State != State.Accepted)
                    {
                        switch (level.State)
                        {
                            case State.SeekingEndOfRoot:
                                if (index == input.Length)
                                {
                                    var group = level.Token as Token.Branch;
                                    if (group.Items.Count > 0 && group.Items.Last() is Token.Delimiter)
                                        throw new FormatException($"Syntax error near position {index}: unexpected end of expression");

                                    level.State = State.Accepted;
                                }
                                else if (char.IsWhiteSpace(input[index]))
                                {
                                    index++;
                                }
                                else if (Match(ContinueSymbol, input, index))
                                {
                                    var group = level.Token as Token.Branch;
                                    if (group.Items.Count == 0 || group.Items.Last() is Token.Delimiter)
                                        throw new FormatException($"Syntax error near position {index}: unexpected {ContinueSymbol}");

                                    group.Items.Add(Token.Continue);
                                    index += LengthOf(ContinueSymbol);
                                }
                                else if (Match(StopOnFailureSymbol, input, index))
                                {
                                    var group = level.Token as Token.Branch;
                                    if (group.Items.Count == 0 || group.Items.Last() is Token.Delimiter)
                                        throw new FormatException($"Syntax error near position {index}: unexpected {StopOnFailureSymbol}");

                                    group.Items.Add(Token.StopOnFailure);
                                    index += LengthOf(StopOnFailureSymbol);
                                }
                                else if (Match(StopOnSuccessSymbol, input, index))
                                {
                                    var group = level.Token as Token.Branch;
                                    if (group.Items.Count == 0 || group.Items.Last() is Token.Delimiter)
                                        throw new FormatException($"Syntax error near position {index}: unexpected {StopOnSuccessSymbol}");

                                    group.Items.Add(Token.StopOnSuccess);
                                    index += LengthOf(StopOnSuccessSymbol);
                                }
                                else if (Match(GroupOpenSymbol, input, index))
                                {
                                    var group = level.Token as Token.Branch;
                                    if (group.Items.Count > 0 && !(group.Items.Last() is Token.Delimiter))
                                        throw new FormatException($"Syntax error near position {index}: unexpected {GroupOpenSymbol}");

                                    level = NextLevel(stack, level, State.SeekingEndOfGroup, sentencePool.Take(Token.SentenceType.CommandGroup));
                                    index += LengthOf(GroupOpenSymbol);
                                }
                                else if (Match(GroupCloseSymbol, input, index))
                                {
                                    throw new FormatException($"Syntax error near position {index}: unexpected {GroupCloseSymbol}");
                                }
                                else
                                {
                                    var group = level.Token as Token.Branch;
                                    if (group.Items.Count > 0 && !(group.Items.Last() is Token.Delimiter))
                                        throw new FormatException($"Syntax error near position {index}: a delimiter was expected");

                                    level = NextLevel(stack, level, State.SeekingEndOfCommandInRoot, clausePool.Take(Token.ClauseType.Command));
                                }
                                break;
                            case State.SeekingEndOfCommandInRoot:
                                if (index == input.Length)
                                {
                                    RemoveTrailingSpace(level.Token as Token.Branch);
                                    level = stack.Pop();
                                }
                                else if (char.IsWhiteSpace(input[index]))
                                {
                                    level = NextLevel(stack, level, State.SeekingEndOfSpace, Token.WhiteSpace);
                                    index++;
                                }
                                else if (Match(SingleQuoteSymbol, input, index))
                                {
                                    level = NextLevel(stack, level, State.SeekingSingleQuote, elementPool.Take(Token.ElementType.Literal, true));
                                    index += LengthOf(SingleQuoteSymbol);
                                }
                                else if (Match(DoubleQuoteSymbol, input, index))
                                {
                                    level = NextLevel(stack, level, State.SeekingDoubleQuote, clausePool.Take(Token.ClauseType.String));
                                    index += LengthOf(DoubleQuoteSymbol);
                                }
                                else if (Match(VariableSubstitutionOpenSymbol, input, index))
                                {
                                    level = NextLevel(stack, level, State.SeekingEndOfVariableSubstitution, elementPool.Take(Token.ElementType.VariableSubstitution));
                                    index += LengthOf(VariableSubstitutionOpenSymbol);
                                }
                                else if (Match(CommandSubstitutionOpenSymbol, input, index))
                                {
                                    level = NextLevel(stack, level, State.SeekingEndOfCommandSubstitution, sentencePool.Take(Token.SentenceType.CommandSubstitution));
                                    index += LengthOf(CommandSubstitutionOpenSymbol);
                                }
                                else if (Match(GroupOpenSymbol, input, index)
                                      || Match(GroupCloseSymbol, input, index)
                                      || Match(ContinueSymbol, input, index)
                                      || Match(StopOnFailureSymbol, input, index)
                                      || Match(StopOnSuccessSymbol, input, index))
                                {
                                    RemoveTrailingSpace(level.Token as Token.Branch);
                                    level = stack.Pop();
                                }
                                else
                                {
                                    level = NextLevel(stack, level, State.SeekingEndOfLiteralInRoot, elementPool.Take(Token.ElementType.Literal));
                                }
                                break;
                            case State.SeekingSingleQuote:
                                if (index == input.Length)
                                {
                                    throw new FormatException($"Syntax error at ({index + 1}): {SingleQuoteSymbol} was expected");
                                }
                                else if (Match(SingleQuoteSymbol, input, index))
                                {
                                    level = stack.Pop();
                                    index += LengthOf(SingleQuoteSymbol);
                                }
                                else
                                {
                                    var node = level.Token as Token.Element;
                                    node.Value.Append(input[index]);
                                    index++;
                                }
                                break;
                            case State.SeekingDoubleQuote:
                                if (index == input.Length)
                                {
                                    throw new FormatException($"Syntax error at ({index + 1}): {DoubleQuoteSymbol} was expected");
                                }
                                else if (Match(DoubleQuoteSymbol, input, index))
                                {
                                    level = stack.Pop();
                                    index += LengthOf(DoubleQuoteSymbol);
                                }
                                else if (Match(VariableSubstitutionOpenSymbol, input, index))
                                {
                                    level = NextLevel(stack, level, State.SeekingEndOfVariableSubstitution, elementPool.Take(Token.ElementType.VariableSubstitution));
                                    index += LengthOf(VariableSubstitutionOpenSymbol);
                                }
                                else if (Match(CommandSubstitutionOpenSymbol, input, index))
                                {
                                    level = NextLevel(stack, level, State.SeekingEndOfCommandSubstitution, sentencePool.Take(Token.SentenceType.CommandSubstitution));
                                    index += LengthOf(CommandSubstitutionOpenSymbol);
                                }
                                else
                                {
                                    level = NextLevel(stack, level, State.SeekingEndOfLiteralInComposition, elementPool.Take(Token.ElementType.Literal));
                                }
                                break;
                            case State.SeekingEndOfGroup:
                            case State.SeekingEndOfCommandSubstitution:
                                if (index == input.Length)
                                {
                                    throw new FormatException($"Syntax error at ({index + 1}): {GroupCloseSymbol} was expected");
                                }
                                else if (char.IsWhiteSpace(input[index]))
                                {
                                    index++;
                                }
                                else if (Match(ContinueSymbol, input, index))
                                {
                                    var group = level.Token as Token.Branch;
                                    if (group.Items.Count == 0 || group.Items.Last() is Token.Delimiter)
                                        throw new FormatException($"Syntax error near position {index}: unexpected {ContinueSymbol}");

                                    group.Items.Add(Token.Continue);
                                    index += LengthOf(ContinueSymbol);
                                }
                                else if (Match(StopOnFailureSymbol, input, index))
                                {
                                    var group = level.Token as Token.Branch;
                                    if (group.Items.Count == 0 || group.Items.Last() is Token.Delimiter)
                                        throw new FormatException($"Syntax error near position {index}: unexpected {StopOnFailureSymbol}");

                                    group.Items.Add(Token.StopOnFailure);
                                    index += LengthOf(StopOnFailureSymbol);
                                }
                                else if (Match(StopOnSuccessSymbol, input, index))
                                {
                                    var group = level.Token as Token.Branch;
                                    if (group.Items.Count == 0 || group.Items.Last() is Token.Delimiter)
                                        throw new FormatException($"Syntax error near position {index}: unexpected {StopOnSuccessSymbol}");

                                    group.Items.Add(Token.StopOnSuccess);
                                    index += LengthOf(StopOnSuccessSymbol);
                                }
                                else if (Match(GroupOpenSymbol, input, index))
                                {
                                    var group = level.Token as Token.Branch;
                                    if (group.Items.Count > 0 && !(group.Items.Last() is Token.Delimiter))
                                        throw new FormatException($"Syntax error near position {index}: unexpected {GroupOpenSymbol}");

                                    level = NextLevel(stack, level, State.SeekingEndOfGroup, sentencePool.Take(Token.SentenceType.CommandGroup));
                                    index += LengthOf(GroupOpenSymbol);
                                }
                                else if (Match(GroupCloseSymbol, input, index))
                                {
                                    level = stack.Pop();
                                    index += LengthOf(GroupCloseSymbol);
                                }
                                else
                                {
                                    var group = level.Token as Token.Branch;
                                    if (group.Items.Count > 0 && !(group.Items.Last() is Token.Delimiter))
                                        throw new FormatException($"Syntax error near position {index}: a delimiter was expected");

                                    level = NextLevel(stack, level, State.SeekingEndOfCommandInGroup, clausePool.Take(Token.ClauseType.Command));
                                }
                                break;
                            case State.SeekingEndOfCommandInGroup:
                                if (index == input.Length)
                                {
                                    RemoveTrailingSpace(level.Token as Token.Branch);
                                    level = stack.Pop();
                                }
                                else if (char.IsWhiteSpace(input[index]))
                                {
                                    level = NextLevel(stack, level, State.SeekingEndOfSpace, Token.WhiteSpace);
                                    index++;
                                }
                                else if (Match(SingleQuoteSymbol, input, index))
                                {
                                    level = NextLevel(stack, level, State.SeekingSingleQuote, elementPool.Take(Token.ElementType.Literal, true));
                                    index += LengthOf(SingleQuoteSymbol);
                                }
                                else if (Match(DoubleQuoteSymbol, input, index))
                                {
                                    level = NextLevel(stack, level, State.SeekingDoubleQuote, clausePool.Take(Token.ClauseType.String));
                                    index += LengthOf(DoubleQuoteSymbol);
                                }
                                else if (Match(VariableSubstitutionOpenSymbol, input, index))
                                {
                                    level = NextLevel(stack, level, State.SeekingEndOfVariableSubstitution, elementPool.Take(Token.ElementType.VariableSubstitution));
                                    index += LengthOf(VariableSubstitutionOpenSymbol);
                                }
                                else if (Match(CommandSubstitutionOpenSymbol, input, index))
                                {
                                    level = NextLevel(stack, level, State.SeekingEndOfCommandSubstitution, sentencePool.Take(Token.SentenceType.CommandSubstitution));
                                    index += LengthOf(CommandSubstitutionOpenSymbol);
                                }
                                else if (Match(GroupOpenSymbol, input, index)
                                      || Match(GroupCloseSymbol, input, index)
                                      || Match(ContinueSymbol, input, index)
                                      || Match(StopOnFailureSymbol, input, index)
                                      || Match(StopOnSuccessSymbol, input, index))
                                {
                                    RemoveTrailingSpace(level.Token as Token.Branch);
                                    level = stack.Pop();
                                }
                                else if (Match(GroupCloseSymbol, input, index)
                                      || Match(ContinueSymbol, input, index)
                                      || Match(StopOnFailureSymbol, input, index)
                                      || Match(StopOnSuccessSymbol, input, index))
                                {
                                    RemoveTrailingSpace(level.Token as Token.Branch);
                                    level = stack.Pop();
                                }
                                else
                                {
                                    level = NextLevel(stack, level, State.SeekingEndOfLiteralInGroup, elementPool.Take(Token.ElementType.Literal));
                                }
                                break;
                            case State.SeekingEndOfLiteralInRoot:
                                if (index == input.Length)
                                {
                                    level = stack.Pop();
                                }
                                else if (char.IsWhiteSpace(input[index])
                                      || Match(SingleQuoteSymbol, input, index)
                                      || Match(DoubleQuoteSymbol, input, index)
                                      || Match(GroupOpenSymbol, input, index)
                                      || Match(VariableSubstitutionOpenSymbol, input, index)
                                      || Match(CommandSubstitutionOpenSymbol, input, index)
                                      || Match(ContinueSymbol, input, index)
                                      || Match(StopOnFailureSymbol, input, index)
                                      || Match(StopOnSuccessSymbol, input, index))
                                {
                                    level = stack.Pop();
                                }
                                else
                                {
                                    Escape(input, ref index);
                                    var node = level.Token as Token.Element;
                                    node.Value.Append(input[index]);
                                    index++;
                                }
                                break;
                            case State.SeekingEndOfLiteralInComposition:
                                if (index == input.Length)
                                {
                                    level = stack.Pop();
                                }
                                else if (Match(DoubleQuoteSymbol, input, index)
                                      || Match(VariableSubstitutionOpenSymbol, input, index)
                                      || Match(CommandSubstitutionOpenSymbol, input, index))
                                {
                                    level = stack.Pop();
                                }
                                else
                                {
                                    Escape(input, ref index);
                                    var node = level.Token as Token.Element;
                                    node.Value.Append(input[index]);
                                    index++;
                                }
                                break;
                            case State.SeekingEndOfLiteralInGroup:
                                if (index == input.Length)
                                {
                                    level = stack.Pop();
                                }
                                else if (char.IsWhiteSpace(input[index])
                                      || Match(SingleQuoteSymbol, input, index)
                                      || Match(DoubleQuoteSymbol, input, index)
                                      || Match(VariableSubstitutionOpenSymbol, input, index)
                                      || Match(CommandSubstitutionOpenSymbol, input, index)
                                      || Match(GroupOpenSymbol, input, index)
                                      || Match(GroupCloseSymbol, input, index)
                                      || Match(ContinueSymbol, input, index)
                                      || Match(StopOnFailureSymbol, input, index)
                                      || Match(StopOnSuccessSymbol, input, index))
                                {
                                    level = stack.Pop();
                                }
                                else
                                {
                                    Escape(input, ref index);
                                    var node = level.Token as Token.Element;
                                    node.Value.Append(input[index]);
                                    index++;
                                }
                                break;
                            case State.SeekingEndOfVariableSubstitution:
                                if (index == input.Length)
                                {
                                    throw new FormatException($"Syntax error at ({index + 1}): {IdentifierCloseSymbol} was expected");
                                }
                                else if (Match(IdentifierCloseSymbol, input, index))
                                {
                                    var node = level.Token as Token.Element;
                                    if (node.Value.Length == 0)
                                        throw new FormatException($"Syntax error at ({index + 1}): invalid identifier");

                                    level = stack.Pop();
                                    index += LengthOf(IdentifierCloseSymbol);
                                }
                                else
                                {
                                    var c = input[index];
                                    var node = level.Token as Token.Element;
                                    if (!char.IsLetter(c) && c != '_' && !(char.IsDigit(c) && node.Value.Length > 0))
                                        throw new FormatException($"Syntax error at ({index + 1}): invalid identifier");
                                    node.Value.Append(c);
                                    index++;
                                }
                                break;
                            case State.SeekingEndOfSpace:
                                if (index == input.Length || !char.IsWhiteSpace(input[index]))
                                    level = stack.Pop();
                                else
                                    index++;
                                break;
                            default:
                                throw new NotImplementedException($"State '{level.State}' is not implemented.");
                        }
                    }

                    return level.Token;
                }
                finally
                {
                    stack.Clear();
                }
            }

            public void Dispose()
            {
                elementPool?.Dispose();
                elementPool = null;

                clausePool?.Dispose();
                clausePool = null;

                sentencePool?.Dispose();
                sentencePool = null;
            }
        }
               
        private Coroutine reader;

        protected override bool Transient => true;

        // TODO: create a standard command to set/clear variables ?

        public Dictionary<string, string> Variables { get; private set; }

#if UNITY_EDITOR
        protected override void Reset()
        {
            Variables?.Clear();            
        }

        protected override void OnValidate() { }
#endif

        protected override void OnSingletonAwake()
        {
            base.OnSingletonAwake();

            Shell.LoadCommands();
            Variables = new Dictionary<string, string>();
            parser = new Parser();
        }

        protected override void OnSingletonDestroy()
        {
            parser?.Dispose();
            parser = null;

            base.OnSingletonDestroy();
        }

        protected virtual void OnStarted() { }

        protected virtual void OnProcessing() { }

        protected virtual void OnProcessed() { }

        protected abstract TextWriter GetErrorWriter();
        protected abstract TextWriter GetOutputWriter();

        protected abstract bool TryReadLine(out string value);

        protected abstract void Clear();

        protected virtual void Start()
        {
            reader = StartCoroutine(Read().Catch(Critical));
        }

        private TextWriter errorWriter;
        public TextWriter Error => errorWriter;

        private TextWriter outputWriter;
        public TextWriter Out => outputWriter;

        private Parser parser;       

        private void ErrorLine(string s) => errorWriter.WriteLine(s);
        private void ErrorLine(Exception e) => errorWriter.WriteLine(e.Message);

        private void Critical(Exception e)
        {
            Debug.LogException(e);
            var msg = e.Message;
            if (string.IsNullOrEmpty(msg))
                errorWriter.WriteLine("Critical exception.");
            else
            {
                errorWriter.Write("Critical exception: ");
                errorWriter.WriteLine(msg);
            }
            Destroy(this);
        }

        private IEnumerator Read()
        {
            errorWriter = GetErrorWriter();
            outputWriter = GetOutputWriter();

            OnStarted();
            while (true)
            {                
                if (TryReadLine(out string value) && !string.IsNullOrEmpty(value))
                {
                    OnProcessing();
                    yield return Process(value, outputWriter).Catch(ErrorLine);
                    OnProcessed();
                }

                yield return null;
            }
        }

        private IEnumerator Process(string input, TextWriter writer)
        {
            using (var token = parser.Parse(input))
                if (token is Parser.Token.Sentence sentence)
                    yield return Process(sentence, writer).Catch(ErrorLine);
        }

        private enum ProcessCondition
        {
            None,
            Success,
            Failure
        }

        private IEnumerator Process(Parser.Token.Sentence current, TextWriter writer)
        {
            var exception = default(Exception);
            var condition = ProcessCondition.None;

            foreach (var item in current.Items)
            {
                switch (item)
                {
                    case Parser.Token.Clause clause:
                        switch (clause.Type)
                        {
                            case Parser.Token.ClauseType.Command:
                                if ((condition == ProcessCondition.None)
                                 || (condition == ProcessCondition.Success && exception == null)
                                 || (condition == ProcessCondition.Failure && exception != null))
                                {
                                    exception = null;
                                    yield return Process(clause, writer).Catch(e => exception = e);
                                }
                                break;
                            default:
                                break;
                        }
                        break;
                    case Parser.Token.Sentence sentence:
                        switch (sentence.Type)
                        {
                            case Parser.Token.SentenceType.CommandGroup:
                                if ((condition == ProcessCondition.None)
                                 || (condition == ProcessCondition.Success && exception == null)
                                 || (condition == ProcessCondition.Failure && exception != null))
                                {
                                    exception = null;
                                    yield return Process(sentence, writer).Catch(e => exception = e);
                                }
                                break;
                            default:
                                break;
                        }
                        break;
                    case Parser.Token.Delimiter.Continue _:
                        condition = ProcessCondition.None;
                        break;
                    case Parser.Token.Delimiter.StopOnFailure _:
                        condition = ProcessCondition.Success;
                        break;
                    case Parser.Token.Delimiter.StopOnSuccess _:
                        condition = ProcessCondition.Failure;
                        break;
                    default:
                        break;
                }
            }

            if (exception != null)
                throw exception;
        }

        private IEnumerator Process(Parser.Token.Clause current, TextWriter writer)
        {
            if (current.Type == Parser.Token.ClauseType.String)
            {
                foreach (var item in current.Items)
                {
                    switch (item)
                    {
                        case Parser.Token.Element element:
                            switch (element.Type)
                            {                                
                                case Parser.Token.ElementType.Literal:
                                    {
                                        var (buffer, index, count) = element.Value.AsArraySegment();
                                        writer.Write(buffer, index, count);
                                    }
                                    break;
                                case Parser.Token.ElementType.VariableSubstitution:
                                    if (Variables.TryGetValue(element.Value.ToString(), out string value))
                                        writer.Write(value);
                                    break;
                                default:
                                    break;
                            }
                            break;
                        case Parser.Token.Sentence sentence:
                            switch (sentence.Type)
                            {                                
                                case Parser.Token.SentenceType.CommandSubstitution:
                                    yield return Process(sentence, writer).Catch(ErrorLine);
                                    break;
                                default:
                                    break;
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            else if (current.Type == Parser.Token.ClauseType.Command)
            {
                var command = default(string);
                var parameters = new List<string>();            

                using (var argument = new StringBuilder())
                {
                    foreach (var item in current.Items)
                    {
                        switch (item)
                        {
                            case Parser.Token.Element element:
                                switch (element.Type)
                                {
                                    case Parser.Token.ElementType.Literal:
                                        argument.Append(element.Value);
                                        break;
                                    case Parser.Token.ElementType.VariableSubstitution:
                                        if (Variables.TryGetValue(element.Value.ToString(), out string value))
                                        {
                                            using (var enumerator = Parser.Split(value))
                                            {
                                                while (enumerator.MoveNext())
                                                {
                                                    var (index, count) = enumerator.Current;
                                                    if (count > 0)
                                                    {
                                                        argument.Append(value, index, count);
                                                    }
                                                    else if (argument.Length > 0)
                                                    {
                                                        if (string.IsNullOrEmpty(command))
                                                            command = argument.ToString();
                                                        else
                                                            parameters.Add(argument.ToString());

                                                        argument.Clear();
                                                    }
                                                }
                                            }
                                        }
                                        break;
                                    default:
                                        break;
                                }
                                break;
                            case Parser.Token.Clause clause:
                                switch (clause.Type)
                                {
                                    case Parser.Token.ClauseType.String:
                                        {
                                            var wait = Process(clause, new StringWriter(argument));
                                            if (wait != null)
                                                yield return wait;
                                        }
                                        break;
                                    default:
                                        break;
                                }
                                break;
                            case Parser.Token.Sentence sentence:
                                switch (sentence.Type)
                                {
                                    case Parser.Token.SentenceType.CommandSubstitution:
                                        using (var sb = new StringBuilder())
                                        {
                                            yield return Process(sentence, new StringWriter(sb)).Catch(ErrorLine);

                                            using (var enumerator = Parser.Split(sb))
                                            {
                                                while (enumerator.MoveNext())
                                                {
                                                    var (index, count) = enumerator.Current;
                                                    if (count > 0)
                                                    {
                                                        argument.Append(sb.AsSpan().Slice(index, count));
                                                    }
                                                    else if (argument.Length > 0)
                                                    {
                                                        if (string.IsNullOrEmpty(command))
                                                            command = argument.ToString();
                                                        else
                                                            parameters.Add(argument.ToString());

                                                        argument.Clear();
                                                    }
                                                }
                                            }
                                        }
                                        break;
                                    default:
                                        break;
                                }
                                break;
                            case Parser.Token.Separator.WhiteSpace _:
                                if (argument.Length > 0)
                                {
                                    if (string.IsNullOrEmpty(command))
                                        command = argument.ToString();
                                    else
                                        parameters.Add(argument.ToString());

                                    argument.Clear();
                                }
                                break;
                            default:
                                break;
                        }
                    }

                    // Add last argument if any
                    if (argument.Length > 0)
                    {
                        if (string.IsNullOrEmpty(command))
                            command = argument.ToString();
                        else
                            parameters.Add(argument.ToString());
                    }
                }
                
                if (!Commands.TryGetValue(command, out CommandInfo info))
                    throw new KeyNotFoundException($"Command not found: {command}");

                var ret = info.Handler(writer, parameters);
                switch (ret)
                {
                    case IEnumerator enumerator:
                        yield return enumerator;
                        break;
                    case YieldInstruction wait:
                        yield return wait;
                        break;
                    case int frames:
                        yield return frames;
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
