using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Pdb;
using Netfuser.Core.Naming;

namespace Netfuser.Core.Impl.Merger
{
    abstract class BaseMerger
    {
        protected readonly ContextImpl Context;
        protected readonly Importer Importer;

        protected BaseMerger(ContextImpl context, Importer importer)
        {
            Context = context;
            Importer = importer;
        }

        protected void CopyCustomAttributes(IHasCustomAttribute source, IHasCustomAttribute dest)
        {
            foreach (var sourceCustomAttribute in source.CustomAttributes)
                dest.CustomAttributes.Add(Clone(sourceCustomAttribute));
        }

        CustomAttribute Clone(CustomAttribute source) =>
            new CustomAttribute((ICustomAttributeType) Importer.Import(source.Constructor),
                source.ConstructorArguments.Select(Clone),
                source.NamedArguments.Select(a => Clone(source.Constructor.DeclaringType, a)), source.BlobOffset);

        CAArgument Clone(CAArgument source)
        {
            var value = source.Value;
            switch (source.Value)
            {
                case CAArgument v:
                    value = Clone(v);
                    break;
                case IList<CAArgument> args:
                    var newArgs = new List<CAArgument>(args.Count);
                    newArgs.AddRange(args.Select(Clone));
                    value = newArgs;
                    break;
                case TypeSig ts:
                    value = Importer.Import(ts);
                    break;
            }

            return new CAArgument(Importer.Import(source.Type), value);
        }

        CANamedArgument Clone(ITypeDefOrRef sourceType, CANamedArgument source)
        {
            var name = source.Name;
            var st = sourceType.ResolveTypeDefThrow();
            var member = source.IsField
                ? (IMemberDef) st.FindFieldCheckBaseType(name)
                : st.FindPropertyCheckBaseType(name);
            if (Context.Plugin<INaming>().FindNewName(member, out _, out var newName))
                name = newName;

            return new CANamedArgument(source.IsField, Importer.Import(source.Type), name,
                Clone(source.Argument));
        }

        SecurityAttribute Clone(SecurityAttribute source) =>
            new SecurityAttribute(Importer.Import(source.AttributeType),
                source.NamedArguments.Select(a => Clone(source.AttributeType, a)).ToList());

        protected void CopyDeclSecurities(IHasDeclSecurity source, IHasDeclSecurity dest)
        {
            if (!source.HasDeclSecurities) return;
            foreach (var sourceDeclSecurity in source.DeclSecurities)
                dest.DeclSecurities.Add(Clone(sourceDeclSecurity));
        }

        DeclSecurity Clone(DeclSecurity source)
        {
            var result = new DeclSecurityUser(source.Action, source.SecurityAttributes.Select(Clone).ToList());
            CopyCustomAttributes(source, result);
            CopyCustomDebugInfo(source, result);
            return result;
        }

        protected virtual PdbCustomDebugInfo CloneOther(PdbCustomDebugInfo source) => null;

        PdbCustomDebugInfo Clone(PdbCustomDebugInfo source)
        {
            var compatible = true;
            switch (Context.TargetModule.PdbState.PdbFileKind)
            {
                case PdbFileKind.WindowsPDB:
                    if ((uint) source.Kind > byte.MaxValue)
                        compatible = false;
                    break;
            }

            PdbCustomDebugInfo result = null;
            if (compatible)
                switch (source)
                {
                    case PdbForwardMethodInfoCustomDebugInfo i:
                        return null; // currently not supported by dnlib
                        result = new PdbForwardMethodInfoCustomDebugInfo((IMethodDefOrRef) Importer.Import(i.Method));
                        break;
                    case PdbForwardModuleInfoCustomDebugInfo i:
                        return null; // currently not supported by dnlib
                        result = new PdbForwardModuleInfoCustomDebugInfo((IMethodDefOrRef) Importer.Import(i.Method));
                        break;
                    case PdbStateMachineTypeNameCustomDebugInfo i:
                        return null; // currently not supported by dnlib
                        result = new PdbStateMachineTypeNameCustomDebugInfo(Importer.Import(i.Type).ResolveTypeDef());
                        break;
                    case PdbTupleElementNamesCustomDebugInfo i1:
                    case PortablePdbTupleElementNamesCustomDebugInfo i2:
                    case PdbDefaultNamespaceCustomDebugInfo i3:
                    case PdbDynamicLocalVariablesCustomDebugInfo i4:
                    case PdbEmbeddedSourceCustomDebugInfo i5:
                    case PdbSourceLinkCustomDebugInfo i6:
                    case PdbSourceServerCustomDebugInfo i7:
                        throw new NotImplementedException();
                    default:
                        result = CloneOther(source);
                        break;
                }
            return Context.Fire(new NetfuserEvent.CloneCustomDebugInfo(Context, source) {Target = result}).Target;
        }

        protected void CopyCustomDebugInfo(IHasCustomDebugInformation source, IHasCustomDebugInformation dest)
        {
            if (Context.TargetModule.PdbState == null || !source.HasCustomDebugInfos)
                return;
            foreach (var si in source.CustomDebugInfos)
            {
                var cloned = Clone(si);
                if (cloned != null)
                    dest.CustomDebugInfos.Add(cloned);
            }
        }
    }
}