using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Carambolas
{
    public static class QueueExtensions
    {
        public static bool TryDequeue<T>(this Queue<T> queue, out T value)
        {
            if (queue.Count > 0)
            {
                value = queue.Dequeue();
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryPeek<T>(this Queue<T> queue, out T value)
        {
            if (queue.Count == 0)
            {
                value = default;
                return false;
            }

            value = queue.Peek();
            return true;
        }
    }
}
