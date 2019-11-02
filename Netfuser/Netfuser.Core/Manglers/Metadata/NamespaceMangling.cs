namespace Netfuser.Core.Manglers.Metadata
{
    /// <summary>
    /// Defines how the namespaces are renamed
    /// </summary>
    public enum NamespaceMangling
    {
        /// <summary>
        /// Treat namespace as a single name when renaming (for example, "original.name.space" will be renamed to "abc")
        /// </summary>
        Fold,
        /// <summary>
        /// Mangle each part of the namespace path separately ("original.name.space" => "ab.cd.xy")
        /// </summary>
        Parts,
        /// <summary>
        /// Place all types under a single namespace with empty name
        /// </summary>
        Empty,
        /// <summary>
        /// Pre-create X namespaces and randomly distribute approximately Y types to each namespace in such a way, 
        /// that X * Y ≈ total number of non-nested types in the target assembly, and X ≈ Y.
        /// In other words, if you have a total of 100 classes, approximately 10 namespaces with random names will be generated,
        /// and each namespace will get approximately 10 types in random order. This is totally independent of the
        /// original namespace names 
        /// </summary>
        Distribute
    }
}
