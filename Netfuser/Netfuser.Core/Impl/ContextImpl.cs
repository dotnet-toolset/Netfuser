using Base.Cil;
using Base.Collections;
using dnlib.DotNet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Base.Lang;
using Base.Logging;
using ILogger = Base.Logging.ILogger;
using System.IO;
using Netfuser.Core.Embedder.Compression;
using Netfuser.Core.Hierarchy;
using Netfuser.Core.Impl.Embedder.Compression;
using Netfuser.Core.Impl.Hierarchy;
using Netfuser.Core.Impl.Manglers.Ints;
using Netfuser.Core.Impl.Merger;
using Netfuser.Core.Impl.Naming;
using Netfuser.Core.Manglers.Ints;
using Netfuser.Core.Naming;
using Netfuser.Core.Rng;
using Netfuser.Dnext;
using Netfuser.Dnext.Impl;

namespace Netfuser.Core.Impl
{
    class ContextImpl : IContextImpl, IResolver, IAssemblyResolver
    {
        private readonly Dictionary<Type, Dictionary<string, IPlugin>> _plugins;
        private readonly Subject<NetfuserEvent> _subject;
        private readonly Dictionary<ITypeKey, TypeMapping> _typeMappings;
        private readonly Dictionary<ITypeKey, ResourceMapping> _resourceMappings;
        private readonly Dictionary<ITypeKey, TypeRef> _forwardedTypes;
        private readonly ConcurrentDictionary<ITypeKey, TypeDef> _externalTypeDefs;

        private ModuleContext _moduleContext;
        private IResolver _defaultResolver;
        private AssemblyResolver _defaultAssemblyResolver;
        private ModuleDef _mainSourceModule, _targetModule;
        private Importer _basicImporter;
        private IReadOnlyDictionary<ModuleTreat, IReadOnlySet<ModuleDef>> _sourceModules;
        public ILogger Logger { get; }
        public ModuleContext ModuleContext => _moduleContext;
        public ModuleDef MainSourceModule => _mainSourceModule;
        public ModuleDef TargetModule => _targetModule;
        public IReadOnlyDictionary<ModuleTreat, IReadOnlySet<ModuleDef>> SourceModules => _sourceModules;
        public IReadOnlyDictionary<ITypeKey, TypeMapping> MappedTypes => _typeMappings;
        public IReadOnlyDictionary<ITypeKey, ResourceMapping> MappedResources => _resourceMappings;
        public Importer BasicImporter => _basicImporter;

        public Importer GetImporter(TypeDef type)
        {
            var mapper = new Mapper(this);
            var importer = new Importer(TargetModule, ImporterOptions.TryToUseDefs,
                GenericParamContext.Create(type), mapper);
            mapper.Init(importer);
            return importer;
        }

        public Importer GetImporter(TypeDef type, MethodDef method)
        {
            var mapper = new Mapper(this);
            var importer = new Importer(TargetModule, ImporterOptions.TryToUseDefs,
                new GenericParamContext(type, method),
                mapper);
            mapper.Init(importer);
            return importer;
        }

        DirectoryInfo IContextImpl.OutputFolder { get; set; }
        public bool DebugMode { get; internal set;}

        public ContextImpl()
        {
#if DEBUG
            DebugMode=true;
#endif
            _plugins = new Dictionary<Type, Dictionary<string, IPlugin>>();
            _subject = new Subject<NetfuserEvent>();
            _typeMappings = new Dictionary<ITypeKey, TypeMapping>();
            _resourceMappings = new Dictionary<ITypeKey, ResourceMapping>();
            _forwardedTypes = new Dictionary<ITypeKey, TypeRef>();
            _externalTypeDefs = new ConcurrentDictionary<ITypeKey, TypeDef>();
            Logger = LogManager.GetLogger("context");
            Register<IRngPlugin>(new RngPlugin(this));
            Register<IVTablePlugin>(new VTablePlugin(this));
            Register<INaming>(new Naming.Naming(this));
        }

        public IDisposable Subscribe(IObserver<NetfuserEvent> observer)
        {
            return _subject.Subscribe(observer);
        }


