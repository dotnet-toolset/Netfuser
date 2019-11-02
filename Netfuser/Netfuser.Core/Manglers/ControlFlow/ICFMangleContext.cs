using System.Collections.Generic;
using Base.Collections.Props;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Netfuser.Dnext.Cil;

namespace Netfuser.Core.Manglers.ControlFlow
{
    /// <summary>
    /// This context is created for each method that is to be obfuscated
    /// </summary>
    public interface ICFMangleContext : IPropsContainer
    {
        /// <summary>
        /// Instance of the <see cref="ICFMangler"/> plugin
        /// </summary>
        ICFMangler Mangler { get; }

        /// <summary>
        /// Method to be obfuscated
        /// </summary>
        MethodDef Method { get; }

        /// <summary>
        /// This importer is to be used to reference metadata elements from CIL
        /// </summary>
        Importer Importer { get; }

        /// <summary>
        /// CIL body of the method to be obfuscated
        /// </summary>
        CilBody MethodBody { get; }

        /// <summary>
        /// Root block of the method to be obfuscated
        /// <see cref="Dnext.Cil.Block"/> for details
        /// </summary>
        Block.Root RootBlock { get; }

        /// <summary>
        /// Splits the given block into smaller fragments that may be re-arranged in the block
        /// </summary>
        /// <param name="block">block of code</param>
        /// <returns>list of generated fragments</returns>
        LinkedList<CilFragment> SplitFragments(Block.Regular block);
        void AddJump(CilFragment instrs, Instruction target, bool stackEmpty);
    }
}
