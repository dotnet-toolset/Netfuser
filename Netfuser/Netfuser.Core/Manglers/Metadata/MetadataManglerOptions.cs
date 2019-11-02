namespace Netfuser.Core.Manglers.Metadata
{

    /// <summary>
    /// Metadata mangler options
    /// </summary>
    public class MetadataManglerOptions
    {
        /// <summary>
        /// Metadata elements to obfuscate
        /// </summary>
        public MetaType Type = MetaType.All;
        
        /// <summary>
        /// Namespace mangling method
        /// </summary>
        public NamespaceMangling NamespaceMangling = NamespaceMangling.Distribute;
        
        /// <summary>
        /// Generator of mangled metadata names
        /// </summary>
        public NameGenerator Generator;
    }
}
