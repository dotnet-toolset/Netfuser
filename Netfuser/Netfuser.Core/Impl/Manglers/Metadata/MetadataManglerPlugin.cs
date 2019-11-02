using System;
using System.Collections.Generic;
using System.Linq;
using Base.Rng;
using dnlib.DotNet;
using Netfuser.Core.Hierarchy;
using Netfuser.Core.Manglers.Metadata;
using Netfuser.Core.Naming;
using Netfuser.Core.Rng;
using Netfuser.Dnext;
using Netfuser.Dnext.Hierarchy;

namespace Netfuser.Core.Impl.Manglers.Metadata
{
    class MetadataManglerPlugin : AbstractPlugin.Subscribed, IMetadataMangler
    {
        private readonly MetadataManglerOptions _options;
        private readonly MetadataElement.Root _root;
        private readonly IRng _rng;
        private readonly INaming _ns;
        private IVChains _nameChains;

        public MetadataManglerOptions Options => _options;
        public IRng Rng => _rng;

        public MetadataManglerPlugin(IContextImpl context, MetadataManglerOptions options)
            : base(context)
        {
            _options = options;
            _root = new MetadataElement.Root();
            _rng = context.Plugin<IRngPlugin>().Get(NetfuserFactory.MetadataManglerName);
            _ns = context.Plugin<INaming>();
        }


        INsNode GetNode(INsNode parent, TypeMapping tm, string oldName, bool preserve = false)
        {
            return parent.GetOrAddNode(oldName, () =>
            {
                string newName;
                if (preserve) newName = oldName;
                else
                    newName = NameGenerator(parent, tm?.Source, oldName,
                        n => parent.NewName == n || parent.MembersByNewName.ContainsKey(n));

                return newName;
            });
        }


        string GetMangledName(TypeMapping mapping, IMemberDef source)
        {
            var parent = GetNode(mapping);
            return parent.GetOrAddMember(source,
                () => NameGenerator(parent, source, source.Name,
                    n => parent.NewName == n || parent.MembersByNewName.ContainsKey(n))).NewName;
        }

        INsNode GetNode(TypeMapping mapping)
        {
            return _ns.GetOrAddNode(mapping, tm =>
            {
                var source = tm.Source;
                var uniqueInScopeOldName = tm.UniqueName;
                var toMangle = _options.Type & ~_ns.Preserved(tm) & MetaType.NamespaceAndType;
                var preserveType = (toMangle & MetaType.Type) == 0;
                var preserveNs = (toMangle & MetaType.Namespace) == 0;
                INsNode scope;
                if (source.DeclaringType != null)
                {
                    var parent = GetNode(Context.MappedTypes[source.DeclaringType.CreateKey()]);
                    scope = GetNode(parent, tm, uniqueInScopeOldName, preserveType);
                }
                else
                {
                    INsNode node = _root;
                    if (preserveNs)
                        node = GetNode(node, null, tm.Source.Namespace, true);
                    else
                    {
                        var nsfolding = _options.NamespaceMangling;
                        switch (nsfolding)
                        {
                            // root -> types
                            case NamespaceMangling.Empty:
                                if (preserveType)
                                    if (node.MembersByNewName.ContainsKey(uniqueInScopeOldName))
                                        goto case NamespaceMangling.Fold;
                                    else
                                        break;
                                if (tm.Source.Namespace.Length > 0)
                                    // this will serve as a key only to prevent duplicates, it will be replaced by mangled name
                                    uniqueInScopeOldName = tm.Source.Namespace + "." + uniqueInScopeOldName;
                                break;
                            // root -> namespaces -> types
                            case NamespaceMangling.Fold:
                                node = GetNode(node, null, tm.Source.Namespace);
                                break;
                            // root -> namespaces[0] -> namespaces[1] -> namespaces[...n] -> types
                            case NamespaceMangling.Parts:
                                foreach (var part in tm.Source.Namespace.ToString().Split('.'))
                                    node = GetNode(node, null, part);
                                break;
                            case NamespaceMangling.Distribute:
                                node = (INsNode)node.Members.RandomElementOrDefault(_rng);
                                goto case NamespaceMangling.Empty;
                        }

                        if (tm.Target.Namespace != node.FullNewName)
                            tm.Target.Namespace = node.FullNewName;
                    }

                    scope = GetNode(node, tm, uniqueInScopeOldName, preserveType);
                }

                if (tm.Target.Name != scope.NewName)
                    tm.Target.Name = scope.NewName;
                return scope;
            });
        }


