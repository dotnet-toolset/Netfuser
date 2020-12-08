using dnlib.DotNet;
using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using Base.IO.Impl;
using Netfuser.Core.Embedder;
using Netfuser.Core.FeatureInjector;
using Netfuser.Core.Impl;
using Netfuser.Core.Impl.Embedder;
using Netfuser.Core.Impl.FeatureInjector;
using Netfuser.Core.Impl.Manglers.ControlFlow;
using Netfuser.Core.Impl.Manglers.ControlFlow.Methods;
using Netfuser.Core.Impl.Manglers.Ints;
using Netfuser.Core.Impl.Manglers.Metadata;
using Netfuser.Core.Impl.Manglers.Strings.Methods;
using Netfuser.Core.Impl.Manglers.Strings.Splitters;
using Netfuser.Core.Impl.Manglers.Values;
using Netfuser.Core.Impl.Project;
using Netfuser.Core.Impl.Writers;
using Netfuser.Core.Manglers.ControlFlow;
using Netfuser.Core.Manglers.Ints;
using Netfuser.Core.Manglers.Metadata;
using Netfuser.Core.Manglers.Strings;
using Netfuser.Core.Naming;
using Netfuser.Core.Project;
using Netfuser.Core.Writers;
using Netfuser.Runtime.Demanglers.Strings;
using Netfuser.Runtime.Embedder;
using Base.Logging;
using Netfuser.Core.Hierarchy;
using Netfuser.Core.Impl.Hierarchy;
using Microsoft.Extensions.DependencyModel;
using System.Reflection;

namespace Netfuser.Core
{
    public static class Extensions
    {
        #region Specifying assemblies to be processed

        /// <summary>
        /// Load assembly and dependencies using Visual Studio project (.csproj file)
        /// This is the preferred way to initialize Netfuser, as it can automatically 
        /// detect the best way to handle referenced assemblies based on project dependencies
        /// (i.e. merge it in the destination module, embed as a resource in the destination module or
        /// copy to the destination assembly folder)
        /// To override the default behavior, use <see cref="IgnoreReferencedAssemblies"/>, <see cref="CopyReferencedAssemblies"/>,
        /// <see cref="MergeReferencedAssemblies"/>, <see cref="EmbedReferencedAssemblies"/>, or observe the <see cref="NetfuserEvent.ResolveSourceModules"/> event
        /// This method doesn't actually perform loading, instead it configures the context to load the
        /// project at the appropriate time, after the <see cref="IContext.Run()"/> method is called.
        /// </summary>
        /// <param name="ctx">Netfuser context</param>
        /// <param name="path">fully-qualified path to the .csproj file or to the directory containing single .csproj file</param>
        /// <param name="options">project loading options</param>
        /// <returns>Netfuser context</returns>
        public static IContext LoadProject(this IContext ctx, string path, ProjectOptions options = null)
        {
            var c = (IContextImpl)ctx;
            c.Register<IProjectLoader>(new ProjectLoader(c, path, options));
            return ctx;
        }

