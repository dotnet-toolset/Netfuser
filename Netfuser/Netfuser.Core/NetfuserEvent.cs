using System;
using dnlib.DotNet;
using dnlib.DotNet.Pdb;
using System.Collections.Generic;
using System.Linq;
using Base.Collections;
using dnlib.DotNet.Emit;
using Netfuser.Core.Impl;
using Netfuser.Dnext;
using Netfuser.Dnext.Cil;

namespace Netfuser.Core
{
    /// <summary>
    /// Base class for Netfuser events
    /// </summary>
    public abstract class NetfuserEvent
    {
        /// <summary>
        /// Context that created this event
        /// </summary>
        public readonly IContext Context;

        protected NetfuserEvent(IContext context)
        {
            Context = context;
        }

        #region Initialization, loading and resolution of source modules

        /// <summary>
        /// Observe this event to change default initialization parameters.
        /// This event will be fired only once, when <see cref="IContext.Run()"/> starts, before any other event
        /// Default plugins may be removed or replaced at this point
        /// </summary>
        public class Initialize : NetfuserEvent
        {
            /// <summary>
            /// This module context will be used to locate assemblies and resolve types.
            /// May be changed by the event handler if custom resolving logic is required.
            /// However, it is advisable to fall back to this ModuleContext's resolving routines instead of the default
            /// ones, because our implementation provides some performance optimizations. 
            /// </summary>
            public ModuleContext ModuleContext;

            internal Initialize(IContext context, ModuleContext moduleContext)
                : base(context)
            {
                ModuleContext = moduleContext;
            }
        }

        /// <summary>
        /// Observe this event to add source modules to the list.
        /// This event will be fired only once, after the <see cref="NetfuserEvent.Initialize"/> event.
        /// There's no need to add referenced modules, they will be added automatically.
        /// Normally only the main module (the one with the entry point) needs to be added via
        /// <see cref="Extensions.LoadExecutable"/> or <see cref="Extensions.LoadProject"/>, all references will be
        /// resolved and added by the Netfuser.
        /// This is only useful if you need to add modules not directly or indirectly referenced by the main module. 
        /// </summary>
        public class LoadSourceModules : NetfuserEvent
        {
            /// <summary>
            /// Add module(s) to this list when handling the event
            /// </summary>
            public readonly List<ModuleDef> Sources;

            internal LoadSourceModules(IContext context)
                : base(context)
            {
                Sources = new List<ModuleDef>();
            }
        }

        /// <summary>
        /// This event is fired for every source module added by the <see cref="NetfuserEvent.LoadSourceModules"/> observer(s)
        /// to set the main module. It is fired before module references are resolved.
        /// By default, the first module with an entry point is treated as a main module.
        /// Observe this event to change the default behavior, set <see cref="NetfuserEvent.SelectMainModule.IsMain"/> accordingly.
        /// </summary>
        public class SelectMainModule : NetfuserEvent
        {
            public readonly ModuleDef Module;
            public bool IsMain;

            internal SelectMainModule(IContext context, ModuleDef module)
                : base(context)
            {
                Module = module;
            }
        }

        /// <summary>
        /// This event is fired for each module in the list of source modules, including the referenced modules.
        /// The purpose is to let Netfuser know what needs to be done with each module (ignore, copy, merge, embed),
        /// and also stop resolution of references for any given module (generally not a very good idea) 
        /// Observers may change read/write fields to alter Netfuser behavior.
        /// </summary>
        public class ResolveSourceModules : NetfuserEvent
        {
            /// <summary>
            /// Sources module(s) reference this module
            /// </summary>
            public readonly ModuleDef Module;

            /// <summary>
            /// Tells Netfuser what should be done with this module.
            /// By default, this is set to <see cref="ModuleTreat.Merge"/> if the module is located in the same folder with the main assembly,
            /// otherwise it is set to  <see cref="ModuleTreat.Ignore"/>
            /// </summary>
            public ModuleTreat Treat;

            /// <summary>
            /// Netfuser will not resolve this module's references if set to <c>false</c>.
            /// By default, this is set to <c>true</c> if the assembly is located in the same folder with the main assembly,
            /// otherwise it is set to <c>false</c>
            /// </summary>
            public bool ResolveReferences;

