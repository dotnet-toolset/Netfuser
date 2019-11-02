using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using dnlib.DotNet;
using Netfuser.Dnext.Hierarchy;

namespace Netfuser.Dnext.Impl.Inheritance
{
    class VTableSignature
    {
        public MethodSig MethodSig { get; }
        public string Name { get; }

        internal VTableSignature(MethodSig sig, string name)
        {
            MethodSig = sig;
            Name = name;
        }

        public static VTableSignature FromMethod(IMethod method)
        {
            var sig = method.MethodSig;
            var declType = method.DeclaringType.ToTypeSig();
            if (declType is GenericInstSig instSig)
                sig = GenericArgumentResolver.Resolve(sig, instSig.GenericArguments);
            return new VTableSignature(sig, method.Name);
        }

        public override bool Equals(object obj)
        {
            return obj is VTableSignature other && new SigComparer().Equals(MethodSig, other.MethodSig) &&
                   Name.Equals(other.Name, StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            var hash = 17;
            hash = hash * 7 + new SigComparer().GetHashCode(MethodSig);
            return hash * 7 + Name.GetHashCode();
        }

        public static bool operator ==(VTableSignature a, VTableSignature b)
        {
            if (ReferenceEquals(a, b))
                return true;
            return a?.Equals(b) ?? false;
        }

        public static bool operator !=(VTableSignature a, VTableSignature b)
        {
            return !(a == b);
        }

        public override string ToString()
        {
            return FullNameFactory.MethodFullName("", Name, MethodSig);
        }
    }

    class VTableSlot : IVTableSlot
    {
        // This is the type in which this slot is defined.
        public TypeSig DeclaringType { get; }

        // This is the signature of this slot.
        public VTableSignature Signature { get; }

        // This is the method that is currently in the slot.
        public TypeSig MethodDefDeclType { get; }
        public MethodDef MethodDef { get; }

        // This is the 'parent slot' that this slot overrides.
        public IVTableSlot Parent { get; }

        internal VTableSlot(MethodDef def, TypeSig decl, VTableSignature signature)
            : this(def.DeclaringType.ToTypeSig(), def, decl, signature, null)
        {
        }

        internal VTableSlot(TypeSig defDeclType, MethodDef def, TypeSig decl, VTableSignature signature,
            VTableSlot overrides)
        {
            MethodDefDeclType = defDeclType;
            MethodDef = def;
            DeclaringType = decl;
            Signature = signature;
            Parent = overrides;
        }

        public override string ToString()
        {
            return MethodDef.ToString();
        }
    }

    class VTable : IVTable
    {
        public TypeSig Type { get; }
        public IReadOnlyList<IVTableSlot> Slots { get; }
        public IReadOnlyDictionary<TypeSig, IReadOnlyList<IVTableSlot>> InterfaceSlots { get; }

        internal VTable(TypeSig type, IReadOnlyList<IVTableSlot> slots,
            IReadOnlyDictionary<TypeSig, IReadOnlyList<IVTableSlot>> ifaceSlots)
        {
            Type = type;
            Slots = slots;
            InterfaceSlots = ifaceSlots;
        }

        internal class Builder
        {
            class TypeSigComparer : IEqualityComparer<TypeSig>
            {
                public static readonly TypeSigComparer Instance = new TypeSigComparer();
                public bool Equals(TypeSig x, TypeSig y) => new SigComparer().Equals(x, y);
                public int GetHashCode(TypeSig obj) => new SigComparer().GetHashCode(obj);
            }

            private readonly TypeDef _typeDef;

            private readonly VTables _storage;

            // All virtual method slots, excluding interfaces
            private readonly List<VTableSlot> _allSlots;

            // All visible virtual method slots (i.e. excluded those being shadowed)
            private readonly Dictionary<VTableSignature, VTableSlot> _slotsMap;

            private readonly Dictionary<TypeSig, Dictionary<VTableSignature, VTableSlot>> _interfaceSlots;

            public Builder(TypeDef typeDef, VTables storage)
            {
                _typeDef = typeDef;
                _storage = storage;
                _allSlots = new List<VTableSlot>();
                _slotsMap = new Dictionary<VTableSignature, VTableSlot>();
                _interfaceSlots =
                    new Dictionary<TypeSig, Dictionary<VTableSignature, VTableSlot>>(TypeSigComparer.Instance);
            }

            static VTableSlot OverridenBy(VTableSlot vTableSlot, MethodDef method) => new VTableSlot(
                method.DeclaringType.ToTypeSig(), method, vTableSlot.DeclaringType, vTableSlot.Signature, vTableSlot);