        /// <summary>
        /// Load the main module (the one with the entry point) contained in the given assembly into the Netfuser context.
        /// Netfuser will attempt to automatically resolve all referenced assemblies and 
        /// will add their modules to the context. 
        /// Netfuser will try to automatically figure out the best way to handle each referenced module/assembly 
        /// (i.e. merge it in the destination module, embed as a resource into the destination module or
        /// copy to the destination assembly folder)
        /// To override the default behavior, use <see cref="IgnoreReferencedAssemblies"/>, <see cref="CopyReferencedAssemblies"/>,
        /// <see cref="MergeReferencedAssemblies"/>, <see cref="EmbedReferencedAssemblies"/>, or observe the <see cref="NetfuserEvent.ResolveSourceModules"/> event
        /// This method doesn't actually perform loading, instead it configures the context to load the
        /// executable at the appropriate time, after the <see cref="IContext.Run()"/> method is called.
        /// </summary>
        /// <param name="ctx">Netfuser context</param>
        /// <param name="mainExePath">fully-qualified path to the executable or .dll for .NETCore </param>
        /// <returns>Netfuser context</returns>
        public static IContext LoadExecutable(this IContext ctx, string mainExePath)
        {
            var c = (IContextImpl)ctx;
            if (c.OutputFolder == null)
                if (mainExePath != null)
                {
                    string dir = null;
                    if (File.Exists(mainExePath))
                        dir = Path.GetDirectoryName(mainExePath);
                    if (dir != null)
                        c.OutputFolder = new DirectoryInfo(Path.Combine(dir, NetfuserFactory.NetfuserName));
                }

            ctx.OfType<NetfuserEvent.LoadSourceModules>().Take(1).Subscribe(e =>
            {
                e.Sources.Add(
                    ModuleDefMD.Load(mainExePath, new ModuleCreationOptions(ctx.ModuleContext)));
            });
            return ctx;
        }

        /// <summary>
        /// Load source assembly into the Netfuser context. May be called multiple times to load multiple assemblies.
        /// Normally it's better to use <see cref="LoadProject"/> or <see cref="LoadExecutable"/> instead, and 
        /// Netfuser will automatically resolve and load all referenced assemblies.
        /// Use only if you want to merge/embed/copy additional assemblies not in the reference tree
        /// This method doesn't actually perform loading, instead it configures the context to load the
        /// assembly at the appropriate time, after the <see cref="IContext.Run()"/> method is called.
        /// </summary>
        /// <param name="ctx">Netfuser context</param>
        /// <param name="path">fully-qualified path to the assembly</param>
        /// <param name="treat">specify what to do with the assembly - merge, embed or copy. If not specified, Netfuser will decide automatically</param>
        /// <returns>Netfuser context</returns>
        public static IContext LoadAssembly(this IContext ctx, string path, ModuleTreat? treat = null)
        {
            ModuleDef md = null;
            ctx.OfType<NetfuserEvent.LoadSourceModules>().Take(1).Subscribe(e =>
            {
                e.Sources.Add(md = ModuleDefMD.Load(path, new ModuleCreationOptions(ctx.ModuleContext)));
            });
            if (treat.HasValue)
                ctx.OfType<NetfuserEvent.ResolveSourceModules>().Subscribe(e =>
                {
                    if (e.Module == md)
                        e.Treat = treat.Value;
                });
            return ctx;
        }

        #endregion

        #region Specifying what to do with referenced assembles

        /// <summary>
        /// Convenience method to tell Netfuser which assemblies to exclude from processing.
        /// Only assemblies directly or indirectly referenced by the loaded assemblies will be considered.
        /// Normally should not be used, as it may break the resulting assembly.
        /// Alternatively, observe <see cref="NetfuserEvent.ResolveSourceModules"/> event for more complex matching of modules 
        /// This method only schedules the operation and returns immediately. The operation itself will be performed at
        /// the appropriate time, after the <see cref="IContext.Run()"/> method is called.
        /// </summary>
        /// <param name="ctx">Netfuser context</param>
        /// <param name="re">Assemblies with the names matching any of these regular expressions will be excluded from the processing</param>
        /// <returns>Netfuser context</returns>
        public static IContext IgnoreReferencedAssemblies(this IContext ctx, params Regex[] re)
        {
            ctx.OfType<NetfuserEvent.ResolveSourceModules>().Subscribe(e =>
            {
                if (re.Any(r => r.IsMatch(e.Module.Assembly.Name)))
                    e.Treat = ModuleTreat.Ignore;
            });
            return ctx;
        }