        public T Fire<T>(T ev) where T : NetfuserEvent
        {
            _subject.OnNext(ev);
            return ev;
        }

        public Exception Error(string message)
        {
            Logger.Fatal(message);
            throw new Exception(message);
        }

        void Initialize()
        {
            Register<IIntMangler>(new IntManglerPlugin(this));
            Register<ICompression>(new DeflateCompression(this));
            Register<ICompression>(new LzmaCompression(this));
            Register<IResNameFixer>(new ResNameFixer(this));

            _moduleContext = new ModuleContext(this, this);
            _defaultAssemblyResolver = new AssemblyResolver(_moduleContext);
            _defaultResolver = new Resolver(this);
            var ev = Fire(new NetfuserEvent.Initialize(this, _moduleContext));
            if (ev.ModuleContext != _moduleContext)
                _moduleContext = ev.ModuleContext ?? throw Error("module context cannot be null");
        }

        IEnumerable<ModuleDef> LoadSourceModules()
        {
            var ev = Fire(new NetfuserEvent.LoadSourceModules(this));
            if (ev.Sources.Count == 0)
                throw Error("at least one source module must be added");
            var rejected = new HashSet<ModuleDef>();
            foreach (var source in ev.Sources)
                if (source.EntryPoint != null && !rejected.Contains(source))
                {
                    var evm = Fire(new NetfuserEvent.SelectMainModule(this, source) { IsMain = true });
                    if (evm.IsMain)
                    {
                        _mainSourceModule = source;
                        break;
                    }

                    rejected.Add(source);
                }

            if (_mainSourceModule == null)
                foreach (var source in ev.Sources)
                    if (!rejected.Contains(source))
                    {
                        var evm = Fire(new NetfuserEvent.SelectMainModule(this, source));
                        if (evm.IsMain)
                        {
                            _mainSourceModule = source;
                            break;
                        }

                        rejected.Add(source);
                    }
            if (_mainSourceModule != null)
            {
                _mainSourceModule.Assembly.TryGetOriginalTargetFrameworkAttribute(out var fw, out var ver, out _);
                if (fw == ".NETCoreApp")
                {
                    _defaultAssemblyResolver.UseGAC = false;
                    _defaultAssemblyResolver.EnableFrameworkRedirect = false;
                    var bitness = (_mainSourceModule?.GetPointerSize(IntPtr.Size) ?? IntPtr.Size) * 8;
                    var dotNetCorePaths = new DotNetCorePathProvider().TryGetDotNetCorePaths(ver, bitness);
                    _defaultAssemblyResolver.PreSearchPaths.AddRange(dotNetCorePaths);
                }
            }
            return ev.Sources;
        }

        void CreateTargetModule()
        {
            ModuleDef targetModule = null;
            var mainmod = _mainSourceModule;
            if (mainmod != null)
            {
                var targetAssembly = new AssemblyDefUser(mainmod.Assembly.Name, mainmod.Assembly.Version,
                    mainmod.Assembly.PublicKey, mainmod.Assembly.Culture)
                {
                    Attributes = mainmod.Assembly.Attributes,
                    HashAlgorithm = mainmod.Assembly.HashAlgorithm,
                };
                targetModule = new ModuleDefUser(mainmod.Name, mainmod.Mvid, mainmod.CorLibTypes.AssemblyRef)
                {
                    RuntimeVersion = mainmod.RuntimeVersion,
                    Cor20HeaderRuntimeVersion = mainmod.Cor20HeaderRuntimeVersion,
                    Cor20HeaderFlags = mainmod.Cor20HeaderFlags,
                    DllCharacteristics = mainmod.DllCharacteristics,
                    TablesHeaderVersion = mainmod.TablesHeaderVersion,
                    Machine = mainmod.Machine,
                    Context = mainmod.Context,
                    Kind = mainmod.Kind
                };
                if (mainmod.PdbState != null)
                    targetModule.CreatePdbState(mainmod.PdbState.PdbFileKind);
                targetAssembly.Modules.Add(targetModule);
            }

            var ev = Fire(new NetfuserEvent.CreateTargetModule(this) { Target = targetModule });
            _targetModule = ev.Target;
            if (_targetModule == null)
                throw Error("could not create target module");
            var mapper = new Mapper(this);
            mapper.Init(_basicImporter = new Importer(_targetModule, ImporterOptions.TryToUseDefs,
                new GenericParamContext(), mapper));
        }

