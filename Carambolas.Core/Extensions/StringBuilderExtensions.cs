using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace Carambolas
{
    public static class StringBuilderExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StringBuilder Replace(this StringBuilder builder, int index, int length, string replacement) => builder.Remove(index, length).Insert(index, replacement);
    }

}
