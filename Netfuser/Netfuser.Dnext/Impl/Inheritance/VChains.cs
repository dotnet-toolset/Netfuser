using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Base.Collections;
using Base.Collections.Impl;
using dnlib.DotNet;
using Netfuser.Dnext.Hierarchy;

namespace Netfuser.Dnext.Impl.Inheritance
{
    class VChains : IVChains
    {
        private readonly Dictionary<ITypeKey, Dictionary<string, VChain>> _chains;

        private readonly Func<MethodDef, bool> DontRename;

        public VChains(IVTables vtables, IEnumerable<TypeDef> types, Func<MethodDef, bool> dontRename)
        {
            _chains = new Dictionary<ITypeKey, Dictionary<string, VChain>>();
            DontRename = dontRename;
            foreach (var type in types)
            {
                var vtbl = vtables.GetVTable(type);
                foreach (var slot in vtbl.Slots)
                    AddNameToChain(slot);
                foreach (var slot in vtbl.InterfaceSlots.Values.SelectMany(s => s))
                    AddNameToChain(slot);
            }
#if DEBUG
            foreach (var chains in _chains.Values)
            foreach (var ch in chains.Values)
            {
                var f = ch.Types.FirstOrDefault(t => _chains[t][ch.Name] != ch);
                Debug.Assert(f == null);
            }
#endif
        }

        private void CombineChains(VChain source, VChain target)
        {
            if (source.Types.Count > target.Types.Count)
            {
                var t = source;
                source = target;
                target = t;
            }

            var name = source.Name;
            Debug.Assert(name == target.Name);
            var set = new HashSet<ITypeKey>(source.Types);
            set.ExceptWith(target.Types);
            target.Types.UnionWith(set);
            target.DontRename |= source.DontRename;
            foreach (var t in set)
                if (_chains.TryGetValue(t, out var names) && names.TryGetValue(name, out var ch))
                {
                    if (ch != target) names[name] = target;
                }
                else
                    Debug.Assert(false);
        }

        private void AddNameToChain(IVTableSlot slot)
        {
            var method = slot.MethodDef;
            var type = method.DeclaringType;
            var name = method.Name;
            var otherType = slot.Parent?.MethodDef.DeclaringType;
            Dictionary<string, VChain> chains, otherChains = null;
            VChain chain, otherChain = null;
            ITypeKey key = type.CreateKey(), otherKey = null;
            if (!_chains.TryGetValue(key, out chains))
                _chains.Add(key, chains = new Dictionary<string, VChain>());
            if (otherType != null)
            {
                otherKey = otherType.CreateKey();
                if (!_chains.TryGetValue(otherKey, out otherChains))
                    _chains.Add(otherKey, otherChains = new Dictionary<string, VChain>());
            }

            chains.TryGetValue(name, out chain);
            otherChains?.TryGetValue(name, out otherChain);
            if (otherChain == null)
            {
                if (chain == null)
                    chains.Add(name, chain = new VChain(name));
                otherChain = chain;
                otherChains?.Add(name, otherChain);
            }
            else
            {
                if (chain == null)
                    chains.Add(name, chain = otherChain);
                else if (chain != otherChain)
                    CombineChains(chain, otherChain);
            }

            chain.Types.Add(key);
            if (otherType != null)
                chain.Types.Add(otherKey);
            if (!chain.DontRename && DontRename != null)
                chain.DontRename = DontRename(method) || (slot.Parent != null && DontRename(slot.Parent?.MethodDef));
        }

        public VChain Get(IMethod method)
        {
            return _chains.TryGetValue(method.DeclaringType.CreateKey(), out var chains)
                ? chains.TryGetValue(method.Name, out var chain) ? chain : null
                : null;
        }

        public IReadOnlySet<VChain> All()
        {
            return new ReadOnlySet<VChain>(_chains.Values.SelectMany(d => d.Values));
        }
    }
}