        void MapForwardedTypes(IEnumerable<ModuleDef> modules)
        {
            foreach (var mod in modules)
                foreach (var exp in mod.ExportedTypes)
                    if (exp.IsForwarder)
                        _forwardedTypes.Add(DnextFactory.NewTypeKey(mod, exp.FullName), exp.ToTypeRef());
        }

        void MapResources(IEnumerable<ModuleDef> modules)
        {
            var resourcesByName = new Dictionary<string, Resource>();

            foreach (var module in modules)
                if (module.HasResources)
                    foreach (var source in module.Resources)
                        Import(module, source);

            var inject = Fire(new NetfuserEvent.InjectResources(this)).Resources;
            foreach (var resource in inject)
                Import(null, resource);

            Fire(new NetfuserEvent.ResourcesDeclared(this));

            void Import(ModuleDef module, Resource source)
            {
                string name;
                if (resourcesByName.TryGetValue(source.Name, out var target))
                    name = Duplicate(module, source, target);
                else name = source.Name;

                target = Fire(new NetfuserEvent.CreateResource(this, module, source) { Target = Clone(source) }).Target;
                _targetModule.Resources.Add(target);
                resourcesByName.Add(name, target);

                var key = DnextFactory.NewTypeKey(module, source);
                var tm = new ResourceMapping(module, source, target);
                _resourceMappings.Add(key, tm);
                Fire(new NetfuserEvent.ResourceMapped(this, tm));
            }

            string Duplicate(ModuleDef sourceModule, Resource source, Resource target)
            {
                var ev = new NetfuserEvent.DuplicateResource(this, sourceModule, source, target);
                var c = 0;
                do
                {
                    ev.RenameInto = source.Name + "_" + ++c;
                } while (resourcesByName.ContainsKey(ev.RenameInto));

                Fire(ev);
                var result = ev.RenameInto;
                if (string.IsNullOrEmpty(result) || resourcesByName.ContainsKey(result))
                    throw Error("resource name must be unique, non-null and non-empty");
                return result;
            }

            Resource Clone(Resource source)
            {
                switch (source)
                {
                    case EmbeddedResource er:
                        return er.SharedClone();
                    case AssemblyLinkedResource alr:
                        return new AssemblyLinkedResource(alr.Name, alr.Assembly, alr.Attributes);
                    case LinkedResource lr:
                        // we will later replace the File property with the clone FileDef, unless someone else changes it
                        return new LinkedResource(lr.Name, lr.File, lr.Attributes);
                }

                throw new NotSupportedException();
            }
        }

        IEnumerable<TypeDef> GetInjectedTypes()
        {
            var ev = new NetfuserEvent.InjectTypes(this);
            var mods = new ConcurrentDictionary<Assembly, ModuleDef>();
            Fire(ev);
            if (ev.Types.Count == 0 && ev.TypeDefs.Count == 0) return Empty.Array<TypeDef>();
            var ctx = ModuleDef.CreateModuleContext();
            foreach (var t in ev.Types)
            {
                var mod = mods.GetOrAdd(t.Assembly, a => ModuleDefMD.Load(a.Location, ctx));
                var imp = new Importer(mod, ImporterOptions.TryToUseDefs);
                var tr = imp.Import(t);
                ev.TypeDefs.Add(tr.ResolveTypeDefThrow());
            }

            return ev.TypeDefs;
        }

        /// <summary>
        /// Members of the following types in the source modules will be merged into the single type with the corresponding name 
        /// </summary>
        static readonly Regex[] MergeableTypes =
        {
            new Regex("^<Module>$", RegexOptions.Compiled),
            new Regex("^<PrivateImplementationDetails>$", RegexOptions.Compiled),
            new Regex("^<PrivateImplementationDetails>/__StaticArrayInitTypeSize=", RegexOptions.Compiled),
            new Regex("^XamlGeneratedNamespace.GeneratedInternalTypeHelper$", RegexOptions.Compiled),
        };

