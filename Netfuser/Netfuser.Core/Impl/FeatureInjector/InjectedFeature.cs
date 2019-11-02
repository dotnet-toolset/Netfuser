using System;
using Base.Rng;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Netfuser.Core.FeatureInjector;
using Netfuser.Core.Impl.Manglers.Ints;
using Netfuser.Dnext;
using Netfuser.Dnext.Cil;

namespace Netfuser.Core.Impl.FeatureInjector
{
    /// <summary>
    /// Base class for actual injection of code
    /// </summary>
    public abstract class InjectedFeature
    {
        /// <summary>
        /// Method to use for injection
        /// </summary>
        public readonly InjectableMethod Method;

        /// <summary>
        /// If the feature returns something, this points at the location of the output var,
        /// <see langword="null"/> otherwise 
        /// </summary>
        public readonly Var Output;

        /// <summary>
        /// Flag that triggers the injected code
        /// </summary>
        public readonly FlagDef Flag;

        /// <summary>
        /// Constraints for the <see cref="Flag"/>. If the  <see cref="Flag"/> value is within these constraints, the injected code is triggered.
        /// May be <see langword="null"/>, in which case the specific <see cref="Value"/> is used as a constraint  
        /// </summary>
        public readonly IntConstraints Constraints;

        /// <summary>
        /// If the  <see cref="Flag"/> value equals to this value, the injected code is triggered.
        /// May be <see langword="null"/>, in which case the <see cref="Constraints"/> are used  
        /// </summary>
        public readonly long? Value;


        protected InjectedFeature(InjectableMethod method, Var output, IRng rng)
        {
            Method = method;
            Output = output;
            var maxFlags = 15;
            bool overlaps;
            do
            {
                var flag = method.Type.GetOrAddFlag(rng);
                var (signed, bytes) = IntConstraints.GetProps(flag.Field.FieldType.ElementType);
                var maxOverlaps = 10;
                do
                {
                    long? value;
                    IntConstraints constraints;
                    if (rng.NextBoolean())
                    {
                        value = null;
                        constraints = IntConstraints.Generate(rng, bytes, signed);
                    }
                    else
                    {
                        value = IntConstraints.NextInt(rng, bytes, signed);
                        constraints = null;
                    }

                    overlaps = false;
                    foreach (var feature in flag.Features)
                    {
                        if (feature.Value.HasValue)
                        {
                            if (value.HasValue && feature.Value.Value == value.Value) overlaps = true;
                            else if (constraints != null && constraints.Contains(feature.Value.Value)) overlaps = true;
                        }
                        else
                        {
                            if (value.HasValue && feature.Constraints.Contains(value.Value)) overlaps = true;
                            else if (constraints != null && constraints.Overlaps(feature.Constraints)) overlaps = true;
                        }

                        if (overlaps) break;
                    }

                    if (!overlaps)
                    {
                        Value = value;
                        Constraints = constraints;
                        flag.AddFeature(this);
                        Flag = flag;
                        break;
                    }
                } while (maxOverlaps-- > 0);
            } while (overlaps && maxFlags-- > 0);

            if (overlaps)
                throw new Exception("could not create flag for injected feature");
        }

        public void EmitChecker(IILEmitter emitter, IRng rng, Instruction normalFlowTarget)
        {
            var fv = Flag.Field;
            emitter.Emit(OpCodes.Ldarg_0);
            emitter.Emit(OpCodes.Ldfld, emitter.Importer.Import(fv));
            if (Constraints != null)
            {
                emitter.NumericConversion(fv.FieldType.ElementType, Constraints.ElementType, false);
                Constraints.EmitChecker(rng, emitter, normalFlowTarget);
            }
            else
            {
                var (signed, bytes) = IntConstraints.GetProps(Flag.Field.FieldType.ElementType);
                emitter.Const(Value.Value, bytes, signed);
                emitter.Emit(OpCodes.Bne_Un, normalFlowTarget);
            }
        }

        private void EmitFlagValue(IILEmitter emitter, IRng rng)
        {
            if (Constraints != null)
                Constraints.EmitValue(emitter, rng);
            else
            {
                var (signed, bytes) = IntConstraints.GetProps(Flag.Field.FieldType.ElementType);
                emitter.Const(Value.Value, bytes, signed);
            }
        }

        public void EmitNew(IILEmitter emitter, IRng rng, Local instance)
        {
            var ctor = Flag.Type.Ctors.RandomElementOrDefault(rng);
            if (ctor == null || (Flag.Ctor != null && rng.NextBoolean()))
            {
                foreach (var pd in Flag.Ctor.Parameters)
                    if (pd.IsNormalMethodParameter)
                    {
                        if (pd == Flag.FlagParameter)
                            EmitFlagValue(emitter, rng);
                        else
                            Utils.RandomConst(emitter, pd.Type, rng);
                    }

                emitter.Newobj(emitter.Importer.Import(Flag.Ctor));
                emitter.Stloc(instance);
            }
            else
            {
                foreach (var pd in ctor.Parameters)
                    if (pd.IsNormalMethodParameter)
                    {
                        Utils.RandomConst(emitter, pd.Type, rng);
                    }

                emitter.Newobj(emitter.Importer.Import(ctor));
                emitter.Stloc(instance);
                emitter.Ldloc(instance);
                EmitFlagValue(emitter, rng);
                emitter.Emit(OpCodes.Stfld, emitter.Importer.Import(Flag.Field));
            }
        }

        public abstract void Emit(MethodDef target, IILEmitter emitter, IRng rng);
    }
}