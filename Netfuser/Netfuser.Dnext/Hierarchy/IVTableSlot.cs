using dnlib.DotNet;

namespace Netfuser.Dnext.Hierarchy
{
    public interface IVTableSlot
    {
        TypeSig DeclaringType { get; }
        TypeSig MethodDefDeclType { get; }
        MethodDef MethodDef { get; }
        IVTableSlot Parent { get; }
    }
}