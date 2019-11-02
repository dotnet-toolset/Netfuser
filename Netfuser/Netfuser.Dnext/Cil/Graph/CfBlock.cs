using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet.Emit;

namespace Netfuser.Dnext.Cil.Graph
{
    /// <summary>
    /// A block in Control Flow Graph (CFG).
    /// </summary>
    public class CfBlock
    {
        private readonly List<CfBlock> _sources, _targets;
        private readonly List<ExceptionHandler> _handlers;

        /// <summary>
        ///     The footer instruction
        /// </summary>
        public readonly Instruction Footer;

        /// <summary>
        ///     The header instruction
        /// </summary>
        public readonly Instruction Header;

        /// <summary>
        ///     The identifier of this block
        /// </summary>
        public readonly int Id;

        /// <summary>
        ///     The type of this block
        /// </summary>
        public readonly CfBlockType Type;

        /// <summary>
        ///     Gets the source blocks of this control flow block.
        /// </summary>
        /// <value>The source blocks.</value>
        public IReadOnlyList<CfBlock> Sources => _sources;

        /// <summary>
        ///     Gets the target blocks of this control flow block.
        /// </summary>
        /// <value>The target blocks.</value>
        public IReadOnlyList<CfBlock> Targets => _targets;

        public IReadOnlyList<ExceptionHandler> Handlers => _handlers;

        internal CfBlock(int id, CfBlockType type, Instruction header, Instruction footer)
        {
            Id = id;
            Type = type;
            Header = header;
            Footer = footer;
            _sources = new List<CfBlock>();
            _targets = new List<CfBlock>();
            _handlers = new List<ExceptionHandler>();
        }

        /// <summary>
        ///     Returns a <see cref="System.String" /> that represents this block.
        /// </summary>
        /// <returns>A <see cref="System.String" /> that represents this block.</returns>
        public override string ToString() =>
            $"Block {Id} => {Type} {string.Join(", ", Targets.Select(block => block.Id.ToString()).ToArray())}";

        internal void AddSource(CfBlock block) => _sources.Add(block);
        internal void AddTarget(CfBlock block) => _targets.Add(block);
        internal void AddHandler(ExceptionHandler handler) => _handlers.Add(handler);
    }
}