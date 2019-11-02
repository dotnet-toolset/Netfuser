using System;
using Base.Lang;
using Base.Logging;
using Netfuser.Core.Impl;

namespace Netfuser.Core
{
    /// <summary>
    /// Base interface for plugins.
    /// There are named and unnamed plugins.
    /// Unnamed plugins are singletons, only one instance of each unnamed plugin is permitted in a <see cref="IContext"/>
    /// Named plugins permit multiple instances per <see cref="IContext"/>, but every instance MUST have a unique name
    /// </summary>
    public interface IPlugin : IDisposable, ILoggable
    {
        /// <summary>
        /// Context thus plugin belongs to
        /// </summary>
        IContextImpl Context { get; }
    }

    /// <summary>
    /// Base interface for named plugins
    /// </summary>
    public interface INamedPlugin : IPlugin, INamed
    {
    }
}