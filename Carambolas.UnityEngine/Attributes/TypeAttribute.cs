using System;

using UnityEngine;

namespace Carambolas.UnityEngine
{
    public class TypeAttribute: PropertyAttribute
    {
        public Type Type { get; private set; }

        public TypeAttribute(Type type) => Type = type;        
    }
}
