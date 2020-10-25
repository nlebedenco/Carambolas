using System;
using System.IO;

using Carambolas.Internal;

namespace Carambolas
{
    public static class StreamExtensions
    {
        public static byte[] ReadAllBytes(this Stream stream)
        {
            var offset = 0;
            var length = stream.Length;
            var count = (int)length;
            var buffer = new byte[count];
            while (count > 0)
            {
                var num = stream.Read(buffer, offset, count);
                if (num == 0)
                    throw new EndOfStreamException(Resources.GetString(Strings.UnableToReadBeyondEndOfStream));
                offset += num;
                count -= num;
            }
            return buffer;
        }
    }
}
