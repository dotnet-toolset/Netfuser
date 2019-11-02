using System.Collections.Generic;
using dnlib.DotNet.Emit;
using Netfuser.Dnext.Cil;

namespace Netfuser.Dnext.Impl.Cil
{
    class FlowInfo : IFlowInfo
    {
        private readonly Dictionary<Instruction, IInstrInfo> _map;

        public int MaxStack { get; internal set; }
        public IReadOnlyDictionary<Instruction, IInstrInfo> Info => _map;

        internal FlowInfo()
        {
            _map = new Dictionary<Instruction, IInstrInfo>();
        }

        internal InstrInfo this[Instruction i]
        {
            get
            {
                if (!_map.TryGetValue(i, out var result))
                    _map[i] = result = new InstrInfo();
                return (InstrInfo) result;
            }
        }
    }
}