            private void Inherits(VTable baseVTbl)
            {
                foreach (VTableSlot slot in baseVTbl.Slots)
                {
                    _allSlots.Add(slot);
                    // It's possible to have same signature in multiple slots,
                    // when a derived type shadow the base type using newslot.
                    // In this case, use the derived type's slot in SlotsMap.

                    // The derived type's slots are always at a later position 
                    // than the base type, so it would naturally 'override'
                    // their position in SlotsMap.
                    _slotsMap[slot.Signature] = slot;
                }

                // This is the step 1 of 12.2 algorithm -- copy the base interface implementation.
                foreach (var iface in baseVTbl.InterfaceSlots)
                {
                    Debug.Assert(!_interfaceSlots.ContainsKey(iface.Key));
                    _interfaceSlots.Add(iface.Key,
                        iface.Value.ToDictionary(slot => ((VTableSlot) slot).Signature, slot => (VTableSlot) slot));
                }
            }

            private void Implements(Dictionary<VTableSignature, MethodDef> virtualMethods, VTable ifaceVTbl,
                TypeSig iface)
            {
                // This is the step 2 of 12.2 algorithm -- use virtual newslot methods for explicit implementation.

                VTableSlot ImplLookup(IVTableSlot slot)
                {
                    if (virtualMethods.TryGetValue(((VTableSlot) slot).Signature, out var impl) && impl.IsNewSlot &&
                        !impl.DeclaringType.IsInterface)
                    {
                        // Interface methods cannot implements base interface methods.
                        // The Overrides of interface slots should directly points to the root interface slot
                        var targetSlot = slot;
                        while (targetSlot.Parent != null && !targetSlot.MethodDef.DeclaringType.IsInterface)
                            targetSlot = (VTableSlot) targetSlot.Parent;
                        Debug.Assert(targetSlot.MethodDef.DeclaringType.IsInterface);
                        return OverridenBy((VTableSlot) targetSlot, impl);
                    }

                    return (VTableSlot) slot;
                }

                if (_interfaceSlots.ContainsKey(iface))
                    _interfaceSlots[iface] =
                        _interfaceSlots[iface].Values.ToDictionary(slot => slot.Signature, ImplLookup);
                else
                    _interfaceSlots.Add(iface,
                        ifaceVTbl.Slots.ToDictionary(slot => ((VTableSlot) slot).Signature, ImplLookup));

                foreach (var baseIface in ifaceVTbl.InterfaceSlots)
                {
                    if (_interfaceSlots.ContainsKey(baseIface.Key))
                        _interfaceSlots[baseIface.Key] = _interfaceSlots[baseIface.Key].Values.ToDictionary(
                            slot => slot.Signature, ImplLookup);
                    else
                        _interfaceSlots.Add(baseIface.Key, baseIface.Value.ToDictionary(
                            slot => ((VTableSlot) slot).Signature, ImplLookup));
                }
            }

            public VTable Build()
            {
                // Inherits base type's slots
                var baseVTbl = (VTable) _storage.GetVTable(_typeDef.GetBaseTypeThrow());
                if (baseVTbl != null) Inherits(baseVTbl);

                var virtualMethods = _typeDef.Methods
                    .Where(method => method.IsVirtual)
                    .ToDictionary(
                        VTableSignature.FromMethod,
                        method => method
                    );
                if (_typeDef.FullName.Contains("SubjectBase"))
                {
                }

                // Explicit interface implementation
                foreach (var iface in _typeDef.Interfaces)
                {
                    var ifaceVTbl = (VTable) _storage.GetVTable(iface.Interface);
                    if (ifaceVTbl != null)
                        Implements(virtualMethods, ifaceVTbl, iface.Interface.ToTypeSig());
                }

                // Normal interface implementation
                if (!_typeDef.IsInterface)
                {
                    // Interface methods cannot implements base interface methods.
                    foreach (var iface in _interfaceSlots.Values)
                    {
                        foreach (var entry in iface.ToList())
                        {
                            if (!entry.Value.MethodDef.DeclaringType.IsInterface)
                                continue;
                            // This is the step 1 of 12.2 algorithm -- find implementation for still empty slots.
                            // Note that it seems we should include newslot methods as well, despite what the standard said.
                            if (virtualMethods.TryGetValue(entry.Key, out var impl))
                                iface[entry.Key] = OverridenBy(entry.Value, impl);
                            else if (_slotsMap.TryGetValue(entry.Key, out var implSlot))
                                iface[entry.Key] = OverridenBy(entry.Value, implSlot.MethodDef);
                        }
                    }
                }

                // Normal overrides
                foreach (var method in virtualMethods)
                {
                    VTableSlot slot;
                    if (method.Value.IsNewSlot)
                        slot = new VTableSlot(method.Value, _typeDef.ToTypeSig(), method.Key);
                    else if (_slotsMap.TryGetValue(method.Key, out slot))
                    {
                        Debug.Assert(!slot.MethodDef.IsFinal);
                        slot = OverridenBy(slot, method.Value);
                    }
                    else
                        slot = new VTableSlot(method.Value, _typeDef.ToTypeSig(), method.Key);

                    _slotsMap[method.Key] = slot;
                    _allSlots.Add(slot);
                }

                // MethodImpls
                foreach (var method in virtualMethods)
                foreach (var impl in method.Value.Overrides)
                {
                    Debug.Assert(impl.MethodBody == method.Value);

                    var targetMethod = impl.MethodDeclaration.ResolveMethodDefThrow();
                    if (targetMethod.DeclaringType.IsInterface)
                    {
                        var iface = impl.MethodDeclaration.DeclaringType.ToTypeSig();
                        var ifaceVTbl = _interfaceSlots[iface];

                        var signature = VTableSignature.FromMethod(impl.MethodDeclaration);
                        var targetSlot = ifaceVTbl[signature];

                        // The Overrides of interface slots should directly points to the root interface slot
                        while (targetSlot.Parent != null)
                            targetSlot = (VTableSlot) targetSlot.Parent;
                        Debug.Assert(targetSlot.MethodDef.DeclaringType.IsInterface);
                        ifaceVTbl[targetSlot.Signature] = OverridenBy(targetSlot, method.Value);
                    }
                    else
                    {
                        var targetSlot = _allSlots.Single(slot => slot.MethodDef == targetMethod);
                        targetSlot = _slotsMap[targetSlot.Signature]; // Use the most derived slot
                        // Maybe implemented by above processes --- this process should take priority
                        while (targetSlot.MethodDef.DeclaringType == _typeDef)
                            targetSlot = (VTableSlot) targetSlot.Parent;
                        _slotsMap[targetSlot.Signature] = OverridenBy(targetSlot, method.Value);
                    }
                }

                var ret = new VTable(_typeDef.ToTypeSig(), _allSlots, _interfaceSlots.ToDictionary(
                    kvp => kvp.Key, kvp => (IReadOnlyList<IVTableSlot>) kvp.Value.Values.ToList()));
                return ret;
            }
        }

