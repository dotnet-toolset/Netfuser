using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Netfuser.Core.Manglers.Metadata;
using Netfuser.Core.Manglers.Strings;
using Netfuser.Core.Naming;
using Netfuser.Dnext;

namespace Netfuser.Core.Impl.Naming
{
    class ResNameFixer : AbstractPlugin.Subscribed, IResNameFixer
    {
        const string Suffix = ".resources";

        private readonly Dictionary<ITypeKey, Res> _bySourceType = new Dictionary<ITypeKey, Res>();

        class Res
        {
            public TypeDef TargetType;
            public EmbeddedResource Resource;
        }

        internal ResNameFixer(IContextImpl context)
            : base(context)
        {
        }

        private string ParseName(string name) =>
            name.EndsWith(Suffix) ? name.Substring(0, name.Length - Suffix.Length) : null;

        private IEnumerable<string> BuildTypeNamesToAvoid()
        {
            var ns = Context.Plugin<INaming>();
            // not all type nodes are created at this point, but this is OK, we check only created ones
            // also, TypeKey here refers to the Possible type name, not necessarily the existing one
            return _bySourceType.Keys
                .Select(t => Context.MappedTypes.TryGetValue(t, out var tm) ? ns.GetOrAddNode(tm)?.NewName : null)
                .Where(n => n != null);
        }

        protected override void Handle(NetfuserEvent ev)
        {
            var ns = Context.Plugin<INaming>();
            switch (ev)
            {
                case NetfuserEvent.ResourceMapped cre:
                    var rm = cre.Mapping;
                    if (rm.SourceModule != null && rm.Source.ResourceType == ResourceType.Embedded)
                    {
                        var typeName = ParseName(rm.Source.Name);
                        if (typeName != null)
                        {
                            _bySourceType.Add(DnextFactory.NewTypeKey(rm.SourceModule, typeName),
                                new Res { Resource = (EmbeddedResource)rm.Target });
                        }
                        ns.Preserve(rm);
                    }

                    break;
                case NameManglerEvent.GenerateName stnre:
                    if (stnre.Options is NameGenerator.Encoded enc && stnre.Source is TypeDef td)
                        if (_bySourceType.TryGetValue(td.CreateKey(), out _))
                        {
                            enc.Dictionary = ManglerCharsets.Latin;
                            // before we generate new name for this type (which is going to be used to name our resource,
                            // we need to make sure that it won't clash with any other type that has attached resource
                            stnre.Avoid(BuildTypeNamesToAvoid());
                        }

                    break;
                case NetfuserEvent.TypeSkeletonsImported ite:
                    foreach (var kv in _bySourceType)
                        if (Context.MappedTypes.TryGetValue(kv.Key, out var mapping))
                        {
                            kv.Value.TargetType = mapping.Target;
                            kv.Value.Resource.Name = mapping.Target.FullName + Suffix;
                        }

                    break;
                case StringManglerEvent.WillMangle wme:
                    if (wme.Method.ReturnType.FullName == typeof(System.Resources.ResourceManager).FullName &&
                        wme.Method.Body != null &&
                        _bySourceType.TryGetValue(wme.Method.DeclaringType.CreateKey(), out var r) &&
                        wme.String == wme.Method.DeclaringType.FullName)
                    {
                        wme.String = r.TargetType?.FullName;
                        _bySourceType.Remove(wme.Method.DeclaringType.CreateKey());
                    }
                    break;
                case NetfuserEvent.CilBodyBuilding cme:
                    if (cme.Source.ReturnType.FullName == typeof(System.Resources.ResourceManager).FullName &&
                        cme.Source.Body != null &&
                        _bySourceType.TryGetValue(cme.Source.DeclaringType.CreateKey(), out var rr) &&
                        rr.TargetType != null)
                    {
                        var instr = cme.Fragment.Instructions.FirstOrDefault(i =>
                            i.OpCode == OpCodes.Ldstr && (string)i.Operand == cme.Source.DeclaringType.FullName);
                        if (instr != null) instr.Operand = rr.TargetType.FullName;
                    }

                    break;
            }
        }
    }
}