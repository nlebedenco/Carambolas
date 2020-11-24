using System;
using System.Collections.Generic;
using System.Text;

namespace Carambolas
{
    public static class ArraySegmentExtensions
    {
        public static void Deconstruct<T>(this ArraySegment<T> self, out T[] array, out int offset, out int count)
        {
            array = self.Array;
            offset = self.Offset;
            count = self.Count;
        }
    }
}
