using System.Collections.Generic;
using dnlib.DotNet.Emit;

namespace Netfuser.Dnext.Cil.Graph
{
    /// <summary>
    ///     A Control Flow Graph (CFG) of a method
    /// </summary>
    public interface ICfGraph : IReadOnlyList<CfBlock>
    {
        /// <summary>
        ///     Gets the <see cref="CfBlock" /> of the specified id.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <returns>The block with specified id.</returns>
        new CfBlock this[int id] { get; }


        /// <summary>
        ///     Gets the corresponding method body.
        /// </summary>
        /// <value>The method body.</value>
        CilBody Body { get; }


        /// <summary>
        ///     Gets the block containing the specified instruction.
        /// </summary>
        /// <param name="instr">The instruction.</param>
        /// <returns>The block containing the instruction.</returns>
        CfBlock GetContainingBlock(Instruction instr);

        /// <summary>
        ///     Computes a key sequence that is valid according to the execution of the CFG.
        /// </summary>
        /// <remarks>
        ///     The caller can utilize the information provided by this classes to instruments state machines.
        ///     For example:
        ///     <code>
        /// int state = 4;
        /// for (int i = 0 ; i &lt; 10; i++) {
        ///     state = 6;
        ///     if (i % 2 == 0) {
        ///         state = 3;
        ///     else {
        ///         // The state varaible is guaranteed to be 6 in here.
        ///     }
        /// }
        ///     </code>
        /// </remarks>
        /// <returns>The generated key sequence of the CFG.</returns>
        CfBlockKey[] ComputeKeys();
    }
}