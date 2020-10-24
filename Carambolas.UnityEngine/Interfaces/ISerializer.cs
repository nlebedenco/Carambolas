using System;
using System.IO;


namespace Carambolas.UnityEngine
{
    public interface ISerializer
    {
        void Deserialize(Stream stream, object obj);
        void Serialize(Stream stream, object obj);
    }

    public interface ISerializer<T>: ISerializer
    {
        T Deserialize(Stream stream);
        void Serialize(Stream stream, T obj);
    }
}
