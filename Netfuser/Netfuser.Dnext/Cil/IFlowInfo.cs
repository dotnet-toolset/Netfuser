using System.Collections.Generic;
using dnlib.DotNet.Emit;

namespace Netfuser.Dnext.Cil
{
    public interface IFlowInfo
    {
        int MaxStack { get; }
        IReadOnlyDictionary<Instruction, IInstrInfo> Info { get; }
    }
}