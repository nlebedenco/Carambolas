using System;

namespace Carambolas.UnityEngine
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class FileNameAttribute: Attribute
    {
        public string FileName { get; private set; }

        public FileNameAttribute(string fileName) => FileName = fileName;
    }
}
