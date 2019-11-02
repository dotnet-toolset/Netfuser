using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Base.Rng;
using dnlib.DotNet;
using Netfuser.Core.Impl;
using Netfuser.Core.Impl.FeatureInjector;
using Netfuser.Core.Impl.Manglers.Ints;
using Netfuser.Dnext;

namespace Netfuser.Core.FeatureInjector
{
    /// <summary>
    /// This class describes <see cref="TypeDef"/> that may be used by <see cref="IFeatureInjector"/>
    /// </summary>
    public class InjectableType
    {
        private readonly ConcurrentDictionary<InjectableFeature, object> _featureDetails;
        private readonly Dictionary<FieldDef, FlagDef> _flags;

        /// <summary>
        /// <see cref="TypeMapping"/> where the injectable  <see cref="TypeDef"/> belongs
        /// </summary>
        public readonly TypeMapping TypeMapping;

        /// <summary>
        /// Base type of this injectable type
        /// </summary>
        public readonly InjectableType Base;

        /// <summary>
        /// List of constructors that may be used to instantiate this injectable type.
        /// </summary>
        public readonly IReadOnlyList<MethodDef> Ctors;

        /// <summary>
        /// Fields of the injectable type that are used as flags to indicate that the injected code should be executed
        /// </summary>
        public IReadOnlyDictionary<FieldDef, FlagDef> Flags => _flags;

        internal InjectableType(TypeMapping tm, InjectableType @base, IReadOnlyList<MethodDef> ctors)
        {
            TypeMapping = tm;
            Base = @base;
            Ctors = ctors;
            _featureDetails = new ConcurrentDictionary<InjectableFeature, object>();
            _flags = new Dictionary<FieldDef, FlagDef>();
        }

        public T Details<T>(InjectableFeature f, Func<T> creator = null)
            where T : class =>
            creator == null
                ? _featureDetails.TryGetValue(f, out var d) ? (T) d : null
                : (T) _featureDetails.GetOrAdd(f, ff => creator());

        public FlagDef GetOrAddFlag(IRng rng)
        {
            FlagDef flagDef;
            if (rng.NextBoolean() || (flagDef = _flags.Values.RandomElementOrDefault(rng)) == null)
            {
                var fld = new FieldDefUser("_injFlag" + _flags.Count,
                    new FieldSig(
                        TypeMapping.Source.Module.CorLibTypes.GetCorLibTypeSig(IntConstraints.SupportedElementTypes
                            .RandomElementOrDefault(rng))),
                    FieldAttributes.Public) {DeclaringType2 = TypeMapping.Source};
                flagDef = new FlagDef(this, fld);
                _flags.Add(fld, flagDef);
            }

            return flagDef;
        }
    }
}