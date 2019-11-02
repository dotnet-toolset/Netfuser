using Base.Collections.Impl;
using dnlib.DotNet.Emit;

namespace Netfuser.Dnext.Cil
{
    /// <summary>
    /// Types of CIL block
    /// </summary>
    public enum BlockType
    {
        /// <summary>
        /// Regular block with CIL code or root block
        /// </summary>
        Regular,
        /// <summary>
        /// Code that is enclosed in the <c> try {} </c> block
        /// </summary>
        Try,
        /// <summary>
        /// Code that is enclosed in the <c> catch(SpecificException){} </c> block
        /// </summary>
        Handler,
        /// <summary>
        /// Code that is enclosed in the <c> finally {} </c> block
        /// </summary>
        Finally,
        /// <summary>
        /// Code in the <c>when()</c> clause
        /// </summary>
        Filter,
        /// <summary>
        /// Not actually used by c# compiler, but present in the CLI spec
        /// </summary>
        Fault
    }

    /// <summary>
    /// Base class for CIL blocks of code.
    /// A <c>Block</c> is a list of IL instructions that are within the boundaries 
    /// of exception handlers. In case of nested exception handlers, blocks contain nested blocks.
    /// </summary>
    public abstract class Block : AbstractNode<Block>
    {
        /// <summary>
        /// Type of this block
        /// </summary>
        public readonly BlockType Type;

        /// <summary>
        /// The first instruction in this block
        /// </summary>
        public abstract Instruction FirstInstr { get; }

        /// <summary>
        /// The last instruction in this block
        /// </summary>
        public abstract Instruction LastInstr { get; }

        protected Block(BlockType type)
        {
            Type = type;
        }

        protected abstract void Commit(CilBody body);

        /// <summary>
        /// Root block is the container for other types of blocks, there's only one root block per method
        /// </summary>
        public class Root : Block
        {
            public override Instruction FirstInstr => FirstChild.FirstInstr;
            public override Instruction LastInstr => LastChild.LastInstr;
            public IFlowInfo FlowInfo { get; internal set; }

            public Root()
                : base(BlockType.Regular)
            {
            }

            protected override void Commit(CilBody body)
            {
                foreach (var block in this)
                    block.Commit(body);
            }

            public void Write(CilBody body)
            {
                body.Instructions.Clear();
                Commit(body);
            }
        }

        /// <summary>
        /// This describes one of the <see cref="BlockType.Try">, <see cref="BlockType.Handler">, 
        /// <see cref="BlockType.Finally">, <see cref="BlockType.Filter"> or <see cref="BlockType.Fault"> blocks.
        /// This is the container for other blocks, either <see cref="Regular"/> (if there is at least 
        /// one CIL instruction immediately following the start of this block) or <see cref="Seh"/> if there's a nested exception handler
        /// </summary>
        public class Seh : Block
        {
            public readonly ExceptionHandler Handler;
            public override Instruction FirstInstr => FirstChild.FirstInstr;
            public override Instruction LastInstr => LastChild.LastInstr;

            public Seh(BlockType type, ExceptionHandler handler)
                : base(type) =>
                Handler = handler;

            private Block NextBlock()
            {
                Block t = this;
                while (t != null)
                {
                    if (t.NextSibling != null) return t.NextSibling;
                    t = t.ParentNode;
                }

                return null;
            }

            protected override void Commit(CilBody body)
            {
                switch (Type)
                {
                    case BlockType.Try:
                        Handler.TryStart = FirstInstr;
                        Handler.TryEnd =
                            NextBlock()?.FirstInstr; // End pointer is in fact the first instruction of the next block 
                        break;
                    case BlockType.Filter:
                        Handler.FilterStart = FirstInstr;
                        break;
                    default:
                        Handler.HandlerStart = FirstInstr;
                        Handler.HandlerEnd = NextBlock()?.FirstInstr;
                        break;
                }

                foreach (var block in this)
                    block.Commit(body);
            }
        }

        /// <summary>
        /// This is the regular block that contains actual instructions
        /// </summary>
        public class Regular : Block
        {
            /// <summary>
            /// Instructions in this block
            /// </summary>
            public readonly CilFragment Fragment;

            public override Instruction FirstInstr => Fragment.First;
            public override Instruction LastInstr => Fragment.Last;

            public Regular()
                : base(BlockType.Regular)
            {
                Fragment = new CilFragment();
            }

            protected override void Commit(CilBody body) => Fragment.Write(body);
        }
    }
}