using System;

namespace Carambolas.UnityEngine
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class IconAttribute: Attribute
    {
        public string IconName { get; private set; }

        public IconAttribute(string iconName) => IconName = iconName;
    }
}
