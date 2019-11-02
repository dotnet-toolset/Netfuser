using System;
using System.Collections.Generic;
using System.IO;
using Base.Collections;
using dnlib.DotNet;
using Netfuser.Dnext;

namespace Netfuser.Core.Impl
{
    /// <summary>
    /// This interface is intended for plugin developers, provides additional 
    /// properies/methods to interact with the <see cref="IContext"/> instance
    /// </summary>
    public interface IContextImpl : IContext
    {
        /// <summary>
        /// List of all source modules loaded into the context, groupped by <see cref="ModuleTreat"/>
        /// </summary>
        IReadOnlyDictionary<ModuleTreat, IReadOnlySet<ModuleDef>> SourceModules { get; }

        /// <summary>
        /// Main source module, usually the executable with entry point
        /// </summary>
        ModuleDef MainSourceModule { get; }

        /// <summary>
        /// This is used to locate <see cref="TypeDef"/> in the target assembly corresponding to the
        /// <see cref="TypeDef"/>, <see cref="TypeRef"/> or <see cref="Type"/> in one of the source assemblies
        /// </summary>
        IReadOnlyDictionary<ITypeKey, TypeMapping> MappedTypes { get; }

        /// <summary>
        /// This is used to locate <see cref="Resource"/> in the target assembly corresponding to the
        /// <see cref="Resource"/> in one of the source assemblies
        /// </summary>
        IReadOnlyDictionary<ITypeKey, ResourceMapping> MappedResources { get; }

        /// <summary>
        /// Generated assembly together with the satellites will be saved to this folder
        /// </summary>
        DirectoryInfo OutputFolder { get; set; }

        /// <summary>
        /// <see cref="dnlib"/>'s <see cref="Importer"/> aware of the target assembly, mapping of types and members etc.
        /// May be used to obtain proper references to the members of target assembly, ready to be used in <see cref="Dnext.Cil.IILEmitter"/> etc.
        /// </summary>
        Importer BasicImporter { get; }

        /// <summary>
        /// Obtains <see cref="dnlib"/>'s <see cref="Importer"/> for a specific type with generic parameters.
        /// Otherwise equivalent to <see cref="BasicImporter"/>
        /// </summary>
        /// <param name="type">type with generic parameters</param>
        /// <returns> instance of the <see cref="Importer"/></returns>
        Importer GetImporter(TypeDef type);

        /// <summary>
        /// Obtains <see cref="dnlib"/>'s <see cref="Importer"/> for a specific type and method with generic parameters.
        /// Otherwise equivalent to <see cref="BasicImporter"/>
        /// </summary>
        /// <param name="type">type with generic parameters</param>
        /// <param name="method">type with generic parameters</param>
        /// <returns> instance of the <see cref="Importer"/></returns>
        Importer GetImporter(TypeDef type, MethodDef method);

        /// <summary>
        /// Obtains instance of the given plugin type, or creates one if it hasn't been instantiated yet.
        /// </summary>
        /// <typeparam name="T">interface type of the plugin, descendant of <see cref="IPlugin"/></typeparam>
        /// <param name="creator">optional function to create an instance of the plugin if none exists yet</param>
        /// <param name="name">name of the plugin (only specified for the named plugins, MUST be <c>null</c> otherwise)</param>
        /// <returns>instance of the plugin or <c>null</c> if the plugin wasn't found and no creator is specified</returns>
        T Plugin<T>(Func<T> creator, string name = null) where T : class, IPlugin;

        /// <summary>
        /// Adds new instance of the given plugin type to this context.
        /// </summary>
        /// <typeparam name="T">type of the plugin (MUST be interface inherited from  <see cref="IPlugin"/>)</typeparam>
        /// <param name="plugin">instance of the plugin</param>
        void Register<T>(T plugin) where T : class, IPlugin;

        /// <summary>
        /// Sends the event to all observers.
        /// See <see cref="NetfuserEvent"/> for details
        /// </summary>
        /// <typeparam name="T">type of the event inherited from <see cref="NetfuserEvent"/></typeparam>
        /// <param name="ev">instance of the event</param>
        /// <returns>the same event instance</returns>
        T Fire<T>(T ev) where T : NetfuserEvent;
        
        /// <summary>
        /// Preferred method to throw from within plugins
        /// </summary>
        /// <param name="msg">exception message</param>
        /// <returns>generated exception, the method never returns actually, 
        /// this is used to trick the compiler so it knows where the code branch terminates
        /// <example>
        /// throw context.Error("horrible error");
        /// // compiler will not expect control to flow here
        /// </example>
        /// </returns>
        Exception Error(string msg);
    }
}