using dnlib.DotNet;

namespace Netfuser.Dnext
{
    /// <summary>
    /// Full name of the type together with the name of module or assembly it is contained within.
    /// Used as a key in caches/dictionaries 
    /// </summary>
    public interface ITypeKey
    {
        IScope Scope { get; }
        string ScopeName { get; }
        string FullName { get; }
    }
}