        /// <summary>
        /// Convenience method to tell Netfuser which assemblies to copy beside the resulting assembly.
        /// Only assemblies directly or indirectly referenced by the loaded assemblies will be considered.
        /// Alternatively, observe <see cref="NetfuserEvent.ResolveSourceModules"/> event for more complex matching of modules 
        /// This method only schedules the operation and returns immediately. The operation itself will be performed at
        /// the appropriate time, after the <see cref="IContext.Run()"/> method is called.
        /// </summary>
        /// <param name="ctx">Netfuser context</param>
        /// <param name="re">Assemblies with the names matching any of these regular expressions will be copied in the folder of the target assembly (will not be merged nor embedded) </param>
        /// <returns>Netfuser context</returns>
        public static IContext CopyReferencedAssemblies(this IContext ctx, params Regex[] re)
        {
            ctx.OfType<NetfuserEvent.ResolveSourceModules>().Subscribe(e =>
            {
                if (re.Any(r => r.IsMatch(e.Module.Assembly.Name)))
                    e.Treat = ModuleTreat.Copy;
            });
            return ctx;
        }

        /// <summary>
        /// Convenience method to tell Netfuser which modules to merge into the resulting assembly.
        /// Merging is performed before obfuscation, so all merged modules will be obfuscated.
        /// Only assemblies directly or indirectly referenced by the loaded assemblies will be considered.
        /// Alternatively, observe <see cref="NetfuserEvent.ResolveSourceModules"/> event for more complex matching of modules 
        /// This method only schedules the operation and returns immediately. The operation itself will be performed at
        /// the appropriate time, after the <see cref="IContext.Run()"/> method is called.
        /// </summary>
        /// <param name="ctx">Netfuser context</param>
        /// <param name="re">Assemblies with the names matching any of these regular expressions will be merged with the target assembly</param>
        /// <returns>Netfuser context</returns>
        public static IContext MergeReferencedAssemblies(this IContext ctx, params Regex[] re)
        {
            ctx.OfType<NetfuserEvent.ResolveSourceModules>().Subscribe(e =>
            {
                if (re.Any(r => r.IsMatch(e.Module.Assembly.Name)))
                    e.Treat = ModuleTreat.Merge;
            });
            return ctx;
        }

        /// <summary>
        /// Convenience method to tell Netfuser which assemblies to embed into the resulting assembly.
        /// Modules of embedded assemblies are not obfuscated, but may be compressed and/or encrypted.
        /// Embedded assemblies are added into the target assembly as resources and are automatically unpacked/decrypted when needed.
        /// Only assemblies directly or indirectly referenced by the loaded assemblies will be considered.
        /// Alternatively, observe <see cref="NetfuserEvent.ResolveSourceModules"/> event for more complex matching of modules 
        /// This method only schedules the operation and returns immediately. The operation itself will be performed at
        /// the appropriate time, after the <see cref="IContext.Run()"/> method is called.
        /// </summary>
        /// <param name="ctx">Netfuser context</param>
        /// <param name="re">Assemblies with the names matching any of these regular expressions will be embedded into the target assembly</param>
        /// <returns>Netfuser context</returns>
        public static IContext EmbedReferencedAssemblies(this IContext ctx, params Regex[] re)
        {
            ctx.OfType<NetfuserEvent.ResolveSourceModules>().Subscribe(e =>
            {
                if (re.Any(r => r.IsMatch(e.Module.Assembly.Name)))
                    e.Treat = ModuleTreat.Embed;
            });
            return ctx;
        }

        #endregion

        #region Embedding assemblies and other resources

        /// <summary>
        /// Obtain instance of the plugin in charge of embedding.
        /// Use this if you want to add additional .NET resources to the resulting assembly, see <see cref="IEmbedder"/>
        /// </summary>
        /// <param name="ctx">Netfuser context</param>
        /// <param name="name">Name of this embedder. Currently the name <see cref="NetfuserFactory.EmbedderIndexName"/> has special meaning
        /// to embed referenced assemblies and inject additional code to load them on demand,
        /// use any other name for other types of resources</param>
        /// <returns>instance of <see cref="IEmbedder"/> plugin </returns>
        public static IEmbedder Embedder(this IContext ctx, string name)
        {
            var c = (IContextImpl)ctx;
            return c.Plugin<IEmbedder>(() => new EmbedderPlugin(c, name), name);
        }

