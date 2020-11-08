using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Carambolas
{
    public static class AppDomainExtensions
    {
        public static IEnumerable<Type> GetAllTypesAssignableFrom<T>(this AppDomain self) => GetAllTypesAssignableFrom(self, typeof(T));
        public static IEnumerable<Type> GetAllTypesAssignableFrom(this AppDomain self, Type other) =>
            self.GetAssemblies()
                .SelectMany(assembly => assembly.GetAllTypesAssignableFrom(other), (assembly, type) => type);

        public static IEnumerable<Type> GetAllSubclassesOf<T>(this AppDomain self) => GetAllSubclassesOf(self, typeof(T));
        public static IEnumerable<Type> GetAllSubclassesOf(this AppDomain self, Type other) =>
            (!other.IsGenericTypeDefinition || !other.IsGenericType)
                ? self.GetAssemblies()
                    .SelectMany(assembly => assembly.GetAllSubclassesOf(other), (assembly, type) => type).Where(type => !type.IsAbstract)
                : self.GetAssemblies()
                    .SelectMany(assembly => assembly.GetAllTypesImplementingOpenGenericType(other), (assembly, type) => type).Where(type => !type.IsAbstract);
    }
}
