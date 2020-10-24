using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using UnityObject = UnityEngine.Object;

namespace Carambolas.UnityEngine
{
    public static class ObjectExtensions
    {
        public static object OrNull(this object o) => o is null || (o as UnityObject) == null ? null : o;

        public static bool IsNullOrDestroyed(this object o) => o is null || (o as UnityObject) == null;

        #region Serialization 

        public static string ToJson(this object obj) => JsonUtility.ToJson(obj, false);

        public static string ToPrettyJson(this object obj) => JsonUtility.ToJson(obj, true);

        public static void FromJson(this object obj, string json) => JsonUtility.FromJsonOverwrite(json, obj);

        public static byte[] ToBinary(this object obj) => BinaryUtility.ToBinary(obj);

        public static void FromBinary(this object obj, byte[] binary) => BinaryUtility.FromBinaryOverwrite(binary, obj);

        #endregion 
    }
}
