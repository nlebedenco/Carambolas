using System;

namespace Carambolas
{
    public static class ByteArrayExtensions
    {    
        public static string ToHex(this byte[] array) => BitConverter.ToString(array);
        public static string ToHex(this byte[] array, int startIndex, int length) => BitConverter.ToString(array, startIndex, length);
    }
}
