using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Carambolas
{
    public static class CustomAttributeExtensions
    {

        #region Methods that test for an attribute of a particular type 

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasAttribute(this Assembly element, Type attributeType) => Attribute.IsDefined(element, attributeType);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasAttribute(this Module element, Type attributeType) => Attribute.IsDefined(element, attributeType);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasAttribute(this MemberInfo element, Type attributeType) => Attribute.IsDefined(element, attributeType);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasAttribute(this ParameterInfo element, Type attributeType) => Attribute.IsDefined(element, attributeType);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasAttribute(this MemberInfo element, Type attributeType, bool inherit) => Attribute.IsDefined(element, attributeType, inherit);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasAttribute(this ParameterInfo element, Type attributeType, bool inherit) => Attribute.IsDefined(element, attributeType, inherit);

        #endregion

        #region Methods that test for an attribute of a particular type using generics

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasAttribute<T>(this Assembly element) => Attribute.IsDefined(element, typeof(T));
            
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasAttribute<T>(this Module element) => Attribute.IsDefined(element, typeof(T));
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasAttribute<T>(this MemberInfo element) => Attribute.IsDefined(element, typeof(T));
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasAttribute<T>(this ParameterInfo element) => Attribute.IsDefined(element, typeof(T));
        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasAttribute<T>(this MemberInfo element, bool inherit) => Attribute.IsDefined(element, typeof(T), inherit);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasAttribute<T>(this ParameterInfo element, bool inherit) => Attribute.IsDefined(element, typeof(T), inherit);

        #endregion
    }
}
