using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Netfuser.Dnext
{
    /// <summary>
    /// Simple [MethodDef,Instruction] tuple referencing specific instruction within a method
    /// </summary>
    public readonly struct InstrRef
    {
        public readonly MethodDef Method;
        public readonly Instruction Instruction;

        public InstrRef(MethodDef method, Instruction instruction)
        {
            Method = method;
            Instruction = instruction;
        }
    }
}