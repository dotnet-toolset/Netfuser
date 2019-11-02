using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Base.Collections;
using dnlib.DotNet.Emit;
using Netfuser.Dnext.Cil;

namespace Netfuser.Dnext.Impl.Cil
{
    class BlockParser
    {
        private readonly IEnumerable<Instruction> _instructions;
        private readonly IEnumerable<ExceptionHandler> _exceptionHandlers;

        private FlowInfo _flow;
        int stack;
        int currentMaxStack;
        bool resetStack;


        public BlockParser(CilBody body)
        {
            _instructions = body.Instructions;
            _exceptionHandlers = body.ExceptionHandlers;
        }


        public BlockParser(IEnumerable<Instruction> instructions, IEnumerable<ExceptionHandler> exceptionHandlers)
        {
            _instructions = instructions;
            _exceptionHandlers = exceptionHandlers;
        }

        int WriteStack(Instruction instr)
        {
            if (instr == null) return stack;
            var info = _flow[instr];
            if (info.IsDepthSet)
            {
                var stack2 = info.DepthBefore;
                if (stack != stack2)
                    info.Error($"stack mismatch: {stack}!={stack2}");
                return stack2;
            }

            info.DepthBefore = stack;
            if (stack > currentMaxStack)
                currentMaxStack = stack;
            return stack;
        }

        private void UpdateStack(Instruction instr)
        {
            var info = _flow[instr];
            if (resetStack)
            {
                stack = info.DepthBefore;
                info.NaturalTarget = false;
                resetStack = false;
            }

            stack = WriteStack(instr);
            var opCode = instr.OpCode;
            var code = opCode.Code;
            if (code == Code.Jmp)
            {
                if (stack != 0)
                    info.Error($"nonzero stack at the jmp: {stack}");
            }
            else
            {
                instr.CalculateStackUsage(out var pushes, out var pops);
                if (pops == -1)
                    stack = 0;
                else
                {
                    stack -= pops;
                    CheckStack();
                    stack += pushes;
                }
            }

            CheckStack();
            info.DepthAfter = stack;

            switch (opCode.FlowControl)
            {
                case FlowControl.Branch:
                    WriteStack(Target(instr.Operand));
                    resetStack = true;
                    break;

                case FlowControl.Call:
                    if (code == Code.Jmp)
                        resetStack = true;
                    break;

                case FlowControl.Cond_Branch:
                    if (code == Code.Switch)
                    {
                        if (instr.Operand is IList<Instruction> targets)
                            foreach (var t in targets)
                                WriteStack(Target(t));
                    }
                    else
                        WriteStack(Target(instr.Operand));

                    break;

                case FlowControl.Return:
                case FlowControl.Throw:
                    resetStack = true;
                    break;
            }

            void CheckStack()
            {
                if (stack >= 0) return;
                info.Error($"negative stack: {stack}");
                stack = 0;
            }

            Instruction Target(object op)
            {
                if (op is Instruction i)
                {
                    _flow[i].Ref(instr);
                    return i;
                }

                info.Error($"invalid operand: {op}");
                return null;
            }
        }

        internal Block.Root Parse()
        {
            var root = new Block.Root
            {
                FlowInfo = _flow = new FlowInfo()
            };

            var ehScopes = new Dictionary<ExceptionHandler, (Block.Seh, Block.Seh, Block.Seh)>();
            foreach (var eh in _exceptionHandlers)
            {
                var tryBlock = new Block.Seh(BlockType.Try, eh);

                var handlerType = BlockType.Handler;

                switch (eh.HandlerType)
                {
                    case ExceptionHandlerType.Finally:
                        handlerType = BlockType.Finally;
                        break;
                    case ExceptionHandlerType.Fault:
                        handlerType = BlockType.Fault;
                        break;
                }

                var handlerBlock = new Block.Seh(handlerType, eh);

                if (eh.FilterStart != null)
                    ehScopes[eh] = (tryBlock, handlerBlock, new Block.Seh(BlockType.Filter, eh));
                else
                    ehScopes[eh] = (tryBlock, handlerBlock, (Block.Seh) null);

                Instruction instr;
                if ((instr = eh.TryStart) != null)
                    _flow[instr].DepthBefore = 0;
                if ((instr = eh.FilterStart) != null)
                {
                    _flow[instr].DepthBefore = 1;
                    currentMaxStack = 1;
                }

                if ((instr = eh.HandlerStart) != null)
                {
                    var pushed = eh.HandlerType == ExceptionHandlerType.Catch ||
                                 eh.HandlerType == ExceptionHandlerType.Filter;
                    if (pushed)
                    {
                        _flow[instr].DepthBefore = 1;
                        currentMaxStack = 1;
                    }
                    else
                        _flow[instr].DepthBefore = 0;
                }
            }

            var scopeStack = new Stack<Block>();

            scopeStack.Push(root);

            stack = 0;
            resetStack = false;
            foreach (var instr in _instructions)
            {
                foreach (var eh in _exceptionHandlers)
                {
                    if (instr == eh.TryEnd) scopeStack.Pop();
                    if (instr == eh.HandlerEnd) scopeStack.Pop();
                    if (eh.FilterStart != null && instr == eh.HandlerStart)
                    {
                        // Filter must precede handler immediately
                        Debug.Assert(scopeStack.Peek().Type == BlockType.Filter);
                        scopeStack.Pop();
                    }
                }

                foreach (var eh in _exceptionHandlers.Reverse())
                {
                    var (item1, item2, item3) = ehScopes[eh];
                    var parent = scopeStack.Count > 0 ? scopeStack.Peek() : null;
                    if (instr == eh.TryStart)
                    {
                        parent?.Add(item1);
                        scopeStack.Push(item1);
                    }

                    if (instr == eh.HandlerStart)
                    {
                        parent?.Add(item2);
                        scopeStack.Push(item2);
                    }

                    if (instr == eh.FilterStart)
                    {
                        parent?.Add(item3);
                        scopeStack.Push(item3);
                    }
                }

                var scope = scopeStack.Peek();
                if (!(scope.LastChild is Block.Regular block))
                    scope.Add(block = new Block.Regular());
                block.Fragment.Add(instr);
                UpdateStack(instr);
            }

            foreach (var eh in _exceptionHandlers)
            {
                if (eh.TryEnd == null)
                    scopeStack.Pop();
                if (eh.HandlerEnd == null)
                    scopeStack.Pop();
            }

            _flow.MaxStack = currentMaxStack;
            Debug.Assert(scopeStack.Count == 1);
            return root;
        }
    }
}