        public IEnumerable<IVTableSlot> FindSlots(IMethod method)
        {
            return Slots
                .Concat(InterfaceSlots.SelectMany(iface => iface.Value))
                .Where(slot => slot.MethodDef == method);
        }
    }

    class VTables : IVTables
    {
        private readonly Dictionary<TypeDef, VTable> _tables = new Dictionary<TypeDef, VTable>();

        VTable GetOrBuild(TypeDef type)
        {
            if (!_tables.TryGetValue(type, out var ret))
                ret = _tables[type] = new VTable.Builder(type, this).Build();
            return ret;
        }

        public IVTable GetVTable(ITypeDefOrRef type)
        {
            switch (type)
            {
                case null:
                    return null;
                case TypeDef def:
                    return GetOrBuild(def);
                case TypeRef @ref:
                    return GetOrBuild(@ref.ResolveTypeDefThrow());
                case TypeSpec spec:
                    switch (spec.TypeSig)
                    {
                        case TypeDefOrRefSig refSig:
                            return GetOrBuild(refSig.TypeDefOrRef.ResolveTypeDefThrow());
                        case GenericInstSig genInst:
                            var openType = genInst.GenericType.TypeDefOrRef.ResolveTypeDefThrow();
                            return ResolveGenericArgument(openType, genInst, GetOrBuild(openType));
                        default:
                            throw new NotSupportedException("Unexpected type: " + type);
                    }
                default:
                    throw new Exception();
            }
        }

        static IVTableSlot ResolveSlot(TypeDef openType, VTableSlot slot, IList<TypeSig> genArgs)
        {
            var newSig = GenericArgumentResolver.Resolve(slot.Signature.MethodSig, genArgs);
            var newDecl = slot.MethodDefDeclType;
            if (new SigComparer().Equals(newDecl, openType))
                newDecl = new GenericInstSig((ClassOrValueTypeSig) openType.ToTypeSig(), genArgs.ToArray());
            else
                newDecl = GenericArgumentResolver.Resolve(newDecl, genArgs);
            return new VTableSlot(newDecl, slot.MethodDef, slot.DeclaringType,
                new VTableSignature(newSig, slot.Signature.Name), (VTableSlot) slot.Parent);
        }

        static VTable ResolveGenericArgument(TypeDef openType, GenericInstSig genInst, VTable vTable)
        {
            Debug.Assert(new SigComparer().Equals(openType, vTable.Type));
            return new VTable(genInst,
                vTable.Slots.Select(slot => ResolveSlot(openType, (VTableSlot) slot, genInst.GenericArguments))
                    .ToList(),
                vTable.InterfaceSlots.ToDictionary(
                    iface => GenericArgumentResolver.Resolve(iface.Key, genInst.GenericArguments),
                    iface => (IReadOnlyList<IVTableSlot>) iface.Value
                        .Select(slot => ResolveSlot(openType, (VTableSlot) slot, genInst.GenericArguments)).ToList())
            );
        }
    }
}