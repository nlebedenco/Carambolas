using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

// Based on https://github.com/daviddesmet/NaCl.Core

namespace Carambolas.Security.Cryptography.NaCl
{
    internal static class ArrayUtils
    {
        #region Individual

        /// <summary>
        /// Stores the value into the buffer.
        /// The value will be split into 8 bytes and put into eight sequential places in the output buffer, starting at the specified offset.
        /// </summary>
        /// <param name="buf">The output buffer.</param>
        /// <param name="offset">The output offset.</param>
        /// <param name="value">The input value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreUInt64LittleEndian(byte[] buf, int offset, ulong value)
        {
            buf[offset] = (byte)(value);
            buf[offset + 1] = (byte)(value >> 8);
            buf[offset + 2] = (byte)(value >> 16);
            buf[offset + 3] = (byte)(value >> 24);
            buf[offset + 4] = (byte)(value >> 32);
            buf[offset + 5] = (byte)(value >> 40);
            buf[offset + 6] = (byte)(value >> 48);
            buf[offset + 7] = (byte)(value >> 56);
        }

        #endregion

        #region Array

        #endregion      
    }
}
