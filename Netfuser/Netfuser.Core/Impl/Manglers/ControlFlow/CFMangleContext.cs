using System.Collections.Generic;
using Base.Collections.Props;
using Base.Lang;
using Base.Rng;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using Netfuser.Core.Manglers.ControlFlow;
using Netfuser.Dnext;
using Netfuser.Dnext.Cil;

namespace Netfuser.Core.Impl.Manglers.ControlFlow
{
    class CFMangleContext : Disposable, ICFMangleContext
    {
        public ICFMangler Mangler { get; }

        public MethodDef Method { get; }

        public Importer Importer { get; }

        public CilBody MethodBody { get; }

        public Block.Root RootBlock { get; }

        public CFMangleContext(CFManglerPlugin mangler, MethodDef method, Importer importer)
        {
            Mangler = mangler;
            Method = method;
            Importer = importer;
            MethodBody = Method.Body;
            RootBlock = MethodBody.ParseBlocks();
        }

        public void AddJump(CilFragment instrs, Instruction target, bool stackEmpty)
        {
            if (stackEmpty)
            {
                instrs.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                instrs.Add(Instruction.Create(OpCodes.Brfalse, target));
            }
            else
                instrs.Add(Instruction.Create(OpCodes.Br, target));
        }

        public LinkedList<CilFragment> SplitFragments(Block.Regular block)
        {
            var statements = new LinkedList<CilFragment>();
            var currentStatement = new CilFragment();
            var requiredInstr = new HashSet<Instruction>();
            var i = 0;
            var instr = block.Fragment.Instructions[i];
            var fi = RootBlock.FlowInfo.Info;
            while (instr != null)
            {
                fi.TryGetValue(instr, out var info);
                i++;
                var next = i < block.Fragment.Instructions.Count ? block.Fragment.Instructions[i] : null;
                currentStatement.Add(instr);

                var shouldSpilt = next != null && fi.TryGetValue(next, out var ni) && ni.RefCount > 1;
                switch (instr.OpCode.FlowControl)
                {
                    case FlowControl.Branch:
                    case FlowControl.Cond_Branch:
                    case FlowControl.Return:
                    case FlowControl.Throw:
                        shouldSpilt = true;
                        if (info.DepthAfter != 0)
                        {
                            if (instr.Operand is Instruction)
                                requiredInstr.Add((Instruction)instr.Operand);
                            else if (instr.Operand is Instruction[])
                                foreach (var target in (Instruction[])instr.Operand)
                                    requiredInstr.Add(target);
                        }
                        break;
                }
                requiredInstr.Remove(instr);
                if ((instr.OpCode.OpCodeType != OpCodeType.Prefix && info.DepthAfter == 0 && requiredInstr.Count == 0) &&
                    (shouldSpilt || Mangler.Options.Intensity > Mangler.Rng.NextDouble()))
                {
                    statements.AddLast(currentStatement);
                    currentStatement = new CilFragment();
                }
                instr = next;
            }

            if (!currentStatement.IsEmpty)
                statements.AddLast(currentStatement);

            return statements;
        }

        internal void Run()
        {
            var method = Mangler.Context.Plugins<ICFMangleMethod>().RandomElementOrDefault(Mangler.Rng);
            if (method == null) return;
            MethodBody.SimplifyBranches();
            foreach (var block in RootBlock.EnumRegular())
                method.Mangle(this, block);
            RootBlock.Write(MethodBody);
            MethodBody.KeepOldMaxStack = false;
            // dnlib will do this when writing the method, we can catch stack errors here
            if (Mangler.Context.DebugMode)
                MaxStackCalculator.GetMaxStack(MethodBody.Instructions, MethodBody.ExceptionHandlers);
            MethodBody.OptimizeBranches();
        }

        private volatile Props _props;
        Props IPropsContainer.GetProps(bool create)
        {
            if (_props != null || !create) return _props;
            lock (this)
                return _props ?? (_props = new Props());
        }

    }
}
