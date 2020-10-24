using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Carambolas.UnityEngine
{
    /// <summary>
    /// Simple API for saving and loading data objects from an abstract data storage.
    /// </summary>
    public static class Storage
    {
        public enum Space
        {
            AppData = 0,
            LocalAppData
        }

        public static string AppDataPath => Application.dataPath;

        public static string LocalAppDataPath => Application.persistentDataPath;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GenerateFileName<T>(string extension = null) => GenerateFileName(typeof(T), extension);

        public static string GenerateFileName(Type type, string extension = null)
        {
            var attr = type.GetCustomAttribute<FileNameAttribute>();
            var fileName = attr?.FileName;
            if (string.IsNullOrEmpty(fileName))
                fileName = type.Name;

            return Path.HasExtension(fileName) ? fileName : Path.ChangeExtension(fileName, extension);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ResolvePath(string path, Space space) => Path.Combine(space == Space.LocalAppData ? LocalAppDataPath : AppDataPath, path);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Load<T>(string path, Space space) => Load<T>(path, space, JsonSerializer<T>.Default);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Load<T>(string path, Space space, ISerializer<T> serializer) => Load(ResolvePath(path, space), serializer);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Load<T>(string path) => Load<T>(path, JsonSerializer<T>.Default);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Load<T>(string path, ISerializer<T> serializer)
        {            
            using (var stream = System.IO.File.Open(path, FileMode.Open))
                return serializer.Deserialize(stream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load(string path, Space space, object obj) => Load(path, space, JsonSerializer.Default, obj);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load(string path, Space space, ISerializer serializer, object obj) => Load(ResolvePath(path, space), serializer, obj);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load(string path, object obj) => Load(path, JsonSerializer.Default, obj);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load(string path, ISerializer serializer, object obj)
        {
            using (var stream = System.IO.File.Open(path, FileMode.Open))
                serializer.Deserialize(stream, obj);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Save<T>(T obj, string path, Space space) => Save(obj, path, space, JsonSerializer<T>.Default);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Save<T>(T obj, string path, Space space, ISerializer<T> serializer) => Save(obj, ResolvePath(path, space), serializer);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Save<T>(T obj, string path) => Save(obj, path, JsonSerializer<T>.Default);

        public static void Save<T>(T obj, string path, ISerializer<T> serializer)
        {
            var directory = Path.GetDirectoryName(path);

            // If the directory doesn't already exist, try to create it
            if (!System.IO.Directory.Exists(directory))
                System.IO.Directory.CreateDirectory(directory);

            using (var stream = System.IO.File.Create(path))
                serializer.Serialize(stream, obj);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Save(object obj, string path, Space space) => Save(obj, path, space, JsonSerializer.Default);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Save(object obj, string path, Space space, ISerializer serializer) => Save(obj, ResolvePath(path, space), serializer);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Save(object obj, string path) => Save(obj, path, JsonSerializer.Default);
        public static void Save(object obj, string path, ISerializer serializer)
        {
            var directory = Path.GetDirectoryName(path);

            // If the directory doesn't already exist, try to create it
            if (!System.IO.Directory.Exists(directory))
                System.IO.Directory.CreateDirectory(directory);

            using (var stream = System.IO.File.Create(path))
                serializer.Serialize(stream, obj);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Exists(string path, Space space) => Exists(ResolvePath(path, space));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Exists(string path) => System.IO.File.Exists(path) || System.IO.Directory.Exists(path);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Delete(string path, Space space) => Delete(ResolvePath(path, space));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Delete(string path)
        {
            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
            else if (Directory.Exists(path))
                System.IO.Directory.Delete(path, true);
        }

        public class File
        {
            protected File() { }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool Exists(string path) => System.IO.File.Exists(path);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool Exists(string path, Space space) => Exists(ResolvePath(path, space));

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Delete(string path) => System.IO.File.Delete(path);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Delete(string path, Space space) => Delete(ResolvePath(path, Space.LocalAppData));
        }

        public class Directory
        {
            protected Directory() { }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool Exists(string path) => System.IO.Directory.Exists(path);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool Exists(string path, Space space) => Exists(ResolvePath(path, space));

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Delete(string path) => System.IO.Directory.Delete(path);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Delete(string path, Space space) => Delete(ResolvePath(path, Space.LocalAppData));

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Delete(string path, bool recursive) => System.IO.Directory.Delete(path, recursive);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Delete(string path, Space space, bool recursive) => Delete(ResolvePath(path, Space.LocalAppData), recursive);
        }
    }

    public class JsonSerializer: ISerializer
    {
        public static JsonSerializer Default = new JsonSerializer(false);
        public static JsonSerializer Pretty = new JsonSerializer(true);

        protected JsonSerializer(bool prettyPrint) => PrettyPrint = prettyPrint;
        

        public bool PrettyPrint { get; private set; }

        public void Deserialize(Stream stream, object obj) => JsonUtility.Load(stream, obj);

        public void Serialize(Stream stream, object obj) => JsonUtility.Save(obj, stream, PrettyPrint);
    }

    public class JsonSerializer<T>: ISerializer<T>
    {
        public static JsonSerializer<T> Default = new JsonSerializer<T>(false);
        public static JsonSerializer<T> Pretty = new JsonSerializer<T>(true);

        protected JsonSerializer(bool prettyPrint) => PrettyPrint = prettyPrint;

        public bool PrettyPrint { get; private set; }

        public T Deserialize(Stream stream) => JsonUtility.Load<T>(stream);
        public void Deserialize(Stream stream, object obj) => JsonUtility.Load(stream, obj);

        public void Serialize(Stream stream, T obj) => JsonUtility.Save(obj, stream, PrettyPrint);
        public void Serialize(Stream stream, object obj) => JsonUtility.Save(obj, stream, PrettyPrint);
    }


    public class BinarySerializer: ISerializer
    {
        public static BinarySerializer Default = new BinarySerializer();

        protected BinarySerializer() { }

        public void Deserialize(Stream stream, object obj) => BinaryUtility.Load(stream, obj);

        public void Serialize(Stream stream, object obj) => BinaryUtility.Save(obj, stream);
    }

    public class BinarySerializer<T>: ISerializer<T>
    {
        public static BinarySerializer<T> Standard = new BinarySerializer<T>();

        protected BinarySerializer() { }

        public T Deserialize(Stream stream) => BinaryUtility.Load<T>(stream);
        public void Deserialize(Stream stream, object obj) => BinaryUtility.Load(stream, obj);

        public void Serialize(Stream stream, T obj) => BinaryUtility.Save(obj, stream);
        public void Serialize(Stream stream, object obj) => BinaryUtility.Save(obj, stream);
    }
}
