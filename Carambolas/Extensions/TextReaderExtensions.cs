using System;
using System.Collections.Generic;
using System.IO;

namespace Carambolas
{
    public static class TextReaderExtensions
    {
        public static IEnumerable<string> ReadLines(this TextReader source)
        {
            string line;
            while ((line = source.ReadLine()) != null)
                yield return line;
        }
    }
}
