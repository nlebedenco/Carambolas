using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Carambolas
{
    public static class StringExtensions
    {
        /// <summary>
        /// Check is this string contains a substring with a particular case sensitivity 
        /// </summary>
        /// <param name="self"></param>
        /// <param name="value"></param>
        /// <param name="comparisonType"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains(this string self, string value, StringComparison comparisonType) => self.IndexOf(value, comparisonType) >= 0;

        /// <summary>
        /// Adds a space between words in camel cased text
        /// </summary>
        /// <param name="self"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string FormatCamelCase(this string self) => Regex.Replace(Regex.Replace(self, @"(\P{Ll})(\P{Ll}\p{Ll})", "$1 $2"), @"(\p{Ll})(\P{Ll})", "$1 $2");

        /// <summary>
        /// Creates a new string by removing all non alpha numeric characters.
        /// </summary>
        /// <param name="self"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string RemoveNonAlphaNumeric(this string self) => Regex.Replace(self, "[^a-zA-Z0-9_.]+", string.Empty, RegexOptions.Compiled);

        // private static Regex csvSplit = new Regex("(?:^|,)(\"(?:[^\"])*\"|[^,]*)", RegexOptions.Compiled);

        /// <summary>
        /// Splits a string into substrings using the specified delimiter (default to a comma (,)). 
        /// If a substring must contain the delimiter it can be quoted using double-quotes (") to avoid being split. 
        /// </summary>
        public static List<string> SplitCharacterSeparatedValues(this string self, char separator = ',')
        {
            var pattern = string.Format("(?:^|{0})(\"(?:[^\"])*\"|[^{0}]*)", separator);
            var regex = new Regex(pattern);
            List<string> list = new List<string>();
            foreach (Match match in regex.Matches(self))
            {
                string s = match.Value;
                if (s.Length == 0)
                    list.Add(string.Empty);

                list.Add(s.TrimStart(',').Trim('"'));
            }

            return list;
        }

        private static readonly Regex ssvSplit = new Regex("(?:^|\\s*)(\"(?:[^\"])*\"|[^\\s]+)", RegexOptions.Compiled);

        /// <summary>
        /// Splits a string into substrings using any white space as delimiter. 
        /// If a substring must contain the delimiter it can be quoted using double-quotes (") to avoid being split. 
        /// </summary>
        public static List<string> SplitSpaceSeparatedValues(this string self)
        {
            List<string> list = new List<string>();
            foreach (Match match in ssvSplit.Matches(self))
            {
                string s = match.Value;
                if (s.Length == 0)
                    list.Add(string.Empty);

                list.Add(s.TrimStart().Trim('"'));
            }

            return list;
        }
    }
}
