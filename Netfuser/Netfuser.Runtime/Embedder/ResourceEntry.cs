using System.Collections.Generic;

namespace Netfuser.Runtime.Embedder
{
    /// <summary>
    /// Key:value dictionary that contains properties of a specific resource.
    /// </summary>
    public sealed class ResourceEntry : Dictionary<string, string>
    {
        public const string KeyName = "name";
        public const string KeyResourceName = "resource-name";
        public const string KeyEncryption = "encryption";
        public const string KeyCompression = "compression";

        /// <summary>
        /// Meaningful name of the resource, for example, assembly name for embedded assembly
        /// </summary>
        public string Name => GetOrDefault(KeyName);

        /// <summary>
        /// Name in the .net resource table
        /// </summary>
        public string ResourceName => GetOrDefault(KeyResourceName);

        /// <summary>
        /// Name of the encryption algorithm or null if no encryption
        /// </summary>
        public string Encryption => GetOrDefault(KeyEncryption);

        /// <summary>
        /// Name of the compression algorithm or null if no compression
        /// </summary>
        public string Compression => GetOrDefault(KeyCompression);

        public string GetOrDefault(string key)
            => TryGetValue(key, out var value) ? value : null;
    }
}