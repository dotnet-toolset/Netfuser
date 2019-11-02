using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Base.Rng;
using dnlib.DotNet;
using Netfuser.Core.FeatureInjector;
using Netfuser.Core.Impl.FeatureInjector;
using Netfuser.Core.Impl.Manglers.Values;
using Netfuser.Core.Manglers.Ints;
using Netfuser.Core.Rng;
using Netfuser.Dnext;

namespace Netfuser.Core.Impl.Manglers.Ints
{
    class IntManglerPlugin : AbstractPlugin.Subscribed, IIntMangler
    {
        private readonly IFeatureInjector _finj;
        private ValueManglingFeature _feature;
        private readonly List<ValueDemangler> _demanglers;
        public IRng Rng { get; }

        public IntManglerPlugin(IContextImpl context)
            : base(context)
        {
            Rng = context.Plugin<IRngPlugin>().Get(NetfuserFactory.IntManglerName);
            _finj = context.FeatureInjector();
            _demanglers=new List<ValueDemangler>();
        }

        public void Emit(FeatureRequest request, int value)
        {
            ValueDemangler cdm;
            do
            {
                cdm = _demanglers.RandomElementOrDefault(Rng);
            } while (request.IsOnStack(cdm.Method.Method));

            using (request.Emitter.UseTempLocal(cdm.Method.Type.TypeMapping.Target.ToTypeSig(), out var instance))
            {
                cdm.EmitNew(request.Emitter, Rng, instance);
                var mangled = cdm.Codec.Mangle(value);
                Debug.Assert(Equals(cdm.Codec.Demangle(mangled), value));
                cdm.EmitCall(request.Emitter, Rng, instance, mangled);
            }
        }

        IEnumerable<ValueDemangler> ToNodes(InjectableMethod rating)
        {
            var ioVars = _feature.GetIOVars(rating, Context).ToList();
            var inputArgs = ioVars.Where(v => (v.Flags & VarFlags.Input) != 0).ToList().Shuffle(Rng);
            var outputArgs = ioVars.Where(v => (v.Flags & VarFlags.Output) != 0).ToList().Shuffle(Rng);
            var vi = 0;
            var vo = 0;
            while (vi < inputArgs.Count && vo < outputArgs.Count)
            {
                var input = inputArgs[vi++];
                var output = outputArgs[vo++];
                var mangler = new IntGenerator(Rng, 5);
                var mr = mangler.Generate();
                yield return new ValueDemangler(rating, mr, Rng, input, output);
                if (Rng.NextUInt32(3)==3) break;
            }

        }

        protected override void Handle(NetfuserEvent ev)
        {
            switch (ev)
            {
                case FeatureInjectorEvent.HaveInjectableTypes tsi:
                    _feature = new ValueManglingFeature(Context.TargetModule.CorLibTypes.Int32);
                    var q = Math.Max(Context.MappedTypes.Count / 100, Rng.NextInt32(50, 100));
                    foreach (var node in _finj.Rate(_feature).OrderByDescending(r => r.Score)
                        .SelectMany(ToNodes).Take(q))
                        _demanglers.Add(node);
                    break;
            }
        }
    }
}