        void ImportSkeletons(IEnumerable<ModuleDef> modules, List<TypeMerger> typeCloners)
        {
            var typesByFullName = new Dictionary<string, TypeDef>();
            if (_targetModule.GlobalType != null)
                typesByFullName.Add(_targetModule.GlobalType.FullName, _targetModule.GlobalType);

            foreach (var r in modules.SelectMany(m => m.Types).Concat(GetInjectedTypes()))
                Import(r);

            static string MakeFullName(TypeDef source, string name)
            {
                if (source.DeclaringType != null)
                    return new StringBuilder(source.DeclaringType.FullName).Append('/').Append(name).ToString();
                return string.IsNullOrEmpty(source.Namespace)
                    ? name
                    : new StringBuilder(source.Namespace).Append('.').Append(name).ToString();
            }

            Fire(new NetfuserEvent.TypeSkeletonsImported(this));

            // returns null if members of source must be copied into target
            // or the name of the new type to copy members of source into
            string Duplicate(TypeDef source, TypeDef target)
            {
                // merge global type <Module> right away
                if (source.IsGlobalModuleType && target.IsGlobalModuleType) return null;
                var fullName = source.FullName;
                var ev = new NetfuserEvent.DuplicateType(this, source, target);
                var c = 0;
                if (!MergeableTypes.Any(r => r.IsMatch(fullName)))
                    do
                    {
                        ev.RenameInto = source.Name + "_" + ++c;
                    } while (typesByFullName.ContainsKey(MakeFullName(source, ev.RenameInto)));

                Fire(ev);
                return ev.RenameInto;
            }

            void Import(TypeDef source, TypeDef declaring = null)
            {
                string name;
                if (typesByFullName.TryGetValue(source.FullName, out var target))
                {
                    name = Duplicate(source, target);
                    if (name != null && typesByFullName.TryGetValue(MakeFullName(source, name), out var nt))
                    {
                        // if event handler returned existing name, it means we need to merge with that existing type
                        target = nt;
                        name = null;
                    }
                }
                else name = source.Name;

                if (name != null)
                {
                    var ev = Fire(new NetfuserEvent.CreateType(this, source)
                    { Target = new TypeDefUser(source.Namespace, name) { Attributes = source.Attributes } });
                    target = _targetModule.UpdateRowId(ev.Target);
                    if (declaring == null)
                        _targetModule.Types.Add(target);
                    else
                        declaring.NestedTypes.Add(target);
                    typesByFullName.Add(MakeFullName(source, name), target);
                }

                var key = source.CreateKey();
                var tm = new TypeMapping(source, target);
                _typeMappings.Add(key, tm);
                Fire(new NetfuserEvent.TypeMapped(this, tm));
                typeCloners.Add(TypeMerger.Create(this, tm, name == null));
                foreach (var nested in source.NestedTypes)
                    Import(nested, target);
            }
        }

        private void ImportDeclarations(List<TypeMerger> typeMergers)
        {
            foreach (var tm in typeMergers)
                tm.ImportDeclarations();
        }


        private void ImportTypes(List<TypeMerger> tms)
        {
            static void Import(Queue<TypeMerger> q)
            {
                while (q.Count > 0)
                    q.Dequeue().Run();
            }

            var ev = Fire(new NetfuserEvent.WillImportMembers(this) { ParallelTasks = Environment.ProcessorCount });
            // we use queue and clear the list to make sure we free the memory ASAP if the type has been merged
            if (ev.ParallelTasks > 0)
            {
                var partitions = tms.PartitionInto(ev.ParallelTasks)
                    .Select(p => new Queue<TypeMerger>(p)).ToList();
                tms.Clear();
                Parallel.ForEach(partitions, Import);
            }
            else
            {
                var q = new Queue<TypeMerger>(tms);
                tms.Clear();
                Import(q);
            }

            Fire(new NetfuserEvent.TypesImported(this));
        }

