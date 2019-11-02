namespace Netfuser.Core.Project
{
    /// <summary>
    /// Plugin that is able to load .csproj files
    /// </summary>
    public interface IProjectLoader : IPlugin
    {
        /// <summary>
        /// Project options
        /// </summary>
        ProjectOptions Options { get; }
    }
}