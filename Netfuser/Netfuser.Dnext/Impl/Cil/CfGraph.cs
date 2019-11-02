using System.Collections;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet.Emit;
using Netfuser.Dnext.Cil.Graph;

namespace Netfuser.Dnext.Impl.Cil
{
    class CfGraph : ICfGraph
    {
        private readonly List<CfBlock> _blocks;
        private readonly CilBody _body;
        private readonly int[] _instrBlocks;
        private readonly Dictionary<Instruction, int> _indexMap;

        public CfGraph(CilBody body)
        {
            _body = body;
            var c = body.Instructions.Count;
            _instrBlocks = new int[c];
            _blocks = new List<CfBlock>();

            _indexMap = new Dictionary<Instruction, int>();
            for (var i = 0; i < c; i++)
                _indexMap.Add(body.Instructions[i], i);
            if (c > 0)
            {
                var blockHeaders = new HashSet<Instruction>();
                var entryHeaders = new HashSet<Instruction>();
                PopulateBlockHeaders(blockHeaders, entryHeaders);
                SplitBlocks(blockHeaders, entryHeaders);
                LinkBlocks();
                MapHandlers();
            }
        }

        public int Count => _blocks.Count;

        public CfBlock this[int id] => _blocks[id];

        public CilBody Body => _body;

        IEnumerator<CfBlock> IEnumerable<CfBlock>.GetEnumerator() => _blocks.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _blocks.GetEnumerator();

        public CfBlock GetContainingBlock(Instruction instr) => _blocks[_instrBlocks[_indexMap[instr]]];

        public CfBlockKey[] ComputeKeys()
        {
            var keys = new CfBlockKey[Count];

            foreach (var block in _blocks)
                keys[block.Id] = new CfBlockKey
                {
                    Type = (block.Type & CfBlockType.Entry) != 0 ? CfBlockKeyType.Explicit : CfBlockKeyType.Incremental
                };

            uint id = 0;
            for (var i = 0; i < keys.Length; i++)
            {
                keys[i].EntryState = id++;
                keys[i].ExitState = id++;
            }

            var finallyIds = new Dictionary<ExceptionHandler, uint>();
            var ehMap = new Dictionary<CfBlock, List<ExceptionHandler>>();

            bool updated;
            do
            {
                updated = false;

                // Update the state ids with the maximum id
                foreach (var block in _blocks)
                {
                    var key = keys[block.Id];
                    if (block.Sources.Count > 0)
                    {
                        var newEntry = block.Sources.Select(b => keys[b.Id].ExitState).Max();
                        if (key.EntryState != newEntry)
                        {
                            key.EntryState = newEntry;
                            updated = true;
                        }
                    }

                    if (block.Targets.Count > 0)
                    {
                        var newExit = block.Targets.Select(b => keys[b.Id].EntryState).Max();
                        if (key.ExitState != newExit)
                        {
                            key.ExitState = newExit;
                            updated = true;
                        }
                    }

                    switch (block.Footer.OpCode.Code)
                    {
                        case Code.Endfilter:
                        case Code.Endfinally:
                            // Match the exit state within finally/fault/filter
                            foreach (var eh in block.Handlers)
                                if (finallyIds.TryGetValue(eh, out var ehVal))
                                {
                                    if (key.ExitState > ehVal)
                                    {
                                        finallyIds[eh] = key.ExitState;
                                        updated = true;
                                    }
                                    else if (key.ExitState < ehVal)
                                    {
                                        key.ExitState = ehVal;
                                        updated = true;
                                    }
                                }
                                else
                                {
                                    finallyIds[eh] = key.ExitState;
                                    updated = true;
                                }

                            break;
                        case Code.Leave:
                        case Code.Leave_S:
                            // Match the exit state with finally/fault/filter
                            uint? maxVal = null;
                            foreach (var eh in block.Handlers)
                                if (finallyIds.TryGetValue(eh, out var ehVal) && (maxVal == null || ehVal > maxVal))
                                {
                                    if (maxVal != null)
                                        updated = true;
                                    maxVal = ehVal;
                                }

                            if (maxVal == null) break;
                            if (key.ExitState > maxVal.Value)
                            {
                                maxVal = key.ExitState;
                                updated = true;
                            }
                            else if (key.ExitState < maxVal.Value)
                            {
                                key.ExitState = maxVal.Value;
                                updated = true;
                            }

                            foreach (var eh in block.Handlers)
                                finallyIds[eh] = maxVal.Value;

                            break;
                    }

                    keys[block.Id] = key;
                }
            } while (updated);

            return keys;
        }

