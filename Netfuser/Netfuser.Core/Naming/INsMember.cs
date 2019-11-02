namespace Netfuser.Core.Naming
{
    /// <summary>
    /// Base interface for naming scopes and scope members that can potentially be naming scopes (e.g. classes).
    /// </summary>
    public interface INsMember
    {
        /// <summary>
        /// Parent of this naming scope or <see langword="null"/> if this is the root scope
        /// </summary>
        INsMember Parent { get; }
        
        /// <summary>
        /// New name of this scope, if renamed
        /// </summary>
        string NewName { get; }
    }
}
