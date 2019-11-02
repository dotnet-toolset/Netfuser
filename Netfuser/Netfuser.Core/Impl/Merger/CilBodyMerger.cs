using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Pdb;

namespace Netfuser.Core.Impl.Merger
{
    class CilBodyMerger : BaseMerger
    {
        private readonly MethodDef _sourceMethod;
        private readonly MethodDef _targetMethod;
        private readonly Dictionary<Instruction, Instruction> _instrMap;
        private readonly TypeMapping _tm;
        private readonly NetfuserEvent.CilBodyBuilding _event;

        private CilBodyMerger(ContextImpl context, Importer importer, TypeMapping tm, MethodDef sourceMethod,
            MethodDef targetMethod)
            : base(context, importer)
        {
            _sourceMethod = sourceMethod;
            _targetMethod = targetMethod;
            _tm = tm;
            _instrMap = new Dictionary<Instruction, Instruction>();
            _event = new NetfuserEvent.CilBodyBuilding(context, _tm, _sourceMethod, _targetMethod, Importer, _instrMap);
        }

        Instruction Clone(Instruction instr)
        {
            Instruction result = null;
            switch (instr.OpCode.OperandType)
            {
                case OperandType.InlineType:
                    result = Instruction.Create(instr.OpCode, Importer.Import((ITypeDefOrRef)instr.Operand));
                    break;
                case OperandType.InlineField:
                    var f = (IField)instr.Operand;
                    result = Instruction.Create(instr.OpCode, Importer.Import(f));
                    break;
                case OperandType.InlineMethod:
                    var m = (IMethod)instr.Operand;
                    result = Instruction.Create(instr.OpCode, Importer.Import(m));
                    break;
                case OperandType.InlineTok:
                    switch (instr.Operand)
                    {
                        case ITypeDefOrRef t:
                            result = Instruction.Create(instr.OpCode, Importer.Import(t));
                            break;
                        case IMethod t:
                            result = Instruction.Create(instr.OpCode, Importer.Import(t));
                            break;
                        case IField t:
                            result = Instruction.Create(instr.OpCode, Importer.Import(t));
                            break;
                        default:
                            throw new NotSupportedException();
                    }

                    break;
                case OperandType.InlineVar:
                    switch (instr.Operand)
                    {
                        case Local l:
                            result = Instruction.Create(instr.OpCode, _event.Locals[l.Index]);
                            break;
                        case Parameter p:
                            result = Instruction.Create(instr.OpCode, _targetMethod.Parameters[p.Index]);
                            break;
                        default:
                            throw new NotSupportedException();
                    }

                    break;
                case OperandType.InlineSig:
                    var mr = (MethodSig)instr.Operand;
                    result = Instruction.Create(instr.OpCode, Importer.Import(mr));
                    break;
            }
            
            // we could have optimized here by referencing source instruction instead of creating new one.
            // however, it will confuse Pdb reader: if we modify instruction offset in the target method, Pdb reader will not find it.
            // it is possible that some other methods rely on the consistency of instructions in the source method, so we are sacrificing the memory here
            // just to be safe
            if (result == null)
                result = new Instruction(instr.OpCode, instr.Operand);

            result.SequencePoint = instr.SequencePoint;
            _instrMap.Add(instr, result);
            return result;
        }

        Instruction GetInstruction(Instruction source)
        {
            if (source == null) return null;
            return _instrMap.TryGetValue(source, out var result) ? result : source;
        }

        Local Clone(Local source)
        {
            return new Local(Importer.Import(source.Type), source.Name, source.Index);
        }

        ExceptionHandler Clone(ExceptionHandler source)
        {
            var result = new ExceptionHandler(source.HandlerType)
            {
                CatchType = Importer.Import(source.CatchType),
                TryStart = GetInstruction(source.TryStart),
                TryEnd = GetInstruction(source.TryEnd),
                HandlerStart = GetInstruction(source.HandlerStart),
                HandlerEnd = GetInstruction(source.HandlerEnd),
                FilterStart = GetInstruction(source.FilterStart)
            };
            return result;
        }