        /// <summary>
        /// Specify additional assemblies to embed.
        /// This allows to embed assemblies not in reference tree.
        /// Normally should not be used. See <see cref="EmbedReferencedAssemblies"/> if you want to embed referenced assembly.
        /// This method only schedules the operation and returns immediately. The operation itself will be performed at
        /// the appropriate time, after the <see cref="IContext.Run()"/> method is called.
        /// </summary>
        /// <param name="ctx">Netfuser context</param>
        /// <param name="modules">Netfuser will embed assemblies that contain these modules</param>
        /// <returns>Netfuser context</returns>
        public static IContext EmbedAssemblies(this IContext ctx, params ModuleDef[] modules)
        {
            var embedder = ctx.Embedder(NetfuserFactory.EmbedderIndexName);
            foreach (var m in modules)
            {
                var emb = new Embedding((IContextImpl)ctx, m.Assembly.FullName, new ReadableFile(m.Location));
                emb.Properties.Add(ResourceEntry.KeyIsAssembly, true.ToString());
                embedder.Add(emb);
            }
            return ctx;
        }

        /// <summary>
        /// Returns .deps.json parser
        /// </summary>
        /// <param name="ctx">Netfuser context</param>
        /// <returns>Netfuser context</returns>
        public static IDepsJsonPlugin Deps(this IContext ctx)
        {
            var c = (IContextImpl)ctx;
            return c.Plugin<IDepsJsonPlugin>(() => new DepsJsonPlugin(c));
        }

        /// <summary>
        /// Embeds native libraries exposed via .deps.json
        /// </summary>
        /// <param name="ctx">Netfuser context</param>
        /// <returns>Netfuser context</returns>
        public static IContext EmbedNativeLibraries(this IContext ctx)
        {
            var deps = ctx.Deps();
            ctx.OfType<NetfuserEvent.WillMergeModules>().Subscribe(e =>
            {
                var c = (IContextImpl)ctx;
                var dc = deps.Deps;
                if (dc != null)
                {
                    var embedder = ctx.Embedder(NetfuserFactory.EmbedderIndexName);
                    var bd = Path.GetDirectoryName(c.MainSourceModule.Location);
                    foreach (var nm in dc.RuntimeLibraries.SelectMany(l => l.NativeLibraryGroups))
                        foreach (var p in nm.AssetPaths)
                        {
                            var fp = Path.Combine(bd, p);
                            if (File.Exists(fp))
                            {
                                var emb = new Embedding(c, Path.GetFileName(p), new ReadableFile(fp));
                                emb.Properties.Add(ResourceEntry.KeyRid, nm.Runtime);
                                emb.Properties.Add(ResourceEntry.KeyPath, p);
                                emb.Properties.Add(ResourceEntry.KeyIsNativeLib, true.ToString());
                                embedder.Add(emb);
                            }
                        }
                    foreach (var nm in dc.RuntimeLibraries.SelectMany(l => l.RuntimeAssemblyGroups).Where(m => !string.IsNullOrEmpty(m.Runtime)))
                        foreach (var p in nm.RuntimeFiles)
                        {
                            var fp = Path.Combine(bd, p.Path);
                            if (File.Exists(fp))
                            {
                                // using var m=ModuleDefMD.Load(fp);
                                var n = AssemblyName.GetAssemblyName(fp);

                                var emb = new Embedding(c, n.FullName, new ReadableFile(fp));
                                emb.Properties.Add(ResourceEntry.KeyPath, p.Path);
                                emb.Properties.Add(ResourceEntry.KeyRid, nm.Runtime);
                                emb.Properties.Add(ResourceEntry.KeyIsAssembly, true.ToString());
                                embedder.Add(emb);
                            }
                        }
                }
            });
            return ctx;
        }

        #endregion