            internal ResolveSourceModules(IContext context, ModuleDef module)
                : base(context)
            {
                Module = module;
            }
        }

        /// <summary>
        /// This event is for information only, provides finalized list of modules to be merged into the target module
        /// </summary>
        public class WillMergeModules : NetfuserEvent
        {
            public readonly IReadOnlySet<ModuleDef> Modules;

            internal WillMergeModules(IContext context, IReadOnlySet<ModuleDef> modules)
                : base(context)
            {
                Modules = modules;
            }
        }

        /// <summary>
        /// Fired when the target module is created.
        /// Handlers may change properties of the new module or replace it with a different one
        /// </summary>
        public class CreateTargetModule : NetfuserEvent
        {
            /// <summary>
            /// Target module
            /// </summary>
            public ModuleDef Target;

            internal CreateTargetModule(IContext context)
                : base(context)
            {
            }
        }

        #endregion

        #region Resources

        /// <summary>
        /// Observe this event to provide additional resource(s) to be injected into the target module
        /// </summary>
        public class InjectResources : NetfuserEvent
        {
            /// <summary>
            /// Add additional resources to this set
            /// </summary>
            public readonly ISet<Resource> Resources;

            internal InjectResources(IContext context)
                : base(context)
            {
                Resources = new HashSet<Resource>();
            }

            /// <summary>
            /// Convenience method to add resources
            /// </summary>
            /// <param name="resource">resource to add</param>
            public void Add(Resource resource) => Resources.Add(resource);
        }

        /// <summary>
        /// Fired when new resource is about to be added into the target module.
        /// Handlers may change properties of the new resource or replace it.
        /// </summary>
        public class CreateResource : NetfuserEvent
        {
            /// <summary>
            /// If the resource is copied from one of the source modules, this will be set to the corresponding source module,
            /// otherwise it is <see langword="null"/>
            /// </summary>
            public readonly ModuleDef SourceModule;

            /// <summary>
            /// Original resource if copied from one of the source modules, or the resource supplied by the
            /// <see cref="NetfuserEvent.InjectResources"/> observer. 
            /// </summary>
            public readonly Resource Source;

            /// <summary>
            /// Corresponding resource in the target module. Cannot be <see langword="null"/>.
            /// Automatically created and populated by the Netfuser. May be replaced or changed by the observer(s)
            /// </summary>
            public Resource Target;

            internal CreateResource(IContext context, ModuleDef sourceModule, Resource source)
                : base(context)
            {
                SourceModule = sourceModule;
                Source = source;
            }
        }

        /// <summary>
        /// Fired when resource with the same name has already been queued for import into the target assembly.
        /// Default algorithm will append "_x" to the name, where x-simple counter that increments until the name is unique
        /// Observer(s) may specify a different name for the resource.
        /// </summary>
        public class DuplicateResource : NetfuserEvent
        {
            /// <summary>
            /// If the resource is copied from one of the source modules, this will be set to the corresponding source module,
            /// otherwise it is <see langword="null"/>
            /// </summary>
            public readonly ModuleDef SourceModule;

            /// <summary>
            /// Original resource if copied from one of the source modules, or the resource supplied by the
            /// <see cref="NetfuserEvent.InjectResources"/> observer. 
            /// </summary>
            public readonly Resource Source;

            /// <summary>
            /// Corresponding resource in the target module. Cannot be <see langword="null"/>.
            /// Automatically created and populated by the Netfuser. May be replaced or changed by the observer(s)
            /// </summary>
            public readonly Resource Target;

            /// <summary>
            /// New name for the target resource, change as you see fit, but see that it is unique
            /// </summary>
            public string RenameInto;

            internal DuplicateResource(IContext context, ModuleDef sourceModule, Resource source, Resource target)
                : base(context)
            {
                SourceModule = sourceModule;
                Source = source;
                Target = target;
            }
        }

