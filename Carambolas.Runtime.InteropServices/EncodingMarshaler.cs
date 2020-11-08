using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Carambolas.Runtime.InteropServices
{
    // Based on https://github.com/libgit2/libgit2sharp/blob/master/LibGit2Sharp/Core/EncodingMarshaler.cs published under the MIT License
    public abstract class EncodingMarshaler: ICustomMarshaler
    {
        private readonly Encoding encoding;

        protected EncodingMarshaler(Encoding encoding) => this.encoding = encoding;

        #region ICustomMarshaler

        public void CleanUpManagedData(object managedObj) { }

        public virtual void CleanUpNativeData(IntPtr ptr) => Cleanup(ptr);

        public int GetNativeDataSize() => -1; // Not a value type

        public virtual IntPtr MarshalManagedToNative(object managedObj)
        {
            if (managedObj is null)
                return IntPtr.Zero;

            if (managedObj is string str)
                return FromManaged(encoding, str);

            throw new MarshalDirectiveException(string.Format("{0} must be used on a string.", GetType().FullName));
        }

        public virtual object MarshalNativeToManaged(IntPtr ptr) => FromNative(encoding, ptr);

        #endregion

        public static unsafe IntPtr FromManaged(Encoding encoding, string value)
        {
            if (encoding == null || value == null)
                return IntPtr.Zero;

            var length = encoding.GetByteCount(value);
            var buffer = (byte*)Marshal.AllocHGlobal(length + 1).ToPointer();

            if (length > 0)
            {
                fixed (char* pValue = value)
                {
                    encoding.GetBytes(pValue, value.Length, buffer, length);
                }
            }

            buffer[length] = 0;

            return new IntPtr(buffer);
        }

        public static void Cleanup(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return;

            Marshal.FreeHGlobal(ptr);
        }

        public static unsafe string FromNative(Encoding encoding, IntPtr ptr) => (ptr == IntPtr.Zero) ? null : FromNative(encoding, (byte*)ptr);

        public static unsafe string FromNative(Encoding encoding, byte* data)
        {
            if (data == default(byte*))
                return null;

            var start = data;
            var walk = start;

            // Find the end of the string
            while (*walk != 0)
                walk++;

            if (walk == start)
                return "" ;

            return new string((sbyte*)data, 0, (int)(walk - start), encoding);
        }

        public static unsafe string FromNative(Encoding encoding, IntPtr ptr, int length)
        {
            if (ptr == IntPtr.Zero)
                return null;

            if (length == 0)
                return "";

            return new string((sbyte*)ptr.ToPointer(), 0, length, encoding);
        }

        public static string FromBuffer(Encoding encoding, byte[] buffer)
        {
            if (buffer == null)
                return null;

            var length = 0;
            var stop = buffer.Length;

            while (length < stop && buffer[length] != 0)
                length++;

            return FromBuffer(encoding, buffer, length);
        }

        public static string FromBuffer(Encoding encoding, byte[] buffer, int length)
        {
            Debug.Assert(buffer != null);

            if (length == 0)
                return "" ;

            return encoding.GetString(buffer, 0, length);
        }
    }
}