        void FixBrTargets(IEnumerable<Instruction> instructions)
        {
            foreach (var instr in instructions)
                switch (instr.OpCode.OperandType)
                {
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.InlineBrTarget:
                        instr.Operand = GetInstruction((Instruction)instr.Operand);
                        break;
                    case OperandType.InlineSwitch:
                        instr.Operand = ((Instruction[])instr.Operand).Select(GetInstruction).ToArray();
                        break;
                }
        }

        internal CilBody Run()
        {
            var source = _sourceMethod.Body;
            _event.Locals.Capacity = source.Variables.Locals.Count;
            _event.Locals.AddRange(source.Variables.Locals.Select(Clone));
            var instructions = source.Instructions.Select(Clone).ToList();
            instructions.SimplifyBranches();
            _event.Fragment.Reset(instructions);
            FixBrTargets(_event.Fragment);
            _event.ExceptionHandlers.AddRange(source.ExceptionHandlers.Select(Clone));
            Context.Fire(_event);
            instructions = _event.Fragment.ToList();
            var body = new CilBody(source.InitLocals, instructions, _event.ExceptionHandlers, _event.Locals);
            var emitter = _event.GetEmitter(false);
            if (emitter != null)
            {
                emitter.Commit();
                emitter.TempLocals.Flush(body);
                body.OptimizeBranches();
            }

            return body;
        }

        PdbDynamicLocal Clone(PdbDynamicLocal source)
        {
            var result = new PdbDynamicLocal(source.Flags.Count)
            {
                Local = _event.Locals[source.Local.Index],
                Name = source.Name
            };
            foreach (var f in source.Flags)
                result.Flags.Add(f);
            return result;
        }

        protected override PdbCustomDebugInfo CloneOther(PdbCustomDebugInfo source)
        {
            switch (source)
            {
                case PdbAsyncMethodCustomDebugInfo i:
                    var n = new PdbAsyncMethodCustomDebugInfo(i.StepInfos.Count)
                    {
                        CatchHandlerInstruction = GetInstruction(i.CatchHandlerInstruction),
                        KickoffMethod = Importer.Import(i.KickoffMethod).ResolveMethodDefThrow()
                    };
                    for (var j = 0; j < i.StepInfos.Count; j++)
                        n.StepInfos.Add(new PdbAsyncStepInfo(
                            GetInstruction(i.StepInfos[j].YieldInstruction),
                            Importer.Import(i.StepInfos[j].BreakpointMethod).ResolveMethodDefThrow(),
                            GetInstruction(i.StepInfos[j].BreakpointInstruction)
                        ));
                    return n;
                case PdbStateMachineHoistedLocalScopesCustomDebugInfo i:
                    var nls = new PdbStateMachineHoistedLocalScopesCustomDebugInfo(i.Scopes.Count);
                    foreach (var si in nls.Scopes)
                        nls.Scopes.Add(new StateMachineHoistedLocalScope(
                            GetInstruction(si.Start),
                            GetInstruction(si.End)));
                    return nls;
                case PdbIteratorMethodCustomDebugInfo i:
                    var im = new PdbIteratorMethodCustomDebugInfo
                    {
                        KickoffMethod = Importer.Import(i.KickoffMethod).ResolveMethodDefThrow()
                    };
                    return im;
                case PdbDynamicLocalsCustomDebugInfo i:
                    var ndl = new PdbDynamicLocalsCustomDebugInfo(i.Locals.Count);
                    foreach (var si in ndl.Locals)
                        ndl.Locals.Add(Clone(si));
                    return ndl;
                case PdbEditAndContinueLocalSlotMapCustomDebugInfo i2:
                case PdbEditAndContinueLambdaMapCustomDebugInfo i3:
                    return source;
            }

            return null;
        }

        internal static CilBodyMerger Create(ContextImpl context, TypeMapping tm, MethodDef sourceMethod,
            MethodDef targetMethod) =>
            new CilBodyMerger(context, context.GetImporter(tm.Target, targetMethod), tm, sourceMethod,
                targetMethod);
    }
}