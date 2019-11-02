namespace Netfuser.Core.Naming
{
    /// <summary>
    /// Fixes names of .NET resources in the target module.
    /// Names of .NET resources are tied to names of some other metadata members,
    /// so we need to keep these in sync if we obfuscate metadata.
    /// This plugin is added to the context automatically, but may be removed if 
    /// the project doesn't use .NET resources
    /// </summary>
    public interface IResNameFixer : IPlugin
    {
    }
}