using System;
using System.IO;

using UnityEngine;

namespace Carambolas.UnityEngine
{
    [Icon("Icons/small-n-flat/png/72/file-exe")]
    public abstract class ConfigAsset: ScriptableObject, IRevertableObject
    {
        public const string DefaultFileExtension = "cfg";

        private string fileName;
        public string FileName
        {
            get
            {
                if (string.IsNullOrEmpty(fileName))
                    fileName = Storage.GenerateFileName(GetType(), DefaultFileExtension);

                return fileName;
            }
        }

        private string defaultValues;

        protected virtual void OnEnable()
        {
            TryLoad(FileName, Storage.Space.AppData);

            defaultValues = this.ToJson();

            TryLoad(FileName, Storage.Space.LocalAppData);
        }

#if UNITY_EDITOR

        protected virtual void Reset()
        {
            // HACK: Workaround for Unity insisting on clearing the scriptable object's name when reset is called in the inspector.
            name = Path.GetFileNameWithoutExtension(UnityEditor.AssetDatabase.GetAssetPath(this));
        }

#endif

        protected virtual void OnValidate() { }

        public virtual void RevertToDefaults()
        {
            if (!string.IsNullOrEmpty(defaultValues))
                this.FromJson(defaultValues);
        }

        public bool TryLoad(string path, Storage.Space space) => TryLoad(path, space, JsonSerializer.Default);
        public bool TryLoad(string path, Storage.Space space, ISerializer serializer) => TryLoad(Storage.ResolvePath(path, space), serializer);

        public bool TryLoad(string path) => TryLoad(path, JsonSerializer.Default);
        public bool TryLoad(string path, ISerializer serializer)
        {
            if (Storage.File.Exists(path))
            {
                try
                {
                    Load(path, serializer);
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            return false;
        }

        public bool TrySave(string path, Storage.Space space) => TrySave(path, space, JsonSerializer.Pretty);
        public bool TrySave(string path, Storage.Space space, ISerializer serializer) => TrySave(Storage.ResolvePath(path, space), serializer);

        public bool TrySave(string path) => TrySave(path, JsonSerializer.Pretty);
        public bool TrySave(string path, ISerializer serializer)
        {
            try
            {
                Storage.Save(this, path, serializer);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            return false;
        }

        public void Load() => Load(FileName, Storage.Space.LocalAppData);

        public void Load(string path, Storage.Space space) => Load(path, space, JsonSerializer.Default);
        public void Load(string path, Storage.Space space, ISerializer serializer) => Load(Storage.ResolvePath(path, space), serializer);

        public void Load(string path) => Load(path, JsonSerializer.Default);
        public void Load(string path, ISerializer serializer)
        {
            Storage.Load(path, serializer, this);
            Validate();
        }

        public void Load(Stream stream) => Load(stream, JsonSerializer.Default);
        public void Load(Stream stream, ISerializer serializer)
        {
            serializer.Deserialize(stream, this);
            Validate();
        }

        public void Load(TextReader reader)
        {
            JsonUtility.Load(reader, this);
            Validate();
        }

        public void Load(TextAsset asset)
        {
            JsonUtility.Load(asset, this);
            Validate();
        }

        public void Save() => Save(FileName, Storage.Space.LocalAppData);

        public void Save(string path, Storage.Space space) => Save(path, space, JsonSerializer.Pretty);
        public void Save(string path, Storage.Space space, ISerializer serializer) => Save(Storage.ResolvePath(path, space), serializer);
        public void Save(string path) => Save(path, JsonSerializer.Pretty);
        public void Save(string path, ISerializer serializer) => Storage.Save(this, path, serializer);

        public void Save(Stream stream) => Save(stream, JsonSerializer.Default);
        public void Save(Stream stream, ISerializer serializer) => serializer.Serialize(stream, this);

        public void Save(TextWriter writer, bool prettyPrint = false) => JsonUtility.Save(this, writer, prettyPrint);

        private void Validate()
        {
            try
            {
                OnValidate();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