        /// <summary>
        /// This event is for information only, fired for every resource that has been added to the target module
        /// </summary>
        public class ResourceMapped : NetfuserEvent
        {
            /// <summary>
            /// <see cref="ResourceMapping"/> entry that provides information about the original resource and the
            /// one being added to the target module
            /// </summary>
            public readonly ResourceMapping Mapping;

            internal ResourceMapped(IContext context, ResourceMapping mapping)
                : base(context)
            {
                Mapping = mapping;
            }
        }

        /// <summary>
        /// This event is fired after all resources (including injected ones) have been added to the target module.
        /// Resource names and/or contents may be changed at this point.
        /// </summary>
        public class ResourcesDeclared : NetfuserEvent
        {
            internal ResourcesDeclared(IContext context)
                : base(context)
            {
            }
        }

        #endregion

        #region Types

        /// <summary>
        /// Observe this event to provide additional <see cref="TypeDef"/>s and/or <see cref="Type"/>s to inject into the target module
        /// Dependencies will not be checked/resolved automatically. If injected type relies on other types,
        /// these will need to be injected as well (via this event).
        /// <see cref="TypeDef"/>s must be from the on-disk assembly (with Assembly.Location pointing at actual file location)  
        /// </summary>
        public class InjectTypes : NetfuserEvent
        {
            /// <summary>
            /// <see cref="Type"/>s to inject into the target module
            /// </summary>
            public readonly ISet<Type> Types;

            /// <summary>
            /// <see cref="TypeDef"/>s to inject into the target module
            /// </summary>
            public readonly ISet<TypeDef> TypeDefs;

            internal InjectTypes(IContext context)
                : base(context)
            {
                TypeDefs = new HashSet<TypeDef>();
                Types = new HashSet<Type>();
            }

            /// <summary>
            /// Convenience method to add <see cref="Type"/> to be injected
            /// </summary>
            /// <param name="type"></param>
            public void Add(Type type) => Types.Add(type);

            /// <summary>
            /// Convenience method to add list of <see cref="Type"/>s to be injected
            /// </summary>
            /// <param name="type"></param>
            public void Add(IEnumerable<Type> type) => Types.AddRange(type);

            /// <summary>
            /// Convenience method to add <see cref="TypeDef"/> to be injected
            /// </summary>
            /// <param name="type"></param>
            public void Add(TypeDef type) => TypeDefs.Add(type);

            /// <summary>
            /// Convenience method to add list of <see cref="TypeDef"/>s to be injected
            /// </summary>
            /// <param name="type"></param>
            public void Add(IEnumerable<TypeDef> type) => TypeDefs.AddRange(type);
        }

        /// <summary>
        /// This event is fired when new type is created in the target module.
        /// Observers may change properties of the new type (including the namespace and name) or replace the type altogether.
        /// </summary>
        public class CreateType : NetfuserEvent
        {
            /// <summary>
            /// Original type from the source module that corresponds to the <see cref="Target"/>
            /// </summary>
            public readonly TypeDef Source;

            /// <summary>
            /// Type that is being added to the target module and corresponds to the <see cref="Source"/>
            /// May NOT be <see langword="null"/>
            /// </summary>
            public TypeDef Target;

            internal CreateType(IContext context, TypeDef source)
                : base(context)
            {
                Source = source;
            }
        }

        /// <summary>
        /// This event is fired when type with the same full name has already been queued for import into the target module.
        /// Default algorithm will append "_x" to the name, where x-simple counter that increments until the name is unique
        /// Observer may specify a different name for the type or <see langword="null"/> to merge the types.
        /// </summary>
        public class DuplicateType : NetfuserEvent
        {
            /// <summary>
            /// Original type from the source module that corresponds to the <see cref="Target"/>
            /// </summary>
            public readonly TypeDef Source;

            /// <summary>
            /// Type that is being added to the target module and corresponds to the <see cref="Source"/>
            /// </summary>
            public readonly TypeDef Target;

            /// <summary>
            /// If <see langword="null"/>, the members of the source type will be merged into the target type,
            /// and the all references to the source type name will be replaced with the target type name.
            /// If not <see langword="null"/>, and the name already exists in the target assembly, the source type will be merged into that type.
            /// Otherwise, new target type will be created with this name.
            /// </summary>
            public string RenameInto;

