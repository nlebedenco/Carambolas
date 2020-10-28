using System;

namespace Carambolas.UnityEngine
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class IconAttribute: Attribute
    {
        public string Name { get; private set; }

        public IconAttribute(string name) => Name = name;
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class ProSkinIconAttribute: Attribute
    {
        public string Name { get; private set; }

        public ProSkinIconAttribute(string name) => Name = name;
    }
}
