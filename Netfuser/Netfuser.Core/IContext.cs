using dnlib.DotNet;
using System;
using System.Collections.Generic;
using Base.Logging;

namespace Netfuser.Core
{
    /// <summary>
    /// Context for all Netfuser operations.
    /// To start working with Netfuser, create new context using <see cref="NetfuserFactory.NewContext()"/>,
    /// then use extension methods to load assemblies, attach and configure plugins, and finally call
    /// <see cref="Run()"/> to process loaded assemblies.
    /// </summary>
    public interface IContext : ILoggable, IObservable<NetfuserEvent>
    {
        /// <summary>
        /// Indicates whether debug mode is active (configured by <see cref="Extensions.SetDebugMode(IContext, bool)"/>)
        /// It's easier to analyze problems in the generated assembly when debug mode is on, obfuscation of metadata names
        /// and some other features are turned off to simplify decompilation and analysis.
        /// </summary>
        bool DebugMode { get; }

        /// <summary>
        /// This module context is used by <c>dnlib</c> to resolve referenced assemblies,
        /// type references and member references.
        /// May be replaced by observing <see cref="NetfuserEvent.Initialize"/>, but see event docs 
        /// </summary>
        ModuleContext ModuleContext { get; }

        /// <summary>
        /// Generated module, contains merged assemblies, embedded resources and appropriate loaders to make things work.
        /// </summary>
        ModuleDef TargetModule { get; }

        /// <summary>
        /// Obtain reference to the plugin that was previously loaded in the context.
        /// If the plugin is unnamed, only its type needs to be specified, otherwise both type and name are required.
        /// </summary>
        /// <example>
        /// The following code obtains instance of <see cref="IRngPlugin"/> configured in the context
        /// <code>
        /// var rng=context.Plugin<IRngPlugin>();
        /// </code>
        /// </example>
        /// <param name="name">name of the plugin for named plugins</param>
        /// <typeparam name="T">interface type of the plugin, descendant of <see cref="IPlugin"/></typeparam>
        /// <returns>instance of the plugin or <c>null</c> if no plugin of the given type/name was found</returns>
        T Plugin<T>(string name = null) where T : class, IPlugin;

        /// <summary>
        /// Obtain list of plugins of the given type instantiated in this context
        /// </summary>
        /// <typeparam name="T">interface type of the plugin, descendant of <see cref="IPlugin"/></typeparam>
        /// <returns>list of plugins or empty list if no plugins of the given type/name was found</returns>
        IReadOnlyList<T> Plugins<T>() where T : class, INamedPlugin;
        
        /// <summary>
        /// Do the work.
        /// This method MUST be called last. The method is blocking until all 
        /// configured transformations for the loaded assemblies are complete.
        /// The method returns normally if all operations were successful, or 
        /// throws the first encountered error.
        /// After the method returns, either normally or due to exception, 
        /// the context is invalid and MUST NOT be used
        /// </summary>
        void Run();
    }
}