            internal DuplicateType(IContext context, TypeDef source, TypeDef target)
                : base(context)
            {
                Source = source;
                Target = target;
            }
        }

        /// <summary>
        /// This event is fired when the source->target type mapping has been created. It is certain at this point that all members of
        /// <see cref="NetfuserEvent.TypeMapped.Mapping.Source"/> will be copied into the <see cref="NetfuserEvent.TypeMapped.Mapping.Target"/>
        /// Target's name and properties are not set in stone at this point and may be changed by plugins
        /// </summary>
        public class TypeMapped : NetfuserEvent
        {
            public readonly TypeMapping Mapping;

            public TypeMapped(IContext context, TypeMapping mapping)
                : base(context)
            {
                Mapping = mapping;
            }
        }


        /// <summary>
        /// This event is fired after all top-level and nested <see cref="TypeDef"/>s are created in the target assembly,
        /// but before type members, generics, attributes and other stuff is imported.
        /// Type mappings are fully populated and usable at this point, but target types are empty
        /// (i.e. contain no members, hence the name - "skeletons")
        /// </summary>
        public class TypeSkeletonsImported : NetfuserEvent
        {
            internal TypeSkeletonsImported(IContext context)
                : base(context)
            {
            }
        }

        #endregion

        #region Type members

        /// <summary>
        /// This event is fired right before type members are imported.
        /// Importing type members is the most CPU-intensive part of the process, and it may be done in parallel
        /// to speed things up.
        /// Observe to change max. number of parallel tasks to run
        /// </summary>
        public class WillImportMembers : NetfuserEvent
        {
            /// <summary>
            /// Number of parallel tasks to launch. By default equals to the number of logical processors
            /// as detected by the .NET runtime. Set to 0 to disable parallel processing.
            /// </summary>
            public int ParallelTasks;

            internal WillImportMembers(IContext context)
                : base(context)
            {
            }
        }

        /// <summary>
        /// Observe this event to inject members into the target type(s)
        /// This event is fired for every <see cref="TypeMapping"/> in the current <see cref="IContext"/>
        /// </summary>
        public class InjectMembers : NetfuserEvent
        {
            /// <summary>
            /// <see cref="TypeDef"/> in the target module is represented by this <see cref="TypeMapping"/>
            /// </summary>
            public readonly TypeMapping TypeMapping;

            /// <summary>
            /// Members to be injected.
            /// It's better to use <see cref="Add"/> method rather than adding to this set directly, it performs some
            /// sanity checks and also changes declaring type of the to-be-injected member, as required by <see cref="dnlib"/>
            /// </summary>
            public readonly ISet<IMemberDef> Injected;

            internal InjectMembers(IContext context, TypeMapping typeMapping)
                : base(context)
            {
                TypeMapping = typeMapping;
                Injected = new HashSet<IMemberDef>();
            }

            /// <summary>
            /// Checks if there's a constructor matching the specified signature
            /// in the source type or among the to-be-injected members   
            /// </summary>
            /// <param name="sig">constructor signature</param>
            /// <returns>true if the matching constructor is found, false otherwise</returns>
            public bool HasInstanceCtor(MethodSig sig)
            {
                var sc = new SigComparer();
                return TypeMapping.Source.FindInstanceConstructors()
                    .Concat(Injected.OfType<MethodDef>().Where(m => m.IsInstanceConstructor))
                    .Any(c => sc.Equals(c.MethodSig, sig));
            }

            /// <summary>
            /// Convenience method to add members to be injected in the target type.
            /// Do not use to inject nested types, observe <see cref="NetfuserEvent.InjectTypes"/> instead.
            /// </summary>
            /// <param name="member">member to add</param>
            /// <exception cref="Exception">if injected of nested type is attempted</exception>
            public void Add(IMemberDef member)
            {
                if (member.IsType)
                    throw ((IContextImpl) Context).Error($"use {nameof(InjectTypes)} to inject nested types");
                if (member.DeclaringType == null)
                    // this hack is to make it look like the member exists in the source type
                    switch (member)
                    {
                        case FieldDef fd:
                            fd.DeclaringType2 = TypeMapping.Source;
                            break;
                        case MethodDef md:
                            md.DeclaringType2 = TypeMapping.Source;
                            break;
                        case EventDef ed:
                            ed.DeclaringType2 = TypeMapping.Source;
                            break;
                        case PropertyDef pd:
                            pd.DeclaringType2 = TypeMapping.Source;
                            break;
                    }

                Injected.Add(member);
            }
        }