        #region Obfuscating metadata (names of the namespaces, types, methods, fields, events, properties...)

        /// <summary>
        /// Use to enable obfuscation of metadata names (namespaces, types, methods,
        /// fields, events, properties, parameters) 
        /// This method only schedules the operation and returns immediately. The operation itself will be performed at
        /// the appropriate time, after the <see cref="IContext.Run()"/> method is called.
        /// </summary>
        /// <param name="ctx">Netfuser context</param>
        /// <param name="options">Mangler options</param>
        /// <returns>Netfuser context</returns>
        public static IContext MangleMetadata(this IContext ctx, MetadataManglerOptions options = null)
        {
            var c = (IContextImpl)ctx;
            c.Register<IMetadataMangler>(new MetadataManglerPlugin(c, options ?? new MetadataManglerOptions()));
            return ctx;
        }

        /// <summary>
        /// Use to exclude names from obfuscation
        /// This method only schedules the operation and returns immediately. The operation itself will be performed at
        /// the appropriate time, after the <see cref="IContext.Run()"/> method is called.
        /// </summary>
        /// <param name="ctx">Netfuser context</param>
        /// <param name="type">Apply to metadata elements of the specified type (or types) </param>
        /// <param name="fn">function that returns types of metadata types to be excluded, or 0 if given element should not be excluded</param>
        /// <returns>Netfuser context</returns>
        public static IContext PreserveNames(this IContext ctx, MetaType type,
            Func<IMemberDef, MetaType> fn)
        {
            var ns = ((IContextImpl)ctx).Plugin<INaming>();
            ctx.OfType<NetfuserEvent.TypeMapped>().Subscribe(tme =>
            {
                var tm = tme.Mapping;
                var preserved = ns.Preserved(tm);
                var mask = type & (MetaType.Namespace | MetaType.Type);
                if (mask != 0 && (preserved & mask) == 0)
                    preserved |= fn(tm.Source);
                if ((type & MetaType.Member) != 0)
                {
                    mask = type & MetaType.Method;
                    if (mask != 0 && (preserved & mask) == 0)
                        foreach (var member in tm.Source.Methods)
                            preserved |= fn(member);
                    mask = type & MetaType.Field;
                    if (mask != 0 && (preserved & mask) == 0)
                        foreach (var member in tm.Source.Fields)
                            preserved |= fn(member);
                    mask = type & MetaType.Property;
                    if (mask != 0 && (preserved & mask) == 0)
                        foreach (var member in tm.Source.Properties)
                            preserved |= fn(member);
                    mask = type & MetaType.Event;
                    if (mask != 0 && (preserved & mask) == 0)
                        foreach (var member in tm.Source.Events)
                            preserved |= fn(member);
                }

                ns.Preserve(tm, preserved);
            });
            return ctx;
        }

        #endregion

        #region Obfuscating control flow

        /// <summary>
        /// Use to obfuscate control flow
        /// This method only schedules the operation and returns immediately. The operation itself will be performed at
        /// the appropriate time, after the <see cref="IContext.Run()"/> method is called.
        /// </summary>
        /// <param name="ctx">Netfuser context</param>
        /// <param name="options">Obfuscation options</param>
        /// <returns>Netfuser context</returns>
        public static IContext MangleControlFlow(this IContext ctx, CFMangleOptions options = null)
        {
            var c = (IContextImpl)ctx;
            c.Register<ICFMangler>(new CFManglerPlugin(c, options ?? new CFMangleOptions()));
            c.Register<ICFMangleMethod>(new Jump(c));
            c.Register<ICFMangleMethod>(new Switch(c));
            return ctx;
        }

        #endregion

        #region Obfuscating strings

