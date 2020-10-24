using System;
using System.IO;
using System.Runtime.CompilerServices;

using UnityEngine;

using UnityJsonUtility = UnityEngine.JsonUtility;

namespace Carambolas.UnityEngine
{
    public abstract class JsonUtility
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FromJson<T>(string json) => UnityJsonUtility.FromJson<T>(json);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object FromJson(string json, Type type) => UnityJsonUtility.FromJson(json, type);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FromJsonOverwrite(string json, object objectToOverwrite) => UnityJsonUtility.FromJsonOverwrite(json, objectToOverwrite);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToJson(object obj) => UnityJsonUtility.ToJson(obj);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToJson(object obj, bool prettyPrint) => UnityJsonUtility.ToJson(obj, prettyPrint);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load(string json, object obj) => FromJsonOverwrite(json, obj);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load(TextAsset asset, object obj) => Load(asset.text, obj);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load(TextReader reader, object obj) => Load(reader.ReadToEnd(), obj);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load(Stream stream, object obj)
        {
            using (var reader = new StreamReader(stream))
                Load(reader, obj);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Load<T>(string json) => FromJson<T>(json);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Load<T>(TextAsset asset) => Load<T>(asset.text);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Load<T>(TextReader reader) => Load<T>(reader.ReadToEnd());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Load<T>(Stream stream)
        {
            using (var reader = new StreamReader(stream))
                return Load<T>(reader);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Save(object obj, TextWriter writer, bool prettyPrint = false)
        {
            writer.Write(ToJson(obj, prettyPrint));
            writer.Flush();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Save(object obj, Stream stream, bool prettyPrint = false)
        {
            using (var writer = new StreamWriter(stream))
                Save(obj, writer, prettyPrint);
        }
    }
}