        void PopulateBlockHeaders(ISet<Instruction> blockHeaders, ISet<Instruction> entryHeaders)
        {
            for (var i = 0; i < _body.Instructions.Count; i++)
            {
                var instr = _body.Instructions[i];
                switch (instr.Operand)
                {
                    case Instruction item:
                    {
                        blockHeaders.Add(item);
                        if (i + 1 < _body.Instructions.Count)
                            blockHeaders.Add(_body.Instructions[i + 1]);
                        break;
                    }
                    case Instruction[] operand:
                    {
                        foreach (var target in operand)
                            blockHeaders.Add(target);
                        if (i + 1 < _body.Instructions.Count)
                            blockHeaders.Add(_body.Instructions[i + 1]);
                        break;
                    }
                    default:
                    {
                        if ((instr.OpCode.FlowControl == FlowControl.Throw ||
                             instr.OpCode.FlowControl == FlowControl.Return) &&
                            i + 1 < _body.Instructions.Count)
                            blockHeaders.Add(_body.Instructions[i + 1]);

                        break;
                    }
                }
            }

            blockHeaders.Add(_body.Instructions[0]);
            foreach (var eh in _body.ExceptionHandlers)
            {
                blockHeaders.Add(eh.TryStart);
                blockHeaders.Add(eh.HandlerStart);
                blockHeaders.Add(eh.FilterStart);
                entryHeaders.Add(eh.HandlerStart);
                entryHeaders.Add(eh.FilterStart);
            }
        }

        private void SplitBlocks(ICollection<Instruction> blockHeaders, ICollection<Instruction> entryHeaders)
        {
            Instruction currentBlockHdr = null;

            for (var i = 0; i < _body.Instructions.Count; i++)
            {
                var instr = _body.Instructions[i];
                if (blockHeaders.Contains(instr))
                {
                    if (currentBlockHdr != null)
                        addBlock(_body.Instructions[i - 1]);

                    currentBlockHdr = instr;
                }

                _instrBlocks[i] = _blocks.Count;
            }

            addBlock(_body.Instructions[_body.Instructions.Count - 1]);

            void addBlock(Instruction footer)
            {
                var type = CfBlockType.Normal;
                if (entryHeaders.Contains(currentBlockHdr) || currentBlockHdr == _body.Instructions[0])
                    type |= CfBlockType.Entry;
                if (footer.OpCode.FlowControl == FlowControl.Return || footer.OpCode.FlowControl == FlowControl.Throw)
                    type |= CfBlockType.Exit;

                _blocks.Add(new CfBlock(_blocks.Count, type, currentBlockHdr, footer));
            }
        }

        void LinkBlocks()
        {
            for (var i = 0; i < _body.Instructions.Count; i++)
            {
                var instr = _body.Instructions[i];
                switch (instr.Operand)
                {
                    case Instruction key:
                        var srcBlock = _blocks[_instrBlocks[i]];
                        var dstBlock = _blocks[_instrBlocks[_indexMap[key]]];
                        dstBlock.AddSource(srcBlock);
                        srcBlock.AddTarget(dstBlock);
                        break;
                    case Instruction[] operand:
                        foreach (var target in operand)
                        {
                            srcBlock = _blocks[_instrBlocks[i]];
                            dstBlock = _blocks[_instrBlocks[_indexMap[target]]];
                            dstBlock.AddSource(srcBlock);
                            srcBlock.AddTarget(dstBlock);
                        }

                        break;
                }
            }

            for (var i = 0; i < _blocks.Count; i++)
                if (_blocks[i].Footer.OpCode.FlowControl != FlowControl.Branch &&
                    _blocks[i].Footer.OpCode.FlowControl != FlowControl.Return &&
                    _blocks[i].Footer.OpCode.FlowControl != FlowControl.Throw)
                {
                    _blocks[i].AddTarget(_blocks[i + 1]);
                    _blocks[i + 1].AddSource(_blocks[i]);
                }
        }

        private void MapHandlers()
        {
            foreach (var block in _blocks)
                switch (block.Footer.OpCode.Code)
                {
                    case Code.Endfilter:
                    case Code.Endfinally:
                        var footerIndex = _indexMap[block.Footer];
                        foreach (var eh in Body.ExceptionHandlers)
                        {
                            if (eh.FilterStart != null && block.Footer.OpCode.Code == Code.Endfilter)
                            {
                                if (footerIndex >= _indexMap[eh.FilterStart] &&
                                    footerIndex < _indexMap[eh.HandlerStart])
                                    block.AddHandler(eh);
                            }
                            else if (eh.HandlerType == ExceptionHandlerType.Finally ||
                                     eh.HandlerType == ExceptionHandlerType.Fault)
                            {
                                if (footerIndex >= _indexMap[eh.HandlerStart] &&
                                    (eh.HandlerEnd == null || footerIndex < _indexMap[eh.HandlerEnd]))
                                    block.AddHandler(eh);
                            }
                        }

                        break;
                    case Code.Leave:
                    case Code.Leave_S:
                        footerIndex = _indexMap[block.Footer];
                        foreach (var eh in Body.ExceptionHandlers)
                        {
                            if (footerIndex >= _indexMap[eh.TryStart] &&
                                (eh.TryEnd == null || footerIndex < _indexMap[eh.TryEnd]))
                                block.AddHandler(eh);
                        }

                        break;
                }
        }
    }
}