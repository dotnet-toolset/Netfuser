using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using Base.Rng;
using dnlib.DotNet;
using Netfuser.Core.FeatureInjector;
using Netfuser.Core.Impl.FeatureInjector;
using Netfuser.Core.Impl.Manglers.Values;
using Netfuser.Core.Manglers.Strings;
using Netfuser.Core.Manglers.Values;
using Netfuser.Dnext;

namespace Netfuser.Core.Impl.Manglers.Strings.Methods
{
    class Injectable : StringMangleMethod
    {
        private readonly List<ICodec> _codecs;
        private readonly IDisposable _subscription;

        private readonly List<ValueDemangler> _demanglers;
        private ValueManglingFeature _feature;
    
        public Injectable(IContextImpl context, string name) 
            : base(context, name)
        {
            _codecs = new List<ICodec>();
            var finj = context.FeatureInjector();
            _demanglers=new List<ValueDemangler>();
            _subscription = Context.OfType<FeatureInjectorEvent.HaveInjectableTypes>().Subscribe(ime =>
            {
                _feature = new ValueManglingFeature(Context.TargetModule.CorLibTypes.String);
                var q = Math.Max(Context.MappedTypes.Count / 100, Mangler.Rng.NextInt32(50, 100));
                foreach (var node in finj.Rate(_feature).OrderByDescending(r => r.Score)
                    .SelectMany(ToNodes).Take(q))
                    _demanglers.Add(node);
            });
        }

        protected override void OnDispose()
        {
            _subscription.Dispose();
        }

        public void Add(ICodec codec)
        {
            _codecs.Add(codec);
        }

        IEnumerable<ValueDemangler> ToNodes(InjectableMethod rating)
        {
            var ioVars = _feature.GetIOVars(rating, Context).ToList();
            var inputArgs = ioVars.Where(v => (v.Flags & VarFlags.Input) != 0).ToList().Shuffle(Mangler.Rng);
            var outputArgs = ioVars.Where(v => (v.Flags & VarFlags.Output) != 0).ToList().Shuffle(Mangler.Rng);
            var vi = 0;
            var vo = 0;
            while (vi < inputArgs.Count && vo < outputArgs.Count)
            {
                var input = inputArgs[vi++];
                var output = outputArgs[vo++];
                var codec = _codecs.RandomElementOrDefault(Mangler.Rng);
                yield return new ValueDemangler(rating, codec, Mangler.Rng, input, output);
                if (Mangler.Rng.NextUInt32(3)==3) break;
            }

        }

        public override StringMangleStackTop? Emit(IStringMangleContext context)
        {
            var emitter = context.Emitter;
            var fr = new FeatureRequest(Context, emitter);
            using (var r = fr.Enter(context.SourceMethod))
                if (r != null)
                {
                    ValueDemangler cdm;
                    do
                    {
                        cdm = _demanglers.RandomElementOrDefault(Mangler.Rng);
                    } while (fr.IsOnStack(cdm.Method.Method));
                    
                     var part = context.Pieces.Dequeue();
                    using (emitter.UseTempLocal(cdm.Method.Type.TypeMapping.Target.ToTypeSig(), out var instance))
                    {
                        cdm.EmitNew(emitter, Mangler.Rng, instance);
                        var mangledPart=cdm.Codec.Mangle(part.Value);
                        Debug.Assert(Equals(cdm.Codec.Demangle(mangledPart), part.Value));
                        cdm.EmitCall(emitter, Mangler.Rng, instance, mangledPart);
                    }

                    return StringMangleStackTop.String;
                }

            return null;
                
        }
    }
}