using System.Collections.Generic;
using dnlib.DotNet;
using Netfuser.Core.Impl.FeatureInjector;

namespace Netfuser.Core.FeatureInjector
{
    /// <summary>
    /// This class describes location of a flag variable that determines whether a particular injection is to
    /// be activated or not when the method with the injected code is called.
    /// One <see cref="FlagDef"/> may be used to switch between multiple injected pieces.
    /// Flag value may be set when instantiating the injectable type (in the constructor), or by directly setting certain field in the type 
    /// </summary>
    public class FlagDef
    {
        private MethodDef _ctor;
        private Parameter _flagPar;
        private readonly List<InjectedFeature> _features;

        /// <summary>
        /// Injectable type where the flag is located
        /// </summary>
        public readonly InjectableType Type;

        /// <summary>
        /// If the flag is passed as a field value, this references the relevant field, otherwise <see langword="null"/>
        /// </summary>
        public readonly FieldDef Field;

        /// <summary>
        /// If the flag is passed as a constructor argument, this references the relevant constructor, otherwise <see langword="null"/>
        /// </summary>
        public MethodDef Ctor => _ctor;

        /// <summary>
        /// If the flag is passed as a constructor argument, this references the relevant parameter, otherwise <see langword="null"/>
        /// </summary>
        public Parameter FlagParameter => _flagPar;

        /// <summary>
        /// List of features that rely on this flag
        /// </summary>
        public IReadOnlyList<InjectedFeature> Features => _features;

        internal FlagDef(InjectableType it, FieldDef field)
        {
            Type = it;
            Field = field;
            _features = new List<InjectedFeature>();
        }

        internal void SetCtor(MethodDef ctor, Parameter flagPar)
        {
            _ctor = ctor;
            _flagPar = flagPar;
        }

        internal void AddFeature(InjectedFeature feature)
        {
            _features.Add(feature);
        }
    }
}