        /// <summary>
        /// Base class for notification events fired when type member has been created and is about
        /// to be added to the corresponding type of the target module
        /// </summary>
        /// <typeparam name="T">type of the member</typeparam>
        public abstract class CreateMember<T> : NetfuserEvent
            where T : IMemberDef
        {
            /// <summary>
            /// Original member, either from source module or injected by the <see cref="NetfuserEvent.InjectMembers"/> observer.
            /// Corresponds to the <see cref="Target"/>, that is created in the type of the target module. 
            /// </summary>
            public readonly T Source;

            /// <summary>
            /// <see cref="TypeDef"/> in the target module is represented by this <see cref="TypeMapping"/>
            /// The new member is being added to this <see cref="TypeDef"/>
            /// </summary>
            public readonly TypeMapping TypeMapping;

            /// <summary>
            /// Member to be added to the target <see cref="TypeDef"/>
            /// It is OK to change properties of this member, or replace it entirely at this point.
            /// </summary>
            public T Target;

            protected CreateMember(IContext context, T source, TypeMapping typeMapping)
                : base(context)
            {
                Source = source;
                TypeMapping = typeMapping;
            }
        }

        /// <summary>
        /// This event is fired after a method has been created and is about to be added
        /// to the corresponding type of the target module.
        /// The method is bodyless at this point, the body will be added later.
        /// Observe to change properties of the new method, or replace it with a custom one.
        /// </summary>
        public class CreateMethod : CreateMember<MethodDef>
        {
            internal CreateMethod(IContext context, MethodDef source, TypeMapping typeMapping)
                : base(context, source, typeMapping)
            {
            }
        }

        /// <summary>
        /// This event is fired after a field has been created and is about to be added
        /// to the corresponding type of the target module.
        /// Observe to change properties of the new field, or replace it with a custom one.
        /// </summary>
        public class CreateField : CreateMember<FieldDef>
        {
            internal CreateField(IContext context, FieldDef source, TypeMapping typeMapping)
                : base(context, source, typeMapping)
            {
            }
        }

        /// <summary>
        /// This event is fired after a property has been created and is about to be added
        /// to the corresponding type of the target module.
        /// Observe to change properties of the new property, or replace it with a custom one.
        /// </summary>
        public class CreateProperty : CreateMember<PropertyDef>
        {
            internal CreateProperty(IContext context, PropertyDef source, TypeMapping typeMapping)
                : base(context, source, typeMapping)
            {
            }
        }

        /// <summary>
        /// This event is fired after an event has been created and is about to be added
        /// to the corresponding type of the target module.
        /// Observe to change properties of the new event, or replace it with a custom one.
        /// </summary>
        public class CreateEvent : CreateMember<EventDef>
        {
            internal CreateEvent(IContext context, EventDef source, TypeMapping typeMapping)
                : base(context, source, typeMapping)
            {
            }
        }

        /// <summary>
        /// This event is fired after method parameter has been created and is about to be added
        /// to the corresponding method in the target module.
        /// Observe to change properties of the new parameter, or replace it with a custom one.
        /// </summary>
        public class CreateMethodParameter : NetfuserEvent
        {
            /// <summary>
            /// Original parameter.
            /// Corresponds to the <see cref="Target"/>, that is created in the target method 
            /// </summary>
            public readonly ParamDef Source;

            /// <summary>
            /// <see cref="TypeDef"/> in the target module is represented by this <see cref="TypeMapping"/>
            /// The new parameter is being added to the method in this <see cref="TypeDef"/>
            /// </summary>
            public readonly TypeMapping TypeMapping;

            /// <summary>
            /// The new parameter is being added to this method
            /// </summary>
            public readonly MethodDef TargetMethod;

