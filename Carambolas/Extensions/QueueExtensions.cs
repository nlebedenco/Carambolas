using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Carambolas
{
    public static class QueueExtensions
    {
        public static bool TryDequeue<T>(this Queue<T> q, out T value)
        {
            if (q.Count > 0)
            {
                value = q.Dequeue();
                return true;
            }

            value = default;
            return false;
        }
    }
}