        /// <summary>
        /// Use this to enable string splitting, i.e. obfuscating smaller parts of the larger string using different methods
        /// Obfuscated parts are de-mangled and concatenated during the run time  
        /// This method only schedules the operation and returns immediately. The operation itself will be performed at
        /// the appropriate time, after the <see cref="IContext.Run()"/> method is called.
        /// </summary>
        /// <param name="ctx">Netfuser context</param>
        /// <param name="options">Splitting options</param>
        /// <returns>Netfuser context</returns>
        public static IContext SplitStringsByFrequency(this IContext ctx, FrequencySplitterOptions options = null)
        {
            var c = (IContextImpl)ctx;
            c.Register<IStringSplitter>(new FrequencySplitter(c, options ?? new FrequencySplitterOptions()));
            return ctx;
        }

        /// <summary>
        /// Use this to add a no-obfuscation method to the list of methods.
        /// No-obfuscation means that the string (or the part of the string) is included as is (without any changes)
        /// into the resulting assembly.
        /// Useful for debugging or if you use other obfuscation methods targeting specific strings,
        /// and don't care about the rest of the strings.
        /// This method only schedules the operation and returns immediately. The operation itself will be performed at
        /// the appropriate time, after the <see cref="IContext.Run()"/> method is called.
        /// </summary>
        /// <param name="ctx">Netfuser context</param>
        /// <returns>Netfuser context</returns>
        public static IContext MangleStringsAsIs(this IContext ctx)
        {
            var c = (IContextImpl)ctx;
            c.Register<IStringMangleMethod>(new AsIs(c));
            return ctx;
        }

        /// <summary>
        /// Use this to add a string-to-int obfuscation method to the list of methods.
        /// Transforms smaller strings (up to 32 significant bits) into integers, and mangles the integers using <see cref="IIntMangler"/>
        /// Useful together with string splitter (because normally we there aren't too many short strings in the assembly)
        /// This method only schedules the operation and returns immediately. The operation itself will be performed at
        /// the appropriate time, after the <see cref="IContext.Run()"/> method is called.
        /// </summary>
        /// <param name="ctx">Netfuser context</param>
        /// <returns>Netfuser context</returns>
        public static IContext MangleStringsAsInt(this IContext ctx)
        {
            var c = (IContextImpl)ctx;
            c.Register<IStringMangleMethod>(new AsInt(c));
            return ctx;
        }

        static Injectable InjectableStringMangler(this IContext ctx)
        {
            const string name = "injectable";
            var c = (IContextImpl)ctx;
            return (Injectable)c.Plugin<IStringMangleMethod>(() => new Injectable(c, name), name);
        }

        /// <summary>
        /// Mangle strings using custom manger and de-mangler.
        /// This method only schedules the operation and returns immediately. The operation itself will be performed at
        /// the appropriate time, after the <see cref="IContext.Run()"/> method is called.
        /// </summary>
        /// <param name="ctx">Netfuser context</param>
        /// <param name="mangler">function that mangles string and returns its mangled representation</param>
        /// <param name="demangler">function that de-mangles and returns de-mangled string. <remarks>IMPORTANT: </remarks>
        /// this function MUST be in one of the source modules that will be merged (not embedded or copied!) into
        /// the target assembly. The function MUST be static</param>
        /// <returns>Netfuser context</returns>
        public static IContext MangleStrings(this IContext ctx, Func<string, string> mangler,
            Func<string, string> demangler)
        {
            var c = (IContextImpl)ctx;
            var inj = ctx.InjectableStringMangler();
            inj.Add(new DelegateCodec<string, string>(c, mangler, demangler));
            return ctx;
        }

        public static IContext MangleStrings(this IContext ctx, Func<string, string> mangler,
            IStringDemangler demangler)
        {
            var c = (IContextImpl)ctx;
            var inj = ctx.InjectableStringMangler();
            inj.Add(new DelegateCodec<string, string>(c, mangler, demangler.Demangler));
            ctx.OfType<NetfuserEvent.InjectTypes>().Take(1).Subscribe(ev =>
            {
                ev.Add(typeof(IStringDemangler));
                ev.Add(demangler.GetType());
            });
            return ctx;
        }

        #endregion

        #region Saving the result

