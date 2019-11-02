using Netfuser.Dnext.Hierarchy;

namespace Netfuser.Core.Hierarchy
{
    /// <summary>
    /// Computes hierarchy information for every type in the source modules and makes it available via the <see cref="VTables"/> property
    /// </summary>
    public interface IVTablePlugin : IPlugin
    {
        /// <summary>
        /// Exposes <see cref="IVTables"/> interface that provides hierarchy info for a given type
        /// </summary>
        IVTables VTables { get; }
    }
}
