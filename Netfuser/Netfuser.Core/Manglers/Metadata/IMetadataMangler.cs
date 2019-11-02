using Base.Rng;

namespace Netfuser.Core.Manglers.Metadata
{
    /// <summary>
    /// This plugin obfuscates metadata names (namespaces, types, methods, fields, properties, events etc.)
    /// </summary>
    public interface IMetadataMangler : IPlugin
    {
        /// <summary>
        /// Metadata mangler options
        /// </summary>
        MetadataManglerOptions Options { get; }
        
        /// <summary>
        /// Pseudo-random number generator for this mangler
        /// </summary>
        IRng Rng { get; }
    }
}
