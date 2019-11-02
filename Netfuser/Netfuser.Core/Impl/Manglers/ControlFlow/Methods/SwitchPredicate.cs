using Base.Rng;
using dnlib.DotNet.Emit;
using Netfuser.Core.Impl.Manglers.Ints;
using Netfuser.Core.Manglers.ControlFlow;
using Netfuser.Core.Manglers.Values;
using Netfuser.Dnext;
using Netfuser.Dnext.Cil;

namespace Netfuser.Core.Impl.Manglers.ControlFlow.Methods
{
    abstract class SwitchPredicate
    {
        private readonly ICFMangleContext _context;

        protected SwitchPredicate(ICFMangleContext technique)
        {
            _context = technique;
        }

        public abstract void EmitSwitchLoad(CilFragment instrs);
        public abstract int GetSwitchKey(int key);

        internal class Xor : SwitchPredicate
        {
            private readonly int _xorKey;

            public Xor(ICFMangleContext ctx)
                : base(ctx)
            {
                _xorKey = ctx.Mangler.Rng.NextInt32();
            }

            public override void EmitSwitchLoad(CilFragment instrs)
            {
                instrs.Add(Instruction.Create(OpCodes.Ldc_I4, _xorKey));
                instrs.Add(Instruction.Create(OpCodes.Xor));
            }

            public override int GetSwitchKey(int key) => key ^ _xorKey;
        }

        internal class Expr : SwitchPredicate
        {
            private readonly ICodec _codec;
            private readonly CilFragment _invCompiled;
            private readonly Local _stateVar;

            public Expr(ICFMangleContext ctx)
                : base(ctx)
            {
                _invCompiled = new CilFragment();
                _stateVar = new Local(ctx.Method.Module.CorLibTypes.Int32);
                var body = ctx.Method.Body;
                body.Variables.Add(_stateVar);
                body.InitLocals = true;
                var nm = new IntGenerator(ctx.Mangler.Rng, ctx.Mangler.Options.MaxMangleIterations);
                var codec = nm.Generate();
                codec.ParameterResolver = p => p == nm.Argument ? _stateVar : null;
                _codec = codec;
                var emitter = DnextFactory.NewILEmitter(ctx.Method.Module, ctx.Importer, _invCompiled);
                codec.EmitDemangler(emitter);
            }

            public override void EmitSwitchLoad(CilFragment instrs)
            {
                instrs.Add(Instruction.Create(OpCodes.Stloc, _stateVar));
                foreach (var instr in _invCompiled)
                    instrs.Add(instr.Clone());
            }

            public override int GetSwitchKey(int key) => (int)_codec.Mangle(key);
        }
    }
}