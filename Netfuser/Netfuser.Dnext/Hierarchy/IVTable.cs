using System.Collections.Generic;
using dnlib.DotNet;

namespace Netfuser.Dnext.Hierarchy
{
    public interface IVTable
    {
        TypeSig Type { get; }
        IReadOnlyList<IVTableSlot> Slots { get; }
        IReadOnlyDictionary<TypeSig, IReadOnlyList<IVTableSlot>> InterfaceSlots { get; }

        IEnumerable<IVTableSlot> FindSlots(IMethod method);
    }
}