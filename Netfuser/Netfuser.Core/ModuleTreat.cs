namespace Netfuser.Core
{
    /// <summary>
    /// Tells Netfuser what to do with the module
    /// </summary>
    public enum ModuleTreat
    {
        /// <summary>
        /// Don't do anything with the module
        /// </summary>
        Ignore,
        /// <summary>
        /// Copy assembly that contains the module to the target folder
        /// </summary>
        Copy,
        /// <summary>
        /// Merge module with the target
        /// </summary>
        Merge,
        /// <summary>
        /// Embed module with the target
        /// </summary>
        Embed
    }
}