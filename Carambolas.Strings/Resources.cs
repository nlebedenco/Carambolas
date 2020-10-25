using System;
using System.Runtime.CompilerServices;

namespace Carambolas.Internal
{
    public interface IResourcesProvider
    {
        string GetString(string term);
    }

    public static class Resources
    {
        private sealed class NullProvider: IResourcesProvider
        {
            public string GetString(string term) => null;
        }

        public static readonly IResourcesProvider DefaultProvider = new NullProvider();

        private static IResourcesProvider provider;

        public static IResourcesProvider Provider
        {
            get => provider;
            set => provider = value ?? DefaultProvider;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetString(string term) => provider.GetString(term) ?? term;
    }
}