        public void Run()
        {
            Logger.Info("initializing");
            Initialize();
            Logger.Info("resolving module dependencies");
            _sourceModules = new ModrefWalker(this, LoadSourceModules()).Resolve();
            if (!_sourceModules.TryGetValue(ModuleTreat.Merge, out var modulesToMerge) || modulesToMerge.Count == 0)
                throw Error("nothing to merge");
            Fire(new NetfuserEvent.WillMergeModules(this, modulesToMerge.AsReadOnlySet()));
            Logger.Info("will merge the following modules: " +
                        modulesToMerge.Select(m => (string)m.Name).Join(", "));
            Logger.Info("creating target module");
            CreateTargetModule();
            Logger.Info("mapping forwarded types");
            MapForwardedTypes(modulesToMerge);
            if (_sourceModules.TryGetValue(ModuleTreat.Embed, out var modulesToEmbed) && modulesToEmbed.Count > 0)
            {
                this.EmbedAssemblies(modulesToEmbed.ToArray());
                Logger.Info("will embed the following modules: " +
                            modulesToEmbed.Select(m => (string)m.Name).Join(", "));
            }

            Logger.Info("mapping resources");
            MapResources(modulesToMerge);
            var typeMergers = new List<TypeMerger>();
            Logger.Info("importing type skeletons");
            ImportSkeletons(modulesToMerge, typeMergers);
            Logger.Info("importing declarations of type members");
            ImportDeclarations(typeMergers);
            Logger.Info("importing types");
            ImportTypes(typeMergers);
            foreach (var m in modulesToMerge)
                ModuleMerger.Create(this, m).Run();
            Fire(new NetfuserEvent.Complete(this));
            Logger.Info("all done");
        }

        private bool CheckMultiple<T>(string name) where T : class, IPlugin
        {
            var t = typeof(T);
            if (!t.IsInterface) throw Error("must specify interface as generic parameter");
            if (typeof(INamedPlugin).IsAssignableFrom(t))
                if (name == null)
                    throw Error("this plugin must have a name");
                else
                    return true;
            if (name != null)
                throw Error("this plugin must not have a name");
            return false;
        }

        public T Plugin<T>(string name = null) where T : class, IPlugin
        {
            CheckMultiple<T>(name);
            lock (_plugins)
                return _plugins.TryGetValue(typeof(T), out var map)
                    ? map.TryGetValue(name ?? string.Empty, out var plugin) ? (T)plugin : null
                    : null;
        }

        public IReadOnlyList<T> Plugins<T>() where T : class, INamedPlugin
        {
            lock (_plugins)
                return _plugins.TryGetValue(typeof(T), out var map)
                    ? map.Values.OfType<T>().AsReadOnlyList()
                    : Empty.Array<T>();
        }

        public T Plugin<T>(Func<T> creator, string name = null) where T : class, IPlugin
        {
            var multipleAllowed = CheckMultiple<T>(name);
            lock (_plugins)
            {
                if (!_plugins.TryGetValue(typeof(T), out var map))
                    _plugins.Add(typeof(T), map = new Dictionary<string, IPlugin>());
                var key = name ?? string.Empty;
                if (!map.TryGetValue(key, out var plugin))
                {
                    if (map.Count > 0 && !multipleAllowed)
                        throw Error("multiple plugins of this type is not allowed");
                    map.Add(key, plugin = creator());
                }
                // else if (add) throw Error("duplicate plugin");

                return (T)plugin;
            }
        }

        public void Register<T>(T plugin) where T : class, IPlugin
        {
            var name = plugin is INamedPlugin np ? np.Name : null;
            var multipleAllowed = CheckMultiple<T>(name);
            lock (_plugins)
            {
                if (!_plugins.TryGetValue(typeof(T), out var map))
                    _plugins.Add(typeof(T), map = new Dictionary<string, IPlugin>());
                var key = name ?? string.Empty;
                if (!map.TryGetValue(key, out var other))
                {
                    if (map.Count > 0 && !multipleAllowed)
                        throw Error("multiple plugins of this type is not allowed");
                    map.Add(key, plugin);
                }
                else throw Error("duplicate plugin");
            }

            plugin.Info("plugin registered");
        }

