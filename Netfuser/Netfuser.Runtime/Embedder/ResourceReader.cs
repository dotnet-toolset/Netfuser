using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Netfuser.Runtime.Embedder.Compression;
using Netfuser.Runtime.Embedder.Encryption;

namespace Netfuser.Runtime.Embedder
{
    /// <summary>
    /// This class reads resources that were previously embedded by the Netfuser's IEmbedder.
    /// </summary>
    public class ResourceReader
    {
        /// <summary>
        /// Name of the resource bundle
        /// </summary>
        public readonly string Name;
        /// <summary>
        /// Assembly where this resource bundle is found
        /// </summary>
        public readonly Assembly Assembly;
        /// <summary>
        /// Entries in this resource bundle
        /// </summary>
        public readonly IReadOnlyList<ResourceEntry> Entries;
        /// <summary>
        /// Resource entries indexed by name
        /// </summary>
        public readonly ILookup<string, ResourceEntry> Index;

        private ResourceReader(string name, Assembly ass, IReadOnlyList<ResourceEntry> entries)
        {
            Name = name;
            Assembly = ass;
            Entries = entries;
            Index = entries.ToLookup(e => e.Name);
        }

        /// <summary>
        /// Open the given resource entry as a readable stream
        /// </summary>
        /// <param name="entry">resource entry</param>
        /// <returns>readable stream or <see langword="null"/> if no such resource is found</returns>
        public Stream Open(ResourceEntry entry)
        {
            if (entry == null) return null;
            var stream = Assembly.GetManifestResourceStream(entry.ResourceName);
            if (entry.Encryption != null && int.TryParse(entry.Compression, out var rid))
            {
                var ct = Assembly.ManifestModule.ResolveType(rid);
                var decryptor = (IDecryptor)Activator.CreateInstance(ct);
                stream = decryptor.Decrypt(stream);
            }
            if (entry.Compression != null && int.TryParse(entry.Compression, out rid))
            {
                var ct = Assembly.ManifestModule.ResolveType(rid);
                var decompressor = (IDecompressor)Activator.CreateInstance(ct);
                stream = decompressor.Decompress(stream);
            }

            return stream;
        }

        /// <summary>
        /// Open the given resource entry as a readable stream
        /// </summary>
        /// <param name="name">name of the resource</param>
        /// <returns>readable stream or <see langword="null"/> if no such resource is found</returns>
        public Stream Open(string name)
        {
            if (!Index.Contains(name)) return null;
            return Open(Index[name].FirstOrDefault());
        }

        /// <summary>
        /// Read the given resource as byte array
        /// </summary>
        /// <param name="name">name of the resource</param>
        /// <returns>array of bytes or <see langword="null"/> if no such resource is found</returns>
        public byte[] GetBytes(string name)
        {
            byte[] result = null;
            using (var stream = Open(name))
                if (stream != null)
                {
                    if (!(stream is MemoryStream mem))
                    {
                        mem = new MemoryStream();
                        stream.CopyTo(mem);
                    }
                    result = mem.ToArray();
                }
            return result;
        }

        /// <summary>
        /// Read the given resource as byte array
        /// </summary>
        /// <param name="re">resource entry</param>
        /// <returns>array of bytes or <see langword="null"/> if no such resource is found</returns>
        public byte[] GetBytes(ResourceEntry re)
        {
            byte[] result = null;
            using (var stream = Open(re))
                if (stream != null)
                {
                    if (!(stream is MemoryStream mem))
                    {
                        mem = new MemoryStream();
                        stream.CopyTo(mem);
                    }
                    result = mem.ToArray();
                }
            return result;
        }

        class CacheKey
        {
            private readonly Assembly _ass;
            private readonly string _name;

            public CacheKey(Assembly ass, string name)
            {
                _ass = ass;
                _name = name;
            }

            public override int GetHashCode() => _ass.GetHashCode() ^ _name.GetHashCode();

            public override bool Equals(object obj) =>
                obj is CacheKey k && Equals(_ass, k._ass) && Equals(_name, k._name);
        }

        private static readonly Dictionary<CacheKey, ResourceReader> Cache = new Dictionary<CacheKey, ResourceReader>();

        private static ResourceReader TryCreate(string name, Assembly ass)
        {
            var names = new HashSet<string>(ass.GetManifestResourceNames());
            if (!names.Contains(name))
                return null;
            using (var stream = ass.GetManifestResourceStream(name))
                if (stream != null)
                {
                    var root = XDocument.Load(stream)?.Root;
                    if (root?.Name != "index") return null;
                    var entries = new List<ResourceEntry>();
                    foreach (var node in root.Elements("entry"))
                    {
                        var entry = new ResourceEntry();
                        foreach (var attr in node.Attributes())
                            entry[attr.Name.LocalName] = attr.Value;
                        if (entry.ResourceName != null && entry.Name != null && names.Contains(name))
                            entries.Add(entry);
                    }
                    if (entries.Count > 0)
                        return new ResourceReader(name, ass, entries);
                }

            return null;
        }

        /// <summary>
        /// Get or create the instance of resource reader for the given name and assembly
        /// </summary>
        /// <param name="name">name of the resource bundle</param>
        /// <param name="ass">assembly where the resource is located (executing assembly by default)</param>
        /// <returns>the instance of resource reader</returns>
        public static ResourceReader GetInstance(string name, Assembly ass = null)
        {
            if (ass == null)
                ass = Assembly.GetExecutingAssembly();
            lock (Cache)
            {
                var key = new CacheKey(ass, name);
                if (!Cache.TryGetValue(key, out var reader))
                    Cache.Add(key, reader = TryCreate(name, ass));
                return reader;
            }
        }
    }
}