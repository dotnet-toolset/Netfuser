using System.Resources;

namespace Netfuser.Core.Embedder
{
    /// <summary>
    /// This plugin allows to embed resources in the target assembly.
    /// Compression and encryption of the resources are supported.
    /// This is a named plugin, and the name has special meaning.
    /// The instance of the plugin with the name <see cref="NetfuserFactory.EmbedderIndexName"/> 
    /// embeds assemblies referenced by the source modules and injects code needed to
    /// load embedded assemblies on demand at runtime.
    /// Other names may be used freely for other types of resources.
    /// Every instance of <see cref="IEmbedder"/> creates separate resource bundle 
    /// (that is different from standard resource bundles created by .NET compiler).
    /// Every resource bundle has XML-encoded manifest that contains important properties
    /// of the resource entries (name as seen by the .NET code, name in the .NET resource table, 
    /// encryption and/or compression algorithms (if applicable), etc.)
    /// Use <see cref="ResourceReader"/> to read these resource bundles and 
    /// individual resource entries from within the target assembly at runtime.
    /// </summary>
    public interface IEmbedder : INamedPlugin
    {
        /// <summary>
        /// Add resource to this bundle
        /// </summary>
        /// <param name="embedding">resource entry</param>
        void Add(Embedding embedding);
    }
}