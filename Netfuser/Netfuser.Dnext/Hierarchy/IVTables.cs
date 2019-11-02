using dnlib.DotNet;

namespace Netfuser.Dnext.Hierarchy
{
    public interface IVTables
    {
        IVTable GetVTable(ITypeDefOrRef type);
    }
}