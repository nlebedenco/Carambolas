/* Based on an article by Jake Ginnivan at: http://jake.ginnivan.net/c-sharp-argument-parser/
 * which in turn was based on a previous article by GriffonRL at: http://www.codeproject.com/KB/recipes/command_line.aspx
 * 
 *
 * Use cases:
 *
 * Argument: -flag 
 * Usage: args.Contains("flag"); 
 * Result: true
 * Usage: args.HasValues("flag"); 
 * Result: false 
 * 
 * Argument: -arg "My Value" 
 * Usage: args.TryGetValue("arg", out string result); 
 * Result: "My Value"
 * 
 * Argument: /arg=Value /arg=Value2 
 * Usage: args["arg"] 
 * Result: StringCollection {"Value", "Value2"} 
 * 
 * Argument: /arg="Value,Value2" 
 * Usage: args["arg"] 
 * Result: StringCollection {"Value", "Value2"} 
 * 
 */ 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Carambolas
{
    using StringCollection = List<string>;

    /// <summary>
    /// CommandLineArguments class
    /// </summary>
    public class CommandLineArguments
    {
        /// <summary>
        /// Splits the command line. When main(string[] args) is used escaped quotes (ie a path "c:\folder\") 
        /// will consume all the following command line arguments as the one argument. 
        /// This function ignores escaped quotes making handling paths much easier.
        /// </summary>
        private static string[] SplitCommandLine(string commandLine)
        {
            var translatedArguments = new StringBuilder(commandLine);
            var escaped = false;
            for (var i = 0; i < translatedArguments.Length; i++)
            {
                if (translatedArguments[i] == '"')
                    escaped = !escaped;

                if (translatedArguments[i] == ' ' && !escaped)
                    translatedArguments[i] = '\n';
            }

            var toReturn = translatedArguments.ToString().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < toReturn.Length; i++)
                toReturn[i] = RemoveMatchingQuotes(toReturn[i]);

            return toReturn;
        }

        private static string RemoveMatchingQuotes(string stringToTrim)
        {
            var firstQuoteIndex = stringToTrim.IndexOf('"');
            var lastQuoteIndex = stringToTrim.LastIndexOf('"');
            while (firstQuoteIndex != lastQuoteIndex)
            {
                stringToTrim = stringToTrim.Remove(firstQuoteIndex, 1);
                stringToTrim = stringToTrim.Remove(lastQuoteIndex - 1, 1); //-1 because we've shifted the indicies left by one
                firstQuoteIndex = stringToTrim.IndexOf('"');
                lastQuoteIndex = stringToTrim.LastIndexOf('"');
            }

            return stringToTrim;
        }

        private readonly Dictionary<string, StringCollection> dictionary;
        private string waitingArgument;

        public CommandLineArguments() : this(Environment.CommandLine) { }

        public CommandLineArguments(string commandLine) : this(SplitCommandLine(commandLine)) { }

        public CommandLineArguments(IEnumerable<string> arguments)
        {
            dictionary = new Dictionary<string, StringCollection>();

            string[] parts;

            //Splits on beginning of arguments ( - and -- and / )
            //And on assignment operators ( = and : )
            var argumentSplitter = new Regex(@"^-{1,2}|^/|=", RegexOptions.IgnoreCase);

            foreach (var argument in arguments)
            {
                parts = argumentSplitter.Split(argument, 3);
                switch (parts.Length)
                {
                    case 1:
                        AddValueToWaitingArgument(parts[0]);
                        break;
                    case 2:
                        AddWaitingArgumentAsFlag();

                        //Because of the split index 0 will be a empty string
                        waitingArgument = parts[1];
                        break;
                    case 3:
                        AddWaitingArgumentAsFlag();

                        //Because of the split index 0 will be a empty string
                        string valuesWithoutQuotes = RemoveMatchingQuotes(parts[2]);

                        Add(parts[1], valuesWithoutQuotes.Split(','));
                        break;
                }
            }

            AddWaitingArgumentAsFlag();
        }

        private void AddWaitingArgumentAsFlag()
        {
            if (waitingArgument == null)
                return;

            Add(waitingArgument);
            waitingArgument = null;
        }

        private void AddValueToWaitingArgument(string value)
        {
            if (waitingArgument == null)
                return;

            value = RemoveMatchingQuotes(value);

            Add(waitingArgument, value);
            waitingArgument = null;
        }

        public int Count => dictionary.Count;

        public StringCollection this[string argument] => dictionary.TryGetValue(argument, out StringCollection values) ? values : null;

        public void Add(string argument)
        {
            if (!dictionary.TryGetValue(argument, out StringCollection list))
                dictionary[argument] = null;
        }

        public void Add(string argument, string value)
        {
            if (!dictionary.TryGetValue(argument, out StringCollection list) || list == null)
                dictionary[argument] = list = new StringCollection() { value };

            list.Add(value);
        }

        public void Add(string argument, IEnumerable<string> values)
        {
            foreach (var value in values)
                Add(argument, value);
        }

        public void Remove(string argument) => dictionary.Remove(argument);

        public void Remove(string argument, string value)
        {
            if (dictionary.TryGetValue(argument, out StringCollection list))
                list?.Remove(value);
        }

        public void Remove(string argument, IEnumerable<string> values)
        {
            foreach (var value in values)
                Remove(argument, value);
        }

        public void Set(string argument, string value) => dictionary[argument] = new StringCollection() { value };

        public void Set(string argument) => dictionary[argument] = null;

        public bool Contains(string argument) => dictionary.ContainsKey(argument);

        public bool HasValues(string argument) => dictionary.TryGetValue(argument, out StringCollection values) ? values?.Count > 0 : false;

        public bool TryGetValues(string argument, out StringCollection values) => dictionary.TryGetValue(argument, out values);

        public bool TryGetValue(string argument, out string value)
        {
            if (dictionary.TryGetValue(argument, out StringCollection list) && list != null)
            {
                value = list.LastOrDefault();
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }
    }
}
