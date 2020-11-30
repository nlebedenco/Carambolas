using System;

using UnityEngine;

namespace Carambolas.UnityEngine
{
    public sealed class PrefabAttribute: PropertyAttribute
    {
        public Type ComponentType { get; set; }

        public PrefabAttribute(Type componentType = null) => ComponentType = componentType;
    }
}
