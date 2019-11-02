using Base.Collections;
using dnlib.DotNet;

namespace Netfuser.Dnext.Hierarchy
{
    /// <summary>
    /// This interface helps to maintain consistency of method names when renaming virtual/abstract methods,
    /// implementations and declarations of interface methods.
    /// Whenever a method needs to be renamed, name chain for this method should be retrieved using this interface.
    /// If the name chain is not found, the method can be safely renamed.
    /// Otherwise, the DontRename field of the chain is checked first. If set, the method cannot be renamed.
    /// Otherwise, the NewName field must be checked. If set, the new name of the method must be set to NewName
    /// Otherwise, new unique name may be assigned to the method, and the NewName field of the chain must be set to
    /// this name (so that subsequent attempts to rename related methods use this name).
    /// </summary>
    public interface IVChains
    {
        IReadOnlySet<VChain> All();

        /// <summary>
        /// Retrieve name chain corresponding to the method
        /// </summary>
        /// <param name="method">method to get the name chain for</param>
        /// <returns></returns>
        VChain Get(IMethod method);
    }
}