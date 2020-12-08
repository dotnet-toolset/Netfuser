using Microsoft.Extensions.DependencyModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace Netfuser.Core.Hierarchy
{
    /// <summary>
    /// Reads dependencies from .deps.json files
    /// </summary>
    public interface IDepsJsonPlugin : IPlugin
    {
        /// <summary>
        /// Provides <see cref="DependencyContext"/> loaded from the main module's .deps.json file (if available). 
        /// </summary>
        DependencyContext Deps { get; }
    }
}
