using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Carambolas
{
    public static class StringBuilderExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StringBuilder Replace(this StringBuilder builder, int index, int length, string replacement) => builder.Remove(index, length).Insert(index, replacement);
    }

}
