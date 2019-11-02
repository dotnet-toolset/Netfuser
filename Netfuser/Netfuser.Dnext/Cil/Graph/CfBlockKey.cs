namespace Netfuser.Dnext.Cil.Graph
{
    /// <summary>
    ///     The type of block in the key sequence
    /// </summary>
    public enum CfBlockKeyType
    {
        /// <summary>
        ///     The state key should be explicitly set in the block
        /// </summary>
        Explicit,

        /// <summary>
        ///     The state key could be assumed to be same as <see cref="CfBlockKey.EntryState" /> at the beginning of block.
        /// </summary>
        Incremental
    }

    /// <summary>
    ///     The information of the block in the key sequence
    /// </summary>
    public struct CfBlockKey
    {
        /// <summary>
        ///     The state key at the beginning of the block
        /// </summary>
        public uint EntryState;

        /// <summary>
        ///     The state key at the end of the block
        /// </summary>
        public uint ExitState;

        /// <summary>
        ///     The type of block
        /// </summary>
        public CfBlockKeyType Type;
    }
}