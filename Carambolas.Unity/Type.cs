using System;
using System.Diagnostics;
using UnityEngine;

namespace Carambolas.UnityEngine
{
    [Serializable]
    public struct SerializableType: IEquatable<SerializableType>, ISerializationCallbackReceiver
    {
        [HideInInspector, SerializeField]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string assemblyQualifiedName;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Type type;

        public SerializableType(Type type) => (this.type, this.assemblyQualifiedName) = (type, null);

        #region Operators

        public static implicit operator Type(SerializableType x) => x.type;

        public static bool operator ==(SerializableType x, SerializableType y) => x.type == y.type;
        public static bool operator !=(SerializableType x, SerializableType y) => !(x == y);

        #endregion

        public override bool Equals(object obj) => obj is SerializableType && this == (SerializableType)obj;

        public bool Equals(SerializableType obj) => this == obj;

        public override int GetHashCode() => this.type?.GetHashCode() ?? 0;

        public override string ToString() => this.type?.ToString() ?? string.Empty;

        #region ISerializationCallbackReceiver

        void ISerializationCallbackReceiver.OnBeforeSerialize() => this.assemblyQualifiedName = this.type?.AssemblyQualifiedName;

        void ISerializationCallbackReceiver.OnAfterDeserialize() => (this.type, this.assemblyQualifiedName) = (Type.GetType(this.assemblyQualifiedName), null);

        #endregion
    }
}