            /// <summary>
            /// Parameter to be added to the target <see cref="MethodDef"/>
            /// It is OK to change properties of this parameter, or replace it entirely at this point.
            /// </summary>
            public ParamDef Target;

            internal CreateMethodParameter(IContext context, ParamDef source, MethodDef targetMethod,
                TypeMapping typeMapping)
                : base(context)
            {
                Source = source;
                TargetMethod = targetMethod;
                TypeMapping = typeMapping;
            }
        }

        /// <summary>
        /// This event is fired after generic parameter has been created and is about to be added
        /// to the corresponding method or type in the target module.
        /// Observe to change properties of the new generic parameter or replace it with a custom one.
        /// </summary>
        public class CreateGenericParameter : NetfuserEvent
        {
            /// <summary>
            /// Original generic parameter.
            /// Corresponds to the <see cref="Target"/>, that is created in the target method or type
            /// </summary>
            public readonly GenericParam Source;

            /// <summary>
            /// <see cref="TypeDef"/> in the target module is represented by this <see cref="TypeMapping"/>
            /// The new generic parameter is being added to the method in this <see cref="TypeDef"/>, or to this <see cref="TypeDef"/>
            /// </summary>
            public readonly TypeMapping TypeMapping;

            /// <summary>
            /// The new generic parameter is being added to this method or type
            /// </summary>
            public readonly ITypeOrMethodDef TargetTypeOrMethod;

            /// <summary>
            /// Generic parameter to be added to the target <see cref="MethodDef"/> or <see cref="TypeDef"/> 
            /// It is OK to change properties of this generic parameter, or replace it entirely at this point.
            /// </summary>
            public GenericParam Target;

            internal CreateGenericParameter(IContext context, GenericParam source,
                ITypeOrMethodDef targetTypeOrMethod, TypeMapping typeMapping)
                : base(context)
            {
                Source = source;
                TargetTypeOrMethod = targetTypeOrMethod;
                TypeMapping = typeMapping;
            }
        }

        #endregion

        #region Methods and method bodies

        /// <summary>
        /// This event is fired right before target method body is to be finalized.
        /// Handling this event and calling <see cref="GetEmitter"/> is the preferred way to make changes to the method body  
        /// </summary>
        public class CilBodyBuilding : NetfuserEvent
        {
            private readonly IReadOnlyDictionary<Instruction, Instruction> _instrMap;
            private IILEmitter _emitter;

            /// <summary>
            /// <see cref="TypeDef"/> in the target module is represented by this <see cref="TypeMapping"/>
            /// The new method body is being added to the method in this <see cref="TypeDef"/>
            /// </summary>
            public readonly TypeMapping TypeMapping;

            /// <summary>
            /// Original method from the source module.
            /// Corresponds to the <see cref="Target"/>
            /// </summary>
            public readonly MethodDef Source;

            /// <summary>
            /// Body of this method is being built 
            /// </summary>
            public readonly MethodDef Target;

            /// <summary>
            /// This instance of <see cref="Importer"/> should be used when importing metadata members
            /// to be referenced from the body of the target method 
            /// </summary>
            public readonly Importer Importer;

            /// <summary>
            /// List of locals in the target method.
            /// This should not be used to add new locals, use <see cref="IILEmitter.TempLocals"/> for that
            /// </summary>
            public readonly List<Local> Locals;

            /// <summary>
            /// Exception handlers in the target method
            /// May be used to add new handlers or change existing
            /// TODO: add convenience methods in <see cref="IILEmitter"/> to manipulate exception handlers
            /// </summary>
            public readonly List<ExceptionHandler> ExceptionHandlers;

            /// <summary>
            /// List of IL instructions in the target method.
            /// This should not be modified directly, call <see cref="GetEmitter"/> and use <see cref="IILEmitter"/>'s convenience methods instead
            /// </summary>
            public readonly CilFragment Fragment;