        private bool DontRename(TypeMapping tm, MethodDef method) =>
            method.IsRuntimeSpecialName || _ns.IsPreserved(tm, MetaType.Method);

        private bool DontRename(TypeMapping tm, FieldDef field) =>
            field.IsRuntimeSpecialName || _ns.IsPreserved(tm, MetaType.Field);

        private bool DontRename(TypeMapping tm, PropertyDef field) =>
            field.IsRuntimeSpecialName || _ns.IsPreserved(tm, MetaType.Property);

        private bool DontRename(TypeMapping tm, EventDef field) =>
            field.IsRuntimeSpecialName || _ns.IsPreserved(tm, MetaType.Event);

        /// <summary>
        /// Name scopes must be populated with the preserved names before we start mangling, so that if we accidentally come up with mangled name equal to one of the
        /// preserved within that scope, we can easily generate new one
        /// </summary>
        /// <param name="tm"></param>
        void Prepopulate(TypeMapping tm)
        {
            var scope = GetNode(tm);
            // take fields first, additionally checking for duplicates
            if ((_options.Type & MetaType.Field) != 0)
                foreach (var m in tm.Source.Fields)
                    if (DontRename(tm, m))
                        // fields with the same name are hopefully not going to happen
                        // not using GetOrAddPreservedMember here to catch [im]possible duplicates
                        scope.GetOrAddMember(m, () => m.Name);
            if ((_options.Type & MetaType.Method) != 0)
                foreach (var m in tm.Source.Methods)
                {
                    var chain = _nameChains.Get(m);
                    if ((chain != null && chain.DontRename) || DontRename(tm, m))
                        // multiple methods with the same name are OK
                        scope.GetOrAddPreservedMember(m);
                }

            if ((_options.Type & MetaType.Property) != 0)
                foreach (var m in tm.Source.Properties)
                    if (DontRename(tm, m))
                        // properties with the same name may exist, for example indexers with different argument types
                        scope.GetOrAddPreservedMember(m);
            if ((_options.Type & MetaType.Event) != 0)
                foreach (var m in tm.Source.Events)
                    if (DontRename(tm, m))
                        // events are usually backed by fields with the same name
                        scope.GetOrAddPreservedMember(m);
        }

        private bool MatchesTypeOrMemberName(IEnumerable<ITypeKey> types, string name) => types
            .Select(t => GetNode(Context.MappedTypes[t])).Any(node =>
                node.NewName == name || node.MembersByNewName.ContainsKey(name));

        protected override void Handle(NetfuserEvent ev)
        {
            switch (ev)
            {
                case NetfuserEvent.Initialize _:
                    if (Options.Generator == null)
                        Options.Generator = Context.DebugMode ? (NameGenerator)new NameGenerator.Debug() : new NameGenerator.Hash();
                    break;
                case NetfuserEvent.TypeSkeletonsImported e:
                    if ((_options.Type & MetaType.Method) != 0)
                    {
                        _nameChains = DnextFactory.BuildVNameChains(Context.Plugin<IVTablePlugin>().VTables,
                            Context.MappedTypes.Values.Select(m => m.Source), m =>
                                !Context.MappedTypes.TryGetValue(m.DeclaringType.CreateKey(), out var tm) ||
                                DontRename(tm, m));
                    }

                    // build scopes for types with preserved names first
                    foreach (var tm in Context.MappedTypes.Values.Where(m =>
                        _ns.IsPreserved(m, MetaType.NamespaceAndType)))
                        Prepopulate(tm);
                    if (_options.NamespaceMangling == NamespaceMangling.Distribute)
                    {
                        var count = (int)Math.Sqrt(Context.MappedTypes.Count);
                        for (var i = 0; i < count; i++)
                            GetNode(_root, null, i.ToString());
                    }

                    // build scopes for the remaining types and their preserved member names
                    foreach (var tm in Context.MappedTypes.Values.Where(m =>
                        !_ns.IsPreserved(m, MetaType.NamespaceAndType)))
                        Prepopulate(tm);

                    if ((_options.Type & MetaType.Method) != 0)
                    {
                        // add virtual renamable methods before regular methods
                        // we use fake scope here
                        var scope = new MetadataElement(null, string.Empty);

                        foreach (var c in _nameChains.All())
                            if (!c.DontRename)
                                c.NewName = NameGenerator(scope, null, c.Name,
                                    n => MatchesTypeOrMemberName(c.Types, n));
                    }

                    break;
                case NetfuserEvent.CreateMethod cm when (_options.Type & MetaType.Method) != 0:
                    cm.Target.Name = GetMangledName(cm.TypeMapping, cm.Source);
                    break;
                case NetfuserEvent.CreateField cm when (_options.Type & MetaType.Field) != 0:
                    cm.Target.Name = GetMangledName(cm.TypeMapping, cm.Source);
                    break;
                case NetfuserEvent.CreateProperty cm when (_options.Type & MetaType.Property) != 0:
                    cm.Target.Name = GetMangledName(cm.TypeMapping, cm.Source);
                    break;
                case NetfuserEvent.CreateEvent cm when (_options.Type & MetaType.Event) != 0:
                    cm.Target.Name = GetMangledName(cm.TypeMapping, cm.Source);
                    break;
                case NetfuserEvent.CreateMethodParameter cm when (_options.Type & MetaType.Method) != 0:
                    // fast and easy way to check if we should rename parameter names:
                    // if renaming didn't happen for this method, there's little sense to rename its parameters.
                    // someone may even rely on parameter names [theoretically], as they are available via reflection
                    // However, we allow one exception - RTSpecialNames (.ctors), as they are never renamed  
                    var method = cm.Source.DeclaringMethod;
                    if (!method.IsRuntimeSpecialName && GetMangledName(cm.TypeMapping, method) == method.Name)
                        break;

                    cm.Target.Name = _options.Generator.Generate(this, cm.Source);
                    break;
                case NetfuserEvent.CreateGenericParameter cm:
                    method = cm.Source.DeclaringMethod;
                    if (method != null)
                    {
                        if (!method.IsRuntimeSpecialName && GetMangledName(cm.TypeMapping, method) == method.Name)
                            break;
                    }
                    else if (cm.Source.DeclaringType != null)
                    {
                        if (GetNode(cm.TypeMapping).NewName == cm.Source.DeclaringType.Name)
                            break;
                    }
                    else throw new NotSupportedException();

                    cm.Target.Name = _options.Generator.Generate(this, cm.Source);
                    break;

                case NetfuserEvent.ResourcesDeclared rde:
                    var rs = new MetadataElement(null, string.Empty);
                    var newNames = new HashSet<string>(Context.MappedResources.Values.Select(m => (string)m.Target.Name));
                    foreach (var rm in Context.MappedResources.Values.Where(m => !_ns.IsPreserved(m)))
                    {
                        var oldName = rm.Target.Name;
                        var newName = NameGenerator(rs, null, rm.Source.Name, n => newNames.Contains(n));
                        if (newName != oldName)
                        {
                            newNames.Remove(oldName);
                            newNames.Add(newName);
                            rm.Target.Name = newName;
                        }
                    }
                    break;
            }
        }


        string NameGenerator(INsMember scope, IMemberDef member, string old, Func<string, bool> isBadName)
        {
            var options = _options.Generator.Clone();
            if (member != null && options is NameGenerator.Encoded enc && member.IsTypeDef &&
                ((TypeDef)member).Interfaces.Any(i =>
                   i.Interface.FullName == typeof(System.Runtime.InteropServices.ICustomMarshaler).FullName))
                enc.Dictionary = ManglerCharsets.Latin;
            var ev = Context.Fire(new NameManglerEvent.GenerateName(Context, scope, member, options));
            options = ev.Options;

            string postfix = null;
            if (options.PreserveGenericCount && member != null && (member.IsType || member.IsMethod))
            {
                var i = old.IndexOf('`');
                if (i != -1)
                {
                    postfix = old.Substring(i + 1);
                    old = old.Substring(0, i);
                }
            }

            var iteration = 0;
            string name;
            while (true)
            {
                if (iteration > options.MaxIterations)
                    throw Context.Error($"too many attempts to generate unique name for {member?.FullName ?? old}");

                name = options.Generate(this, scope, member, old, iteration++);
                if (isBadName != null && isBadName(name)) continue;
                if (ev.AvoidNames != null && ev.AvoidNames.Contains(name)) continue;
                break;
            }

            if (postfix != null) name += '`' + postfix;
            return name;
        }
    }
}