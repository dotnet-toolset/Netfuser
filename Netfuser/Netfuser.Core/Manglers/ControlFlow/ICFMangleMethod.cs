using Netfuser.Dnext.Cil;

namespace Netfuser.Core.Manglers.ControlFlow
{
    /// <summary>
    /// Every control flow mangling method must implement this interface.
    /// </summary>
    public interface ICFMangleMethod : INamedPlugin
    {
        /// <summary>
        /// Instance of <see cref="ICFMangler"/> plugin
        /// </summary>
        ICFMangler Mangler { get; }

        /// <summary>
        /// The actual mangling is performed here, the mangler may re-arrange instructions 
        /// within the given block the way it likes, add new instructions etc.
        /// </summary>
        /// <param name="context">mangling context</param>
        /// <param name="block">block to be mangled</param>
        void Mangle(ICFMangleContext context, Block.Regular block);
    }
}