        /// <summary>
        /// Write the resulting assembly and satellite files (.pdb, .json, .config etc)
        /// </summary>
        /// <param name="ctx">Netfuser context</param>
        /// <param name="dest">destination folder. Normally no need to specify, Netfuser will detect automatically
        /// based on your project or executable location</param>
        /// <returns>Netfuser context</returns>
        public static IContext WriteAssembly(this IContext ctx, DirectoryInfo dest = null)
        {
            var c = (IContextImpl)ctx;
            c.Register<IAssemblyWriterPlugin>(new AssemblyWriterPlugin(c, dest));
            return ctx;
        }

        /// <summary>
        /// Write the resulting assembly and satellite files (.pdb, .json, .config etc)
        /// </summary>
        /// <param name="ctx">Netfuser context</param>
        /// <param name="dest">destination folder. Normally no need to specify, Netfuser will detect automatically
        /// based on your project or executable location</param>
        /// <returns>Netfuser context</returns>
        public static IContext WriteAssembly(this IContext ctx, string dest)
        {
            return WriteAssembly(ctx, dest == null ? null : new DirectoryInfo(dest));
        }

        /// <summary>
        /// Writes names.xml file that contains original names of all re-named metadata, to help decode exception stacks
        /// </summary>
        /// <param name="ctx">Netfuser context</param>
        /// <param name="dest">destination folder. Normally no need to specify, Netfuser will detect automatically
        /// based on your project or executable location</param>
        /// <returns>Netfuser context</returns>
        public static IContext WriteNameMap(this IContext ctx, string dest = null)
        {
            var c = (IContextImpl)ctx;
            c.Register<IMapWriterPlugin>(new MapWriterPluginPlugin(c, dest));
            return ctx;
        }

        #endregion

        #region Miscellaneous

        /// <summary>
        /// Turns debug mode on or off.
        /// In debug mode resulting assembly will have readable (slightly modified) names 
        /// to simplify analysis of stack traces and reverse engineering.
        /// Debug mode is ON by default in DEBUG build and OFF in Release build.
        /// Don't use Debug mode for production assemblies
        /// </summary>
        /// <param name="ctx">Netfuser context</param>
        /// <param name="debugMode">debug mode flag</param>
        /// <returns>Netfuser context</returns>
        public static IContext SetDebugMode(this IContext ctx, bool debugMode)
        {
            ((ContextImpl)ctx).DebugMode = debugMode;
            return ctx;
        }

        /// <summary>
        /// Adds <see cref="IIntMangler"/> to this context, or returns existing <see cref="IIntMangler"/> if it has already been added 
        /// </summary>
        /// <param name="ctx">>Netfuser context</param>
        /// <returns><see cref="IIntMangler"/> </returns>
        public static IIntMangler IntMangler(this IContext ctx)
        {
            var c = (IContextImpl)ctx;
            return c.Plugin<IIntMangler>(() => new IntManglerPlugin(c));
        }

        /// <summary>
        /// Adds <see cref="IFeatureInjector"/> to this context, or returns existing <see cref="IFeatureInjector"/> if it has already been added 
        /// </summary>
        /// <param name="ctx">>Netfuser context</param>
        /// <returns><see cref="IFeatureInjector"/> </returns>
        public static IFeatureInjector FeatureInjector(this IContext ctx)
        {
            var c = (IContextImpl)ctx;
            return c.Plugin<IFeatureInjector>(() => new FeatureInjectorPlugin(c));
        }

        #endregion

        /// <summary>
        /// Applies default configuration
        /// </summary>
        /// <param name="ctx">Netfuser context</param>
        /// <returns>Netfuser context</returns>
        public static IContext ApplyDefaults(this IContext ctx)
        {
            return ctx
                .EmbedNativeLibraries()
                .MangleMetadata()
#if !DEBUG
                .MangleControlFlow()
#endif
                .WriteNameMap()
                .WriteAssembly();

        }
    }
}