        internal void Unregister(IPlugin plugin)
        {
            var name = plugin is INamedPlugin np ? np.Name : string.Empty;
            lock (_plugins)
                foreach (var kv in _plugins)
                    if (kv.Key.IsInstanceOfType(plugin) && kv.Value.TryGetValue(name, out var p) && p == plugin)
                    {
                        kv.Value.Remove(name);
                        break;
                    }

            plugin.Info("plugin unregistered");
        }

        TypeDef ITypeResolver.Resolve(TypeRef typeRef, ModuleDef sourceModule)
        {
            var key = typeRef.CreateKey();
            if (_typeMappings.TryGetValue(key, out var tm)) return tm.Source;
            return _externalTypeDefs.GetOrAdd(key, k => _defaultResolver.Resolve(typeRef, sourceModule));
        }

        IMemberForwarded IMemberRefResolver.Resolve(MemberRef memberRef)
        {
            return _defaultResolver.Resolve(memberRef);
        }

        public AssemblyDef Resolve(IAssembly assembly, ModuleDef sourceModule)
        {
            if (assembly.IsCorLib() && sourceModule == _targetModule)
                return _defaultAssemblyResolver.Resolve(_targetModule.CorLibTypes.AssemblyRef, sourceModule);
            return _defaultAssemblyResolver.Resolve(assembly, sourceModule);
        }

        private class Mapper : ImportMapper
        {
            private readonly ContextImpl _context;
            private readonly INaming _ns;
            private Importer _importer;

            internal Mapper(ContextImpl context)
            {
                _context = context;
                _ns = context.Plugin<INaming>();
            }

            internal void Init(Importer importer)
            {
                _importer = importer;
            }

            public override ITypeDefOrRef Map(ITypeDefOrRef type)
            {
                var key = type.CreateKey();
                if (_context._forwardedTypes.TryGetValue(key, out var fwd))
                    return _importer.Import(fwd);
                return _context._typeMappings.TryGetValue(key, out var result)
                    ? result.Target
                    : null;
            }

            public override IField Map(FieldDef source)
            {
                if (_ns.FindNewName(source, out var tm, out var member))
                    return tm.Target.FindField(member);

                return base.Map(source);
            }

            public override MemberRef Map(MemberRef source)
            {
                if (_ns.FindNewName(source, out _, out var member))
                {
                    var result = _context._targetModule.UpdateRowId(new MemberRefUser(_context._targetModule, member));
                    result.Signature = _importer.Import(source.Signature);
                    result.Class =
                        _importer.Import(source.DeclaringType); // must import 'cause type may have generic args
                    return result;
                }

                return base.Map(source);
            }

            public override IMethod Map(MethodDef source)
            {
                if (_ns.FindNewName(source, out var tm, out var member))
                    return tm.Target.FindMethod(member, _importer.Import(source.MethodSig));
                return base.Map(source);
            }

            public override TypeRef Map(Type source)
            {
                if (source.Assembly.IsCorLib())
                    return CreateCorlibTypeRef(source);
                // it's tempting to use type mappings here as well, but we should not. dnlib's logic doesn't use mapper 
                // resolve members of imported System.Type's, so if we try to resolve System.Reflection.MemberInfo via
                // dnlib, even with the attached mapper, we will get MemberRef with the mapped TypeRef and unmapped member name
                // The workaround is call Importer.Import() twice, once for the System.Reflection.MemberInfo and then
                // for the returned MemberRef
                return base.Map(source);
            }


            TypeRef CreateCorlibTypeRef(Type type)
            {
                var module = _context._targetModule;
                if (!type.IsNested)
                    return module.UpdateRowId(new TypeRefUser(module, type.Namespace ?? string.Empty,
                        type.Name ?? string.Empty, module.CorLibTypes.AssemblyRef));
                return module.UpdateRowId(new TypeRefUser(module, string.Empty, type.Name ?? string.Empty,
                    CreateCorlibTypeRef(type.DeclaringType)));
            }
        }
    }
}