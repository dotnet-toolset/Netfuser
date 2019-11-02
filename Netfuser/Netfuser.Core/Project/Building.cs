namespace Netfuser.Core.Project
{
    public enum Building
    {
        /// <summary>
        /// Don't build project - assume it is ready for processing
        /// </summary>
        No,
        /// <summary>
        /// Build project using MSBbuild/devenv /Build command
        /// </summary>
        Build,
        /// <summary>
        /// Rebuild project using MSBbuild/devenv /Rebuild command
        /// </summary>
        Rebuild,
        /// <summary>
        /// Remove all files under project's and referenced projects' bin and obj folders, then do regular build
        /// USE WITH CAUTION! Files will be removed recursively without any prompts.
        /// </summary>
        CleanBuild
    }
}