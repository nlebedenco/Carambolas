using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;

namespace Carambolas.UnityEngine
{
    public abstract class BinaryUtility
    {
        private static byte[] Compress(string text)
        {
            using (var compressed = new MemoryStream())
            {
                using (var gzip = new GZipStream(compressed, CompressionMode.Compress))
                {
                    var encoded = Encoding.UTF8.GetBytes(text);
                    gzip.Write(encoded, 0, encoded.Length);
                }

                return compressed.ToArray();
            }
        }

        private static string Decompress(byte[] data)
        {
            using (var compressed = new MemoryStream(data))
            {
                using (var gzip = new GZipStream(compressed, CompressionMode.Decompress))
                {
                    using (var reader = new StreamReader(gzip, Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FromBinary<T>(byte[] binary) => (T)FromBinary(binary, typeof(T));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object FromBinary(byte[] binary, Type type)
        {
            var obj = Activator.CreateInstance(type);
            FromBinaryOverwrite(binary, obj);
            return obj;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FromBinaryOverwrite(byte[] binary, object objectToOverwrite) => objectToOverwrite.FromJson(Decompress(binary));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] ToBinary(object obj) => Compress(obj.ToJson());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load(byte[] binary, object obj) => FromBinaryOverwrite(binary, obj);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load(Stream stream, object obj) => Load(stream.ReadAllBytes(), obj);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Load<T>(byte[] binary) => FromBinary<T>(binary);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Load<T>(Stream stream) => Load<T>(stream.ReadAllBytes());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Save(object obj, Stream stream)
        {
            var binary = ToBinary(obj);
            stream.Write(binary, 0, binary.Length);
        }
    }
}
