using System;

namespace Netfuser.Dnext.Cil.Graph
{
    /// <summary>
    ///     The type of Control Flow Block
    /// </summary>
    [Flags]
    public enum CfBlockType
    {
        /// <summary>
        ///     The block is a normal block
        /// </summary>
        Normal = 0,

        /// <summary>
        ///     There are unknown edges to this block. Usually used at exception handlers / method entry.
        /// </summary>
        Entry = 1,

        /// <summary>
        ///     There are unknown edges from this block. Usually used at filter blocks / throw / method exit.
        /// </summary>
        Exit = 2
    }
}