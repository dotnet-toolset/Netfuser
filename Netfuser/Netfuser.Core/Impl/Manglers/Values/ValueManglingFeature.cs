using System.Collections.Generic;
using dnlib.DotNet;
using Netfuser.Core.FeatureInjector;
using Netfuser.Core.Impl.FeatureInjector;
using Netfuser.Dnext;

namespace Netfuser.Core.Impl.Manglers.Values
{
    public class ValueManglingFeature : InjectableFeature
    {
        public readonly TypeSig Type;
        public readonly PrimitiveConversion AlternativeTypes;

        public ValueManglingFeature(TypeSig type)
        {
            Type = type;
            AlternativeTypes = PrimitiveConversion.Get(type.ElementType);
        }

        bool IsSuitableInput(TypeSig type)
        {
            if (new SigComparer().Equals(Type, type)) return true;
            if (AlternativeTypes?.ConvertsTo.Contains(type.ElementType) ?? false) return true;
            return false;
        }

        class Injectable : InjectableMethod
        {
            private readonly IReadOnlyList<int> _arglist;

            public Injectable(InjectableType type, MethodDef method, int score, IReadOnlyList<int> arglist)
                : base(type, method, score)
            {
                _arglist = arglist;
            }

            public IEnumerable<Var> GetIOVars(ValueManglingFeature feature, IContextImpl ctx)
            {
                foreach (var a in _arglist)
                    yield return new Var.Arg(Method.Parameters[a]);
                var t = Type;
                while (t != null)
                {
                    foreach (var f in TD.Get(feature, t).Fields)
                        yield return new Var.Fld(f, fld => ctx.BasicImporter.Import(fld));
                    t = t.Base;
                }
            }
        }

        class TD
        {
            public readonly int Score;
            public readonly List<FieldDef> Fields;

            public TD(ValueManglingFeature f, InjectableType t)
            {
                Fields = new List<FieldDef>();
                foreach (var field in t.TypeMapping.Source.Fields)
                    if (!field.IsStatic && !field.IsInitOnly && (field.IsPublic || field.IsFamilyOrAssembly))
                    {
                        var ft = field.FieldType;
                        if (f.IsSuitableInput(ft)) Fields.Add(field);
                    }

                Score = t.Ctors.Count + Fields.Count;
            }

            public static TD Get(ValueManglingFeature f, InjectableType t) => t.Details(f, () => new TD(f, t));
        }

        public override InjectableMethod Rate(InjectableType t, MethodDef m)
        {
            if (!m.HasBody || m.IsConstructor || m.IsStatic || !m.IsPublic || m.HasGenericParameters) return null;
            var mask = t.TypeMapping.Source.Attributes &
                       (TypeAttributes.Abstract | TypeAttributes.Interface | TypeAttributes.Sealed);
            // static class is Abstract|Sealed, but we allow non-abstract Sealed
            if (mask != 0 && mask != TypeAttributes.Sealed) return null;
            var td = TD.Get(this, t);
            var rating = td.Score;
            var parlist = new List<int>();
            foreach (var p in m.Parameters)
                if (p.IsNormalMethodParameter)
                {
                    if (IsSuitableInput(p.Type)) parlist.Add(p.Index);
                }

            return new Injectable(t, m, rating + parlist.Count, parlist);
        }

        public IEnumerable<Var> GetIOVars(InjectableMethod r, IContextImpl ctx)
        {
            return ((Injectable) r).GetIOVars(this, ctx);
        }
    }
}