            internal CilBodyBuilding(IContext context, TypeMapping typeMapping, MethodDef source, MethodDef target,
                Importer importer, IReadOnlyDictionary<Instruction, Instruction> instructionMap)
                : base(context)
            {
                TypeMapping = typeMapping;
                Source = source;
                Target = target;
                Importer = importer;
                Locals = new List<Local>();
                ExceptionHandlers = new List<ExceptionHandler>();
                Fragment = new CilFragment();
                _instrMap = instructionMap;
            }

            /// <summary>
            /// This runs block parser on the target method body and returns parsed blocks.
            /// See <see cref="DnextFactory.ParseBlocks"/> for details
            /// </summary>
            /// <returns>parsed blocks</returns>
            public Block.Root ParseBlocks() => DnextFactory.ParseBlocks(Fragment.Instructions, ExceptionHandlers);

            /// <summary>
            /// Get the instance of <see cref="IILEmitter"/> for the body of the target method.
            /// <see cref="IILEmitter"/> should have all you need to inject code into the target method's body
            /// </summary>
            /// <param name="create">the default is <see langword="true"/>, meaning that new emitter will be created if there's none yet,
            /// set to <see langword="false"/> if you don't want to create new one if none exists, in this case <see langword="null"/> may be returned</param>
            /// <returns>instance of <see cref="IILEmitter"/></returns>
            public IILEmitter GetEmitter(bool create = true)
            {
                if (_emitter == null && create)
                    lock (this)
                        if (_emitter == null)
                            _emitter = DnextFactory.NewILEmitter(Context.TargetModule, Importer, Fragment);
                return _emitter;
            }

            /// <summary>
            /// Given the instruction from the source method's body, get the corresponding instruction in the target method's body
            /// </summary>
            /// <param name="si">instruction in the source method's body</param>
            /// <returns>instruction in the target method's body</returns>
            public Instruction Map(Instruction si)
            {
                if (si == null) return null;
                return _instrMap.TryGetValue(si, out var result) ? result : si;
            }
        }

        /// <summary>
        /// This event is fired when the target method is fully imported and is valid in the target module.
        /// This is the last chance to make any and all changes to the method and/or it's body.
        /// Control flow obfuscator uses this event to mangle method body after all other changes and injections have taken place.
        /// </summary>
        public class MethodImported : NetfuserEvent
        {
            /// <summary>
            /// Source method corresponding to the <see cref="Target"/>
            /// </summary>
            public readonly MethodDef Source;

            /// Target method corresponding to the <see cref="Source"/>
            public readonly MethodDef Target;

            /// <summary>
            /// This instance of <see cref="Importer"/> should be used when importing metadata members
            /// to be referenced from the body of the target method 
            /// </summary>
            public readonly Importer Importer;

            internal MethodImported(IContext context, MethodDef source, MethodDef target, Importer importer)
                : base(context)
            {
                Source = source;
                Target = target;
                Importer = importer;
            }
        }

        #endregion

        #region Debug info

        /// <summary>
        /// This event is fired when debug info item from the source module has been cloned and is about to be added
        /// to the target module. Observe to make changes or replace this debug info item. 
        /// </summary>
        public class CloneCustomDebugInfo : NetfuserEvent
        {
            /// <summary>
            /// Debug info item from the source module, corresponding to the <see cref="Target"/>.
            /// </summary>
            public readonly PdbCustomDebugInfo Source;

            /// <summary>
            /// Debug info item to be added to the target module, corresponding to the <see cref="Source"/>.
            /// Feel free to change or replace this 
            /// </summary>
            public PdbCustomDebugInfo Target;

            internal CloneCustomDebugInfo(IContext context, PdbCustomDebugInfo source)
                : base(context)
            {
                Source = source;
            }
        }

        #endregion

        #region Finalization

        /// <summary>
        /// This event is fired when all types from all source modules have been added to the target module,
        /// along with all members, fully populated and valid.
        /// </summary>
        public class TypesImported : NetfuserEvent
        {
            internal TypesImported(IContext context)
                : base(context)
            {
            }
        }

        /// <summary>
        /// This event is fired when all source modules have been fully merged into the target.
        /// At this point the target module is ready to be saved to disk
        /// </summary>
        public class Complete : NetfuserEvent
        {
            internal Complete(IContext context)
                : base(context)
            {
            }
        }

        #endregion
    }
}