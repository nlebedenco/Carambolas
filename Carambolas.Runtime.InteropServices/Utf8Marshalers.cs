using System;
using System.Runtime.InteropServices;
using System.Text;

// Based on (https://github.com/libgit2/libgit2sharp/blob/master/LibGit2Sharp/Core/Utf8Marshaler.cs) published under the MIT License

namespace Carambolas.Runtime.InteropServices
{
    /// <summary>
    /// This marshaler is to be used for capturing a UTF-8 string owned by a native context and
    /// convert it to a managed string instance. The marshaler will not attempt to
    /// free the native pointer after conversion, because the memory is owned by the native context.
    /// <para/>
    /// Use this marshaler for return values, for example:
    /// [return: MarshalAs(UnmanagedType.CustomMarshaler,
    ///                    MarshalCookie = UniqueId.UniqueIdentifier,
    ///                    MarshalTypeRef = typeof(InputUtf8Marshaler))]
    /// </summary>
    public class InputUtf8Marshaler: InputUtf8AndFreeMarshaler
    {
        private static readonly InputUtf8Marshaler instance = new InputUtf8Marshaler();

        public new static ICustomMarshaler GetInstance(string cookie) => instance;

        #region ICustomMarshaler

        // Override cleanup so the pointer is not freed.
        public override void CleanUpNativeData(IntPtr ptr) { }

        #endregion
    }

    /// <summary>
    /// This marshaler is to be used for capturing a UTF-8 string allocated by a native context and
    /// convert it to a managed string instance. The marshaler will free the native pointer
    /// after conversion.
    /// </summary>
    public class InputUtf8AndFreeMarshaler: EncodingMarshaler
    {
        private static readonly Encoding encoding = new UTF8Encoding(false, false);
        private static readonly InputUtf8AndFreeMarshaler instance = new InputUtf8AndFreeMarshaler();        

        public InputUtf8AndFreeMarshaler() : base(encoding) { }

        public static ICustomMarshaler GetInstance(string cookie) => instance;

        #region ICustomMarshaler

        public override IntPtr MarshalManagedToNative(object managedObj)
            => throw new InvalidOperationException(string.Format("{0} cannot be used to pass data to a native context.", GetType().FullName));

        #endregion

        public static unsafe string FromNative(char* data) => FromNative(encoding, (byte*)data);

        public static string FromNative(IntPtr ptr) => FromNative(encoding, ptr);

        public static string FromNative(IntPtr ptr, int length) => FromNative(encoding, ptr, length);
        
        public static string FromBuffer(byte[] buffer) => FromBuffer(encoding, buffer);

        public static string FromBuffer(byte[] buffer, int length) => FromBuffer(encoding, buffer, length);
    }

    /// <summary>
    /// This marshaler is to be used for sending managed string instances to a native context.
    /// The marshaler will allocate a buffer in native memory to hold the UTF-8 string
    /// and perform the encoding conversion using that buffer as the target. The pointer
    /// received by the native context will be to this buffer. After the function call completes, 
    /// the native buffer is freed.
    /// <para/>
    /// Use this marshaler for function parameters, for example:
    /// [DllImport(libgit2)]
    /// internal static extern int git_tag_delete(RepositorySafeHandle repo,
    ///     [MarshalAs(UnmanagedType.CustomMarshaler
    ///                MarshalCookie = UniqueId.UniqueIdentifier,
    ///                MarshalTypeRef = typeof(OutputUtf8Marshaler))] String tagName);
    /// </summary>
    public class OutputUtf8Marshaler: EncodingMarshaler
    {
        private static readonly Encoding encoding = new UTF8Encoding(false, true);
        private static readonly OutputUtf8Marshaler instance = new OutputUtf8Marshaler();

        public OutputUtf8Marshaler() : base(encoding) { }

        public static ICustomMarshaler GetInstance(string cookie) => instance;

        #region ICustomMarshaler

        public override object MarshalNativeToManaged(IntPtr ptr)
            => throw new InvalidOperationException(string.Format("{0} cannot be used to retrieve data from a native context.", GetType().FullName));

        #endregion

        public static IntPtr FromManaged(string value) => FromManaged(encoding, value);
    }

}
