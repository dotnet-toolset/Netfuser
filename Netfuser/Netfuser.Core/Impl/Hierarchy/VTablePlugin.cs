using Netfuser.Core.Hierarchy;
using Netfuser.Dnext;
using Netfuser.Dnext.Hierarchy;

namespace Netfuser.Core.Impl.Hierarchy
{
    class VTablePlugin : AbstractPlugin, IVTablePlugin
    {
        public IVTables VTables { get; }

        internal VTablePlugin(IContextImpl context)
            : base(context)
        {
            VTables = DnextFactory.NewVTables();
        }
    }
}