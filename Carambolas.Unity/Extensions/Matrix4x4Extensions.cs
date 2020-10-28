using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Carambolas.UnityEngine
{
    public static class Matrix4x4Extensions
    {
        /// <summary>
        /// Return the position of the matrix
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 Position(this in Matrix4x4 self) => self.GetColumn(3);

        /// <summary>
        /// Return the rotation of the matrix
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion Rotation(this in Matrix4x4 self) => Quaternion.LookRotation(self.GetColumn(2), self.GetColumn(1));

        /// <summary>
        /// Return the scale of the matrix
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 Scale(this in Matrix4x4 self) => new Vector3(self.GetColumn(0).magnitude, self.GetColumn(1).magnitude, self.GetColumn(2).magnitude);
    }
}
