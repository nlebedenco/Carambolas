using System;
using System.Collections.Generic;

using Carambolas.Internal;

namespace Carambolas
{
    public static class Enum<T>
    {
        private static readonly Dictionary<T, string> dict = new Dictionary<T, string>();

        static Enum()
        {
            var type = typeof(T);
            if (type.IsEnum)
                throw new TypeAccessException(string.Format(Resources.GetString(Strings.Enum.TypeIsNotEnum), type.FullName));

            var names = Enum.GetNames(type);
            var values = Enum.GetValues(type);

            for (int i = 0; i < values.Length; i++)
                dict[(T)values.GetValue(i)] = names[i];
        }

        public static string GetName(T value) => dict[value];
    }
}
