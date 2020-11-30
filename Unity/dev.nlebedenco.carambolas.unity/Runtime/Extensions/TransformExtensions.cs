using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Carambolas.UnityEngine
{
    public static class TransformExtensions
    {
        /// <summary>
        /// Recursively searches for a transform mapped to a given bone id. 
        /// </summary>
        /// <param name="self"></param>
        /// <param name="humanBoneId"></param>
        /// <returns> Transform of the bone or null </returns>
        public static Transform FindTransform(this Transform self, HumanBodyBones humanBoneId)
        {
            var animator = self.gameObject.GetComponent<Animator>();
            #pragma warning disable IDE0031
            return animator == null ? null : animator.GetBoneTransform(humanBoneId);
            #pragma warning restore IDE0031
        }

        /// <summary>
        /// Recursively searches for a transform given the name and returns it if found. 
        /// </summary>
        /// <param name="self"> Parent to search through </param>
        /// <param name="name"> Bone to find </param>
        /// <returns> Transform of the bone or null </returns>
        public static Transform FindTransform(this Transform self, string name)
        {
            if (name == self.name)
                return self;

            // Handle the case where the bone name is nested in a namespace
            var index = self.name.IndexOf(':');
            if (index >= 0)
            {
                if (name == self.name.Substring(index + 1))
                    return self;
            }

            // Since we didn't find it, check the children
            for (int i = 0; i < self.childCount; i++)
            {
                var child = self.GetChild(i).FindTransform(name);
                if (child != null)
                    return child;
            }

            return null;
        }

        /// <summary>
        /// Get transform position with an offset
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 PositionWithOffset(this Transform self, in Vector3 offset, Space offsetSpace = Space.Self) => offsetSpace == Space.World ? self.position + offset : self.TransformPoint(offset);
    }
}
