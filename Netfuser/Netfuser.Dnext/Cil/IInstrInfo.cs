using System.Collections.Generic;
using dnlib.DotNet.Emit;

namespace Netfuser.Dnext.Cil
{
    public interface IInstrInfo
    {
        /// <summary>
        /// Stack depth before the instruction is executed
        /// </summary>
        int DepthBefore { get; }

        /// <summary>
        /// Stack depth after the instruction is executed
        /// </summary>
        int DepthAfter { get; }

        /// <summary>
        /// true if control may naturally be passed to the instruction
        /// false if the instruction preceeded by unconditional jump, throw, ret and there's no way to reach the instruction unless it is referenced elsewhere
        /// </summary>
        bool NaturalTarget { get; }

        /// <summary>
        /// Instructions that may transfer control to this instruction
        /// </summary>
        IReadOnlyList<Instruction> ReferencedBy { get; }

        /// <summary>
        /// Number of ways to transfer control to this instruction, including natural flow, if applicable
        /// </summary>
        int RefCount { get; }

        /// <summary>
        /// List of errors related to this instruction encountered when parsing method body
        /// </summary>
        IReadOnlyList<string> Errors { get; }
    }
}