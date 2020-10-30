﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Runtime.CompilerServices;

using UnityEngine;
using UnityEngine.Scripting;

namespace Carambolas.UnityEngine
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ConsoleAttribute: PreserveAttribute
    {
        public string Name;
        public string Description;
        public string Help;
    }

    public abstract class Console: SingletonBehaviour<Console>
    {
        private static class Shell
        {
            public delegate object CommandDelegate(Writer writer, params string[] args);

            private struct CommandInfo
            {
                public string Description;
                public string Help;

                public CommandInfo(string description, string help)
                {
                    this.Description = description;
                    this.Help = help;
                }
            }
            
            private static readonly Dictionary<string, CommandDelegate> handlers = new Dictionary<string, CommandDelegate>();            
            private static readonly Dictionary<string, CommandInfo> details = new Dictionary<string, CommandInfo>();

            private static string[] names;

            public static void FindCommandNamesStartingWith(string prefix, out ArraySegment<string> segment)
            {
                if (string.IsNullOrWhiteSpace(prefix))
                    return;

                var index = names.BinarySearch(prefix);
                if (index < 0) // if not found take the next element larger than prefix
                    index = ~index;

                var i = index;
                while (i < names.Length && names[i].StartsWith(prefix))
                    ++i;

                segment = new ArraySegment<string>(names, index, i - index);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool TryGetCommandDelegate(string key, out CommandDelegate value) => handlers.TryGetValue(key, out value);

            private static readonly MethodInfo BoxedFuncMaker = typeof(Console).GetMethod("GenericMakeMethod", BindingFlags.NonPublic | BindingFlags.Static);
            private static readonly MethodInfo BoxedFuncWithoutArgsMaker = typeof(Console).GetMethod("GenericMakeMethodWithoutArgs", BindingFlags.NonPublic | BindingFlags.Static);
            private static readonly MethodInfo BoxedFuncWithoutWriterMaker = typeof(Console).GetMethod("GenericMakeMethodWithoutWriter", BindingFlags.NonPublic | BindingFlags.Static);
            private static readonly MethodInfo BoxedFuncWithoutWriterAndArgsMaker = typeof(Console).GetMethod("GenericMakeMethodWithoutWriterAndArgs", BindingFlags.NonPublic | BindingFlags.Static);

            private static CommandDelegate GenericMakeMethod<U>(MethodInfo method) => (Delegate.CreateDelegate(typeof(Func<Writer, string[], U>), method, false) is Func<Writer, string[], U> func) ? (writer, args) => func(writer, args) : (CommandDelegate)null;

            private static CommandDelegate GenericMakeMethodWithoutArgs<U>(MethodInfo method) => (Delegate.CreateDelegate(typeof(Func<Writer, U>), method, false) is Func<Writer, U> func) ? (writer, args) => func(writer) : (CommandDelegate)null;

            private static CommandDelegate GenericMakeMethodWithoutWriter<U>(MethodInfo method) => (Delegate.CreateDelegate(typeof(Func<string[], U>), method, false) is Func<string[], U> func) ? (writer, args) => func(args) : (CommandDelegate)null;

            private static CommandDelegate GenericMakeMethodWithoutWriterAndArgs<U>(MethodInfo method) => (Delegate.CreateDelegate(typeof(Func<U>), method, false) is Func<U> func) ? (writer, args) => func() : (CommandDelegate)null;

            private static readonly object[] genericMakeArgs = new object[1];

            private static CommandDelegate MakeDelegate(MethodInfo method)
            {
                if (Delegate.CreateDelegate(typeof(CommandDelegate), method, false) is CommandDelegate func)
                    return func;

                if (Delegate.CreateDelegate(typeof(Action<Writer, string[]>), method, false) is Action<Writer, string[]> action)
                    return (writer, args) => { action(writer, args); return null; };

                if (Delegate.CreateDelegate(typeof(Action<Writer>), method, false) is Action<Writer> actionWithoutArgs)
                    return (writer, args) => { actionWithoutArgs(writer); return null; };

                if (Delegate.CreateDelegate(typeof(Action<string[]>), method, false) is Action<string[]> actionWithoutWriter)
                    return (writer, args) => { actionWithoutWriter(args); return null; };

                if (Delegate.CreateDelegate(typeof(Action), method, false) is Action actionWithoutWriterAndArgs)
                    return (writer, args) => { actionWithoutWriterAndArgs(); return null; };

                if (Delegate.CreateDelegate(typeof(Func<Writer, object>), method, false) is Func<Writer, object> funcWithoutArgs)
                    return (writer, args) => funcWithoutArgs(writer);

                if (Delegate.CreateDelegate(typeof(Func<string[], object>), method, false) is Func<string[], object> funcWithoutWriter)
                    return (writer, args) => funcWithoutWriter(args);

                if (Delegate.CreateDelegate(typeof(Func<object>), method, false) is Func<object> funcWithoutWriterAndArgs)
                    return (writer, args) => funcWithoutWriterAndArgs();

                genericMakeArgs[0] = method;
                if (BoxedFuncMaker.MakeGenericMethod(method.ReturnType).Invoke(null, genericMakeArgs) is CommandDelegate funcBoxed)
                    return funcBoxed;

                if (BoxedFuncWithoutArgsMaker.MakeGenericMethod(method.ReturnType).Invoke(null, genericMakeArgs) is CommandDelegate funcBoxedWithoutArgs)
                    return funcBoxedWithoutArgs;

                if (BoxedFuncWithoutWriterMaker.MakeGenericMethod(method.ReturnType).Invoke(null, genericMakeArgs) is CommandDelegate funcBoxedWithoutWriter)
                    return funcBoxedWithoutWriter;

                if (BoxedFuncWithoutWriterAndArgsMaker.MakeGenericMethod(method.ReturnType).Invoke(null, genericMakeArgs) is CommandDelegate funcBoxedWithoutWriterAndArgs)
                    return funcBoxedWithoutWriterAndArgs;

                return null;
            }

            public static void RegisterCommands(Assembly[] assemblies)
            {
                handlers.Clear();
                details.Clear();

                RegisterCommands(typeof(Console).Assembly);
                foreach (var assembly in assemblies)
                    RegisterCommands(assembly);

                names = handlers.Keys.ToArray();
                names.Sort();
            }

            private static void RegisterCommands(Assembly assembly)
            {
                Debug.Log($"Inspecting assembly {assembly} for console commands.");

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                foreach (var type in types)
                {
                    foreach (var method in type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        try
                        {
                            var attribute = method.GetCustomAttribute<ConsoleAttribute>();
                            if (attribute != null)
                            {
                                if (!method.IsStatic)
                                {
                                    Debug.LogError($"Method decorated with {typeof(ConsoleAttribute).Name} must be static: {method}");
                                }
                                else
                                {
                                    var name = attribute.Name ?? method.Name;
                                    var description = attribute.Description;
                                    var help = attribute.Help;

                                    if (handlers.ContainsKey(name))
                                        Debug.LogError($"Error trying to register command '{name}' with method {method}. This command was already registered with another handler.");
                                    else
                                    {
                                        var handler = MakeDelegate(method);
                                        if (handler == null)
                                        {
                                            Debug.LogError($"Method decorated with {typeof(ConsoleAttribute).Name} is not supported: {method}");
                                        }
                                        else
                                        {

                                            handlers.Add(name, handler);
                                            details.Add(name, new CommandInfo(description, help));
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
            }

            private static class StandardCommands
            {
                [Console(Name = "frame")]
                private static void Frame(Writer writer) => writer.Out.WriteLine(Time.frameCount);

                [Console(Name = "app.version")]
                private static void Version(Writer writer) => writer.Out.WriteLine(Application.version);

                [Console(Name = "app.name")]
                private static void AppName(Writer writer) => writer.Out.WriteLine($"{Application.productName}");

                [Console(Name = "app.info")]
                private static void AppInfo(Writer writer) => writer.Out.WriteLine($"{Application.productName} {Application.version} (unity version {Application.unityVersion})");

                [Console(Name = "echo")]
                private static void Echo(Writer writer, params string[] args)
                {
                    if (args?.Length > 0)
                    {
                        for (int i = 0; i < args.Length - 1; ++i)
                        {
                            writer.Out.Write(args[i]);
                            writer.Out.Write(' ');
                        }

                        writer.Out.Write(args[args.Length - 1]);
                    }
                    writer.Out.WriteLine();
                }

                [Console(Name = "fail")]
                private static void Fail(Writer writer, params string[] args)
                {
                    if (args?.Length > 0)
                    {
                        for (int i = 0; i < args.Length - 1; ++i)
                        {
                            writer.Error.Write(args[i]);
                            writer.Error.Write(' ');
                        }

                        writer.Error.Write(args[args.Length - 1]);
                    }
                    writer.Error.WriteLine();

                    throw new UnityException(string.Empty);
                }

                [Console(Name = "quit")]
                private static void Quit(Writer writer) => Application.Quit();
            }
        }

        private static class Parser
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

            private const char ContinueSymbol = ';';

            private const string StopOnFailureSymbol = "&&";
            private const string StopOnSuccessSymbol = "||";

            public abstract class Token
            {
                public override string ToString() => throw new NotImplementedException();

                public abstract class Leaf: Token
                {
                    public readonly StringBuilder Value = new StringBuilder();
                }

                public abstract class Branch: Token
                {
                    public readonly List<Token> Items = new List<Token>();

                    public override string ToString() => string.Join(string.Empty, Items);
                }

                public sealed class Literal: Leaf
                {
                    public override string ToString() => Value.ToString();
                }

                public sealed class VariableSubstitution: Leaf
                {
                    public override string ToString() => "${" + Value.ToString() + "}";
                }

                public sealed class String: Branch
                {
                    public override string ToString() => "\"" + base.ToString() + "\"";
                }

                public sealed class Command: Branch
                {
                }

                public sealed class Group: Branch
                {
                    public override string ToString() => "(" + base.ToString() + ")";
                }

                public sealed class CommandSubstitution: Branch
                {
                    public override string ToString() => "$(" + base.ToString() + ")";
                }

                public sealed class Space: Token
                {
                    public override string ToString() => " ";
                }

                public abstract class Delimiter: Token
                {
                }

                public sealed class Continue: Delimiter
                {
                    public override string ToString() => ";";
                }

                public sealed class StopOnFailure: Delimiter
                {
                    public override string ToString() => "&&";
                }

                public sealed class StopOnSuccess: Delimiter
                {
                    public override string ToString() => "||";
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
                        throw new FormatException($"Syntax error ({index}): invalid escape sequence {EscapeSymbol}");
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void RemoveTrailingSpace(Token.Branch group)
            {
                if (group.Items.Count > 0 && group.Items.Last() is Token.Space)
                    group.Items.RemoveAt(group.Items.Count - 1);
            }

            public static IEnumerator<string> Split(string value)
            {
                for (int i = 0; i < value.Length;)
                {
                    if (char.IsWhiteSpace(value[i]))
                    {
                        yield return " ";
                        do
                            ++i;
                        while (i < value.Length && char.IsWhiteSpace(value[i]));
                    }
                    else
                    {
                        var startIndex = i;
                        do
                            ++i;
                        while (i < value.Length && !char.IsWhiteSpace(value[i]));
                        yield return value.Substring(startIndex, i - startIndex);
                    }
                }
            }

            public static Token Parse(string input)
            {
                var stack = new Stack<Level>();
                var level = new Level(State.SeekingEndOfRoot, new Token.Group());
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
                                    throw new FormatException($"Syntax error ({index}): unexpected end of expression");

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
                                    throw new FormatException($"Syntax error ({index}): unexpected {ContinueSymbol}");

                                group.Items.Add(new Token.Continue());
                                index += LengthOf(ContinueSymbol);
                            }
                            else if (Match(StopOnFailureSymbol, input, index))
                            {
                                var group = level.Token as Token.Branch;
                                if (group.Items.Count == 0 || group.Items.Last() is Token.Delimiter)
                                    throw new FormatException($"Syntax error ({index}): unexpected {StopOnFailureSymbol}");

                                group.Items.Add(new Token.StopOnFailure());
                                index += LengthOf(StopOnFailureSymbol);
                            }
                            else if (Match(StopOnSuccessSymbol, input, index))
                            {
                                var group = level.Token as Token.Branch;
                                if (group.Items.Count == 0 || group.Items.Last() is Token.Delimiter)
                                    throw new FormatException($"Syntax error ({index}): unexpected {StopOnSuccessSymbol}");

                                group.Items.Add(new Token.StopOnSuccess());
                                index += LengthOf(StopOnSuccessSymbol);
                            }
                            else if (Match(GroupOpenSymbol, input, index))
                            {
                                var group = level.Token as Token.Branch;
                                if (group.Items.Count > 0 && !(group.Items.Last() is Token.Delimiter))
                                    throw new FormatException($"Syntax error ({index}): unexpected {GroupOpenSymbol}");

                                level = NextLevel(stack, level, State.SeekingEndOfGroup, new Token.Group());
                                index += LengthOf(GroupOpenSymbol);
                            }
                            else if (Match(GroupCloseSymbol, input, index))
                            {
                                throw new FormatException($"Syntax error ({index}): unexpected {GroupCloseSymbol}");
                            }
                            else
                            {
                                var group = level.Token as Token.Branch;
                                if (group.Items.Count > 0 && !(group.Items.Last() is Token.Delimiter))
                                    throw new FormatException($"Syntax error ({index}): delimiter expected");

                                level = NextLevel(stack, level, State.SeekingEndOfCommandInRoot, new Token.Command());
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
                                level = NextLevel(stack, level, State.SeekingEndOfSpace, new Token.Space());
                                index++;
                            }
                            else if (Match(SingleQuoteSymbol, input, index))
                            {
                                level = NextLevel(stack, level, State.SeekingSingleQuote, new Token.Literal());
                                index += LengthOf(SingleQuoteSymbol);
                            }
                            else if (Match(DoubleQuoteSymbol, input, index))
                            {
                                level = NextLevel(stack, level, State.SeekingDoubleQuote, new Token.String());
                                index += LengthOf(DoubleQuoteSymbol);
                            }
                            else if (Match(VariableSubstitutionOpenSymbol, input, index))
                            {
                                level = NextLevel(stack, level, State.SeekingEndOfVariableSubstitution, new Token.VariableSubstitution());
                                index += LengthOf(VariableSubstitutionOpenSymbol);
                            }
                            else if (Match(CommandSubstitutionOpenSymbol, input, index))
                            {
                                level = NextLevel(stack, level, State.SeekingEndOfCommandSubstitution, new Token.CommandSubstitution());
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
                                level = NextLevel(stack, level, State.SeekingEndOfLiteralInRoot, new Token.Literal());
                            }
                            break;
                        case State.SeekingSingleQuote:
                            if (index == input.Length)
                            {
                                throw new FormatException($"Syntax error ({index + 1}): {SingleQuoteSymbol} expected");
                            }
                            else if (Match(SingleQuoteSymbol, input, index))
                            {
                                level = stack.Pop();
                                index += LengthOf(SingleQuoteSymbol);
                            }
                            else
                            {
                                var node = level.Token as Token.Leaf;
                                node.Value.Append(input[index]);
                                index++;
                            }
                            break;
                        case State.SeekingDoubleQuote:
                            if (index == input.Length)
                            {
                                throw new FormatException($"Syntax error ({index + 1}): {DoubleQuoteSymbol} expected");
                            }
                            else if (Match(DoubleQuoteSymbol, input, index))
                            {
                                level = stack.Pop();
                                index += LengthOf(DoubleQuoteSymbol);
                            }
                            else if (Match(VariableSubstitutionOpenSymbol, input, index))
                            {
                                level = NextLevel(stack, level, State.SeekingEndOfVariableSubstitution, new Token.VariableSubstitution());
                                index += LengthOf(VariableSubstitutionOpenSymbol);
                            }
                            else if (Match(CommandSubstitutionOpenSymbol, input, index))
                            {
                                level = NextLevel(stack, level, State.SeekingEndOfCommandSubstitution, new Token.CommandSubstitution());
                                index += LengthOf(CommandSubstitutionOpenSymbol);
                            }
                            else
                            {
                                level = NextLevel(stack, level, State.SeekingEndOfLiteralInComposition, new Token.Literal());
                            }
                            break;
                        case State.SeekingEndOfGroup:
                        case State.SeekingEndOfCommandSubstitution:
                            if (index == input.Length)
                            {
                                throw new FormatException($"Syntax error ({index + 1}): {GroupCloseSymbol} expected");
                            }
                            else if (char.IsWhiteSpace(input[index]))
                            {
                                index++;
                            }
                            else if (Match(ContinueSymbol, input, index))
                            {
                                var group = level.Token as Token.Branch;
                                if (group.Items.Count == 0 || group.Items.Last() is Token.Delimiter)
                                    throw new FormatException($"Syntax error ({index}): unexpected {ContinueSymbol}");

                                group.Items.Add(new Token.Continue());
                                index += LengthOf(ContinueSymbol);
                            }
                            else if (Match(StopOnFailureSymbol, input, index))
                            {
                                var group = level.Token as Token.Branch;
                                if (group.Items.Count == 0 || group.Items.Last() is Token.Delimiter)
                                    throw new FormatException($"Syntax error ({index}): unexpected {StopOnFailureSymbol}");

                                group.Items.Add(new Token.StopOnFailure());
                                index += LengthOf(StopOnFailureSymbol);
                            }
                            else if (Match(StopOnSuccessSymbol, input, index))
                            {
                                var group = level.Token as Token.Branch;
                                if (group.Items.Count == 0 || group.Items.Last() is Token.Delimiter)
                                    throw new FormatException($"Syntax error ({index}): unexpected {StopOnSuccessSymbol}");

                                group.Items.Add(new Token.StopOnSuccess());
                                index += LengthOf(StopOnSuccessSymbol);
                            }
                            else if (Match(GroupOpenSymbol, input, index))
                            {
                                var group = level.Token as Token.Branch;
                                if (group.Items.Count > 0 && !(group.Items.Last() is Token.Delimiter))
                                    throw new FormatException($"Syntax error ({index}): unexpected {GroupOpenSymbol}");

                                level = NextLevel(stack, level, State.SeekingEndOfGroup, new Token.Group());
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
                                    throw new FormatException($"Syntax error ({index}): delimiter expected");

                                level = NextLevel(stack, level, State.SeekingEndOfCommandInGroup, new Token.Command());
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
                                level = NextLevel(stack, level, State.SeekingEndOfSpace, new Token.Space());
                                index++;
                            }
                            else if (Match(SingleQuoteSymbol, input, index))
                            {
                                level = NextLevel(stack, level, State.SeekingSingleQuote, new Token.Literal());
                                index += LengthOf(SingleQuoteSymbol);
                            }
                            else if (Match(DoubleQuoteSymbol, input, index))
                            {
                                level = NextLevel(stack, level, State.SeekingDoubleQuote, new Token.String());
                                index += LengthOf(DoubleQuoteSymbol);
                            }
                            else if (Match(VariableSubstitutionOpenSymbol, input, index))
                            {
                                level = NextLevel(stack, level, State.SeekingEndOfVariableSubstitution, new Token.VariableSubstitution());
                                index += LengthOf(VariableSubstitutionOpenSymbol);
                            }
                            else if (Match(CommandSubstitutionOpenSymbol, input, index))
                            {
                                level = NextLevel(stack, level, State.SeekingEndOfCommandSubstitution, new Token.CommandSubstitution());
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
                                level = NextLevel(stack, level, State.SeekingEndOfLiteralInGroup, new Token.Literal());
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
                                var node = level.Token as Token.Leaf;
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
                                var node = level.Token as Token.Leaf;
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
                                var node = level.Token as Token.Leaf;
                                node.Value.Append(input[index]);
                                index++;
                            }
                            break;
                        case State.SeekingEndOfVariableSubstitution:
                            if (index == input.Length)
                            {
                                throw new FormatException($"Syntax error ({index + 1}): {IdentifierCloseSymbol} expected");
                            }
                            else if (Match(IdentifierCloseSymbol, input, index))
                            {
                                var node = level.Token as Token.Leaf;
                                if (node.Value.Length == 0)
                                    throw new FormatException($"Syntax error ({index + 1}): invalid identifier");

                                level = stack.Pop();
                                index += LengthOf(IdentifierCloseSymbol);
                            }
                            else
                            {
                                var c = input[index];
                                var node = level.Token as Token.Leaf;
                                if (!char.IsLetter(c) && c != '_' && !(char.IsDigit(c) && node.Value.Length > 0))
                                    throw new FormatException($"Syntax error ({index + 1}): invalid identifier");
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
        }

        protected override bool Transient => true;

        private static string prompt = ">";
        public static string Prompt
        {
            get => prompt;
            set => prompt = value ?? string.Empty;
        }

        private static Assembly[] assemblies = Array.Empty<Assembly>();
        public static Assembly[] Assemblies
        {
            get => assemblies;
            set => assemblies = value ?? Array.Empty<Assembly>();
        }

        public sealed class Writer
        {
            public readonly TextWriter Out;
            public readonly TextWriter Error;

            public Writer() => (Out, Error) = (new StringWriter(), new StringWriter());
            public Writer(TextWriter output, TextWriter error) => (Out, Error) = (output, error);
            public Writer(StringBuilder output, StringBuilder error) => (Out, Error) = (new StringWriter(output), new StringWriter(error));
        }

        protected interface IHistory
        {
            bool IsFirst { get; }
            bool IsLast { get; }

            string GoToFirst();
            string GoToLast();
            string GoToNext();
            string GoToPrevious();

            void Add(string value);
            void Clear();
        }

        private sealed class LimitedHistory: IHistory
        {
            private readonly LinkedListNodePool<string> pool;
            private readonly LinkedList<string> items;

            private LinkedListNode<string> line;

            public int Capacity
            {
                get => pool.Capacity;
                set => pool.Capacity = value;
            }

            public int Count => items.Count;

            public bool IsFirst => line == items.First;
            public bool IsLast => line == items.Last;

            public LimitedHistory(int capacity = int.MaxValue)
            {
                items = new LinkedList<string>();
                pool = new LinkedListNodePool<string>(capacity);
            }

            public void Add(string value)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    var node = pool.Take();
                    node.Value = value;
                    items.AddLast(node);
                    line = null;
                }
            }

            public void Clear()
            {
                var node = items.First;
                while (node != null)
                {
                    pool.Return(node);
                    node = node.Next;
                }
                items.Clear();
                line = null;
            }

            public string GoToFirst() => (line = items.First)?.Value;
            public string GoToLast() => (line = items.Last)?.Value;

            public string GoToNext()
            {
                if (line == null)
                    return null;

                return (line.Next != null) ? (line = line.Next)?.Value : line.Value;
            }

            public string GoToPrevious()
            {
                if (line == null)
                    return (line = items.Last)?.Value;

                return (line.Previous != null) ? (line = line.Previous)?.Value : line.Value;
            }

            public LinkedList<string>.Enumerator GetEnumerator() => items.GetEnumerator();
        }

        private Coroutine reader;

        protected IHistory History { get; private set; }

        public Dictionary<string, string> Variables { get; private set; }

#if UNITY_EDITOR
        protected override void Reset()
        {
            History?.Clear();
            Variables?.Clear();            
        }

        protected override void OnValidate() { }
#endif

        protected override void OnSingletonAwake()
        {
            base.OnSingletonAwake();

            Shell.RegisterCommands(assemblies);

            Variables = new Dictionary<string, string>();
            History = new LimitedHistory(64);
        }

        protected virtual void Start()
        {
            reader = StartCoroutine(ReadInput());
        }

        protected abstract bool TryReadLine(out string value);

        protected virtual void AutoComplete(string prefix, out ArraySegment<string> completions) => Shell.FindCommandNamesStartingWith(prefix, out completions);

        private IEnumerator ReadInput()
        {
            System.Console.Write(prompt);
            while (true)
            {
                if (TryReadLine(out string value))
                {
                    System.Console.WriteLine();
                    yield return Process(value).Catch(Print);
                    System.Console.Write(prompt);
                }

                yield return null;
            }
        }

        private IEnumerator Process(string input)
        {
            if (Parser.Parse(input) is Parser.Token.Group group)
            {
                var writer = new Writer(System.Console.Out, System.Console.Error);
                yield return Process(group, writer).Catch(Print);
            }
        }

        private enum ProcessCondition
        {
            None,
            Success,
            Failure
        }

        private IEnumerator Process(Parser.Token.Group group, Writer writer)
        {
            var exception = default(Exception);
            var condition = ProcessCondition.None;

            foreach (var item in group.Items)
            {

                if (item is Parser.Token.Group subgroup)
                {
                    if ((condition == ProcessCondition.None)
                     || (condition == ProcessCondition.Success && exception == null)
                     || (condition == ProcessCondition.Failure && exception != null))
                    {
                        exception = null;
                        yield return Process(subgroup, writer).Catch(e => exception = e);
                    }
                }
                else if (item is Parser.Token.Command command)
                {
                    if ((condition == ProcessCondition.None)
                     || (condition == ProcessCondition.Success && exception == null)
                     || (condition == ProcessCondition.Failure && exception != null))
                    {
                        exception = null;
                        yield return Process(command, writer).Catch(e => exception = e);
                    }
                }
                else if (item is Parser.Token.Continue)
                {
                    condition = ProcessCondition.None;
                }
                else if (item is Parser.Token.StopOnFailure)
                {
                    condition = ProcessCondition.Success;
                }
                else if (item is Parser.Token.StopOnSuccess)
                {
                    condition = ProcessCondition.Failure;
                }
            }

            if (exception != null)
                throw exception;
        }

        private IEnumerator Process(Parser.Token.Command command, Writer writer)
        {
            var splitted = new List<string>();
            var argument = new StringBuilder();
            foreach (var item in command.Items)
            {
                switch (item)
                {
                    case Parser.Token.Literal literal:
                        argument.Append(literal.Value);
                        break;
                    case Parser.Token.VariableSubstitution variableSubstitution:
                        if (Variables.TryGetValue(variableSubstitution.Value.ToString(), out string value))
                            using (var enumerator = Parser.Split(value))
                            {
                                while (enumerator.MoveNext())
                                {
                                    var s = enumerator.Current;
                                    if (string.IsNullOrWhiteSpace(s))
                                    {
                                        if (argument.Length > 0)
                                        {
                                            splitted.Add(argument.ToString());
                                            argument.Clear();
                                        }
                                    }
                                    else
                                    {
                                        argument.Append(s);
                                    }
                                }
                            }
                        break;
                    case Parser.Token.String composition:
                        {
                            IEnumerator wait = Process(composition, new StringWriter(argument));
                            if (wait != null)
                                yield return wait;
                        }
                        break;
                    case Parser.Token.Space space:
                        if (argument.Length > 0)
                        {
                            splitted.Add(argument.ToString());
                            argument.Clear();
                        }
                        break;
                    case Parser.Token.CommandSubstitution commandSubstitution:
                        yield return null;
                        break;
                    default:
                        break;
                }
            }

            // Add last argument if any
            if (argument.Length > 0)
                splitted.Add(argument.ToString());

            if (splitted?.Count > 0)
            {
                var key = splitted[0];
                if (Shell.TryGetCommandDelegate(key, out Shell.CommandDelegate handler))
                {
                    History.Add(key);
                    var ret = handler(writer, splitted.Skip(1).ToArray());
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
                else
                {
                    Shell.FindCommandNamesStartingWith(key, out ArraySegment<string> completions);
                    if (completions.Count == 0)
                        throw new UnityException($"{key}: command not found");

                    History.Add(key);
                    var n = completions.Offset + completions.Count;
                    for (int i = completions.Offset; i < n; ++i)
                        writer.Out.WriteLine(completions.Array[i]);                    
                }
            }
        }

        private IEnumerator Process(Parser.Token.String composition, TextWriter writer)
        {
            foreach (var item in composition.Items)
            {
                switch (item)
                {
                    case Parser.Token.Literal literal:
                        writer.Write(literal.Value);
                        break;
                    case Parser.Token.VariableSubstitution variable:
                        if (Variables.TryGetValue(variable.Value.ToString(), out string value))
                            writer.Write(value);
                        break;
                    case Parser.Token.CommandSubstitution command:
                        yield return null;
                        break;
                    default:
                        break;
                }
            }
        }

        private static void Print(Exception e)
        {
            var msg = e.Message;
            if (!string.IsNullOrEmpty(msg))
            {
                var color = System.Console.ForegroundColor;
                System.Console.ForegroundColor = ConsoleColor.Red;
                System.Console.Error.WriteLine(msg);
                System.Console.ForegroundColor = color;
            }
        }

    }
}
