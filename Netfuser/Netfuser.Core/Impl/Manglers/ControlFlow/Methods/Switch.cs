using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Base.Collections.Props;
using Base.Rng;
using dnlib.DotNet.Emit;
using Netfuser.Core.Manglers.ControlFlow;
using Netfuser.Dnext;
using Netfuser.Dnext.Cil;

namespace Netfuser.Core.Impl.Manglers.ControlFlow.Methods
{
    class Switch : CFMangleMethod
    {
        static readonly PropKey<ICFMangleContext, Impl> PropImpl = new PropKey<ICFMangleContext, Impl>();
        public Switch(IContextImpl context)
            : base(context, NetfuserFactory.CFManglerSwitchName)
        {
        }

        class Impl
        {
            private readonly Switch _switch;
            private readonly ICFMangleContext _context;
            private readonly SwitchPredicate _predicate;
            private readonly Local _local;
            public Impl(Switch @switch, ICFMangleContext context)
            {
                _switch = @switch;
                _context = context;
                _local = new Local(_context.Method.Module.CorLibTypes.UInt32);
                _context.MethodBody.Variables.Add(_local);
                _context.MethodBody.InitLocals = true;

                var rng = _switch.Mangler.Rng;
                switch (rng.NextUInt32(2)) // case 2 is null predicate, which is OK
                {
                    case 0:
                        _predicate = new SwitchPredicate.Xor(_context);
                        break;
                    case 1:
                        _predicate = new SwitchPredicate.Expr(_context);
                        break;
                }
            }

