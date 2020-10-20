using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Carambolas
{
    public static class StackExtensions
    {
        public static bool TryPop<T>(this Stack<T> stack, out T value)
        {
            if (stack.Count == 0)
            {
                value = default(T);
                return false;
            }

            value = stack.Pop();
            return true;
        }
    }
}
