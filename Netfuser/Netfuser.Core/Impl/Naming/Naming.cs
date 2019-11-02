using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Base.Collections.Props;
using dnlib.DotNet;
using Netfuser.Core.Naming;
using Netfuser.Dnext;

namespace Netfuser.Core.Impl.Naming
{
    class Naming : AbstractPlugin, INaming
    {
        static readonly PropKey<TypeMapping, MetaType> PropPreservedType = new PropKey<TypeMapping, MetaType>();
        static readonly PropKey<ResourceMapping, bool> PropPreservedResource = new PropKey<ResourceMapping, bool>();

        readonly IDictionary<string, MetaType> _preserveNamesIfImplements = new Dictionary<string, MetaType>
        {
            ["System.Runtime.InteropServices.ICustomMarshaler"] = MetaType.Member
        };

        readonly IDictionary<string, MetaType> _preserveNamesIfAnnotatedBy = new Dictionary<string, MetaType>
        {
            ["System.CodeDom.Compiler.GeneratedCodeAttribute"] = MetaType.Member
        };

        /// <summary>
        /// nodes correspond to target types, not to type mappings, as there can be multiple mappings for a single target
        /// </summary>
        private readonly Dictionary<TypeDef, INsNode> _nodes;

        public Naming(IContextImpl context)
            : base(context)
        {
            _nodes = new Dictionary<TypeDef, INsNode>();
            context.OfType<NetfuserEvent.TypeMapped>().Subscribe(ev =>
            {
                MetaType preserve = 0;
                var source = ev.Mapping.Source;
                if (source.IsGlobalModuleType) preserve |= MetaType.NamespaceAndType;
                if (source.IsComImport() || source.IsDelegate) preserve |= MetaType.Member;
                if (source.IsRuntimeSpecialName || source.IsSpecialName)
                    preserve |= MetaType.All;

                if (_preserveNamesIfImplements.Count > 0)
                    preserve |= source.Interfaces
                        .Select(i =>
                            _preserveNamesIfImplements.TryGetValue(i.Interface.FullName, out var v) ? v : 0)
                        .Aggregate(MetaType.None, (a, v) => a | v);
                if (_preserveNamesIfAnnotatedBy.Count > 0)
                    preserve |= source.CustomAttributes
                        .Select(ca =>
                            _preserveNamesIfAnnotatedBy.TryGetValue(ca.Constructor.DeclaringType.FullName,
                                out var v)
                                ? v
                                : 0).Aggregate(MetaType.None, (a, v) => a | v);
                Preserve(ev.Mapping, preserve);
            });
        }

        public INsNode GetOrAddNode(TypeMapping tm, Func<TypeMapping, INsNode> creator = null)
        {
            if (!_nodes.TryGetValue(tm.Target, out var node) && creator != null)
            {
                node = creator(tm);
                if (node != null)
                    _nodes.Add(tm.Target, node);
            }

            return node;
        }

        public bool FindNewName(IMemberRef source, out TypeMapping tm, out string newName)
        {
            newName = null;
            if (!Context.MappedTypes.TryGetValue(source.DeclaringType.CreateKey(), out tm))
                return false;
            var nn = GetOrAddNode(tm);
            if (nn == null || !nn.MembersByOldName.TryGetValue(source.Name, out var mn)) return false;
            newName = mn.NewName;
            return true;
        }

        public MetaType Preserve(TypeMapping tm, MetaType type) =>
            type == 0 ? PropPreservedType[tm] : PropPreservedType[tm] |= type;

        public MetaType Preserved(TypeMapping tm) => PropPreservedType[tm];

        public bool IsPreserved(TypeMapping tm, MetaType type) => (PropPreservedType[tm] & type) != 0;

        public bool IsPreserved(TypeMapping tm, IMemberDef member) =>
            _nodes.TryGetValue(tm.Target, out var node) &&
            node.MembersByOldName.TryGetValue(member.Name, out var om) &&
            node.MembersByNewName.TryGetValue(member.Name, out var nm) && ReferenceEquals(nm, om);

        public void Preserve(ResourceMapping rm)
        {
            PropPreservedResource[rm] = true;
        }

        public bool IsPreserved(ResourceMapping rm) => PropPreservedResource[rm];
    }
}