            public void Mangle(Block.Regular block)
            {
                var rng = _switch.Mangler.Rng;
                var fi = _context.RootBlock.FlowInfo.Info;
                var fragments = _context.SplitFragments(block);

                if (fragments.Count < 3) return;

                int i;
                var keyId = Enumerable.Range(0, fragments.Count).ToArray();
                keyId.Shuffle(rng);
                var key = new int[keyId.Length];
                for (i = 0; i < key.Length; i++)
                {
                    var q = rng.NextInt32() & 0x7fffffff;
                    key[i] = q - q % fragments.Count + keyId[i];
                }

                var statementKeys = new Dictionary<Instruction, int>();
                var current = fragments.First;
                i = 0;
                while (current != null)
                {
                    if (i != 0)
                        statementKeys[current.Value.First] = key[i];
                    i++;
                    current = current.Next;
                }

                var lastInstructions = new HashSet<Instruction>(fragments.Select(f => f.Last()));
                var blockInstructions = new HashSet<Instruction>(block.Fragment);
                var firstFragmentInstructions = new HashSet<Instruction>(fragments.First());

                bool HasUnknownSource(IEnumerable<Instruction> instrs) =>
                    instrs.Any(instr =>
                    {
                        //return true;
                        fi.TryGetValue(instr, out var info);
                        if (info.RefCount > 1) return true;
                        Debug.Assert(info.RefCount == 1);
                        var srcs = info.ReferencedBy;
                        if (srcs.Count == 0) return false;
                        Debug.Assert(srcs.Count == 1);
                        var src = srcs[0];
                        return
                            // Target of switch => assume unknown
                            src.OpCode.Code == Code.Switch // Operand is Instruction[]
                                                           // Not targeted by the last of statements
                            || lastInstructions.Contains(src)
                            // Not within current instruction block / targeted in first statement
                            // || src.Offset <= fragments.First.Value.Last().Offset || src.Offset >= block.Fragment.Last().Offset
                            || !blockInstructions.Contains(src)
                            || firstFragmentInstructions.Contains(src);
                    });

                var switchInstr = new Instruction(OpCodes.Switch);
                var switchHdr = new CilFragment();

                if (_predicate != null)
                {
                    switchHdr.Add(Instruction.CreateLdcI4(_predicate.GetSwitchKey(key[1])));
                    _predicate.EmitSwitchLoad(switchHdr);
                }
                else
                    switchHdr.Add(Instruction.CreateLdcI4(key[1]));

                switchHdr.Add(Instruction.Create(OpCodes.Dup));
                switchHdr.Add(Instruction.Create(OpCodes.Stloc, _local));
                switchHdr.Add(Instruction.Create(OpCodes.Ldc_I4, fragments.Count));
                switchHdr.Add(Instruction.Create(OpCodes.Rem_Un));
                switchHdr.Add(switchInstr);

                _context.AddJump(switchHdr, fragments.Last.Value.First, true);
                var switchHdrSecond = switchHdr.Instructions[1];

                var operands = new Instruction[fragments.Count];
                current = fragments.First;
                i = 0;
                while (current.Next != null)
                {
                    var newFragment = new CilFragment(current.Value);

                    if (i != 0)
                    {
                        var lastInstr = newFragment.Last();
                        // Convert to switch
                        var converted = false;
                        fi.TryGetValue(lastInstr, out var info);

                        if (lastInstr.IsBr())
                        {
                            // Unconditional
                            var target = (Instruction)lastInstr.Operand;
                            if (info.ReferencedBy.Count == 0 && statementKeys.TryGetValue(target, out var brKey))
                            {
                                var targetKey = _predicate?.GetSwitchKey(brKey) ?? brKey;
                                var unkSrc = HasUnknownSource(newFragment);

                                newFragment.Instructions.RemoveAt(newFragment.Instructions.Count - 1);

                                if (unkSrc)
                                    newFragment.Add(Instruction.Create(OpCodes.Ldc_I4, targetKey));
                                else
                                {
                                    var thisKey = key[i];
                                    var r = rng.NextInt32();
                                    newFragment.Add(Instruction.Create(OpCodes.Ldloc, _local));
                                    newFragment.Add(Instruction.CreateLdcI4(r));
                                    newFragment.Add(Instruction.Create(OpCodes.Mul));
                                    newFragment.Add(Instruction.Create(OpCodes.Ldc_I4, (thisKey * r) ^ targetKey));
                                    newFragment.Add(Instruction.Create(OpCodes.Xor));
                                }

                                _context.AddJump(newFragment, switchHdrSecond, false);
                                operands[keyId[i]] = newFragment.First;
                                converted = true;
                            }
                        }
                        else if (lastInstr.IsConditionalBranch())
                        {
                            // Conditional
                            var target = (Instruction)lastInstr.Operand;
                            if (info.ReferencedBy.Count == 0 && statementKeys.TryGetValue(target, out var brKey))
                            {
                                var unkSrc = HasUnknownSource(newFragment);
                                var nextKey = key[i + 1];
                                var condBr = newFragment.Last().OpCode;
                                newFragment.Instructions.RemoveAt(newFragment.Instructions.Count - 1);

                                if (rng.NextBoolean())
                                {
                                    condBr = DnextFactory.InverseBranch(condBr);
                                    var tmp = brKey;
                                    brKey = nextKey;
                                    nextKey = tmp;
                                }

                                var thisKey = key[i];
                                int r = 0, xorKey = 0;
                                if (!unkSrc)
                                {
                                    r = rng.NextInt32();
                                    xorKey = thisKey * r;
                                }

                                var brKeyInstr =
                                    Instruction.CreateLdcI4(xorKey ^ (_predicate?.GetSwitchKey(brKey) ?? brKey));
                                var nextKeyInstr =
                                    Instruction.CreateLdcI4(xorKey ^ (_predicate?.GetSwitchKey(nextKey) ?? nextKey));
                                var pop = Instruction.Create(OpCodes.Pop);

                                newFragment.Add(Instruction.Create(condBr, brKeyInstr));
                                newFragment.Add(nextKeyInstr);
                                newFragment.Add(Instruction.Create(OpCodes.Dup));
                                newFragment.Add(Instruction.Create(OpCodes.Br, pop));
                                newFragment.Add(brKeyInstr);
                                newFragment.Add(Instruction.Create(OpCodes.Dup));
                                newFragment.Add(pop);

                                if (!unkSrc)
                                {
                                    newFragment.Add(Instruction.Create(OpCodes.Ldloc, _local));
                                    newFragment.Add(Instruction.CreateLdcI4(r));
                                    newFragment.Add(Instruction.Create(OpCodes.Mul));
                                    newFragment.Add(Instruction.Create(OpCodes.Xor));
                                }

                                _context.AddJump(newFragment, switchHdrSecond, false);
                                operands[keyId[i]] = newFragment.First;
                                converted = true;
                            }
                        }

                        if (!converted)
                        {
                            // Normal

                            var targetKey = _predicate?.GetSwitchKey(key[i + 1]) ?? key[i + 1];
                            if (!HasUnknownSource(newFragment))
                            {
                                var thisKey = key[i];
                                var r = rng.NextInt32();
                                newFragment.Add(Instruction.Create(OpCodes.Ldloc, _local));
                                newFragment.Add(Instruction.CreateLdcI4(r));
                                newFragment.Add(Instruction.Create(OpCodes.Mul));
                                newFragment.Add(Instruction.Create(OpCodes.Ldc_I4, (thisKey * r) ^ targetKey));
                                newFragment.Add(Instruction.Create(OpCodes.Xor));
                            }
                            else
                            {
                                newFragment.Add(Instruction.Create(OpCodes.Ldc_I4, targetKey));
                            }

                            _context.AddJump(newFragment, switchHdrSecond, false);
                            operands[keyId[i]] = newFragment.First;
                        }
                    }
                    else
                        operands[keyId[i]] = switchHdr.First;

                    current.Value = newFragment;
                    current = current.Next;
                    i++;
                }

                operands[keyId[i]] = current.Value.First;
                switchInstr.Operand = operands;

                var first = fragments.First.Value;
                fragments.RemoveFirst();
                var last = fragments.Last.Value;
                fragments.RemoveLast();

                var newStatements = fragments.ToList();
                newStatements.Shuffle(rng);

                block.Fragment.Reset(first.Concat(switchHdr).Concat(newStatements.SelectMany(s => s)).Concat(last));
            }
        }

        public override void Mangle(ICFMangleContext context, Block.Regular block)
        {
            PropImpl.GetOrAdd(context, () => new Impl(this, context)).Mangle(block);
        }
    }
}