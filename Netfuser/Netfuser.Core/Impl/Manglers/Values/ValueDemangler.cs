using Base.Rng;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Netfuser.Core.FeatureInjector;
using Netfuser.Core.Impl.FeatureInjector;
using Netfuser.Core.Manglers.Values;
using Netfuser.Dnext;
using Netfuser.Dnext.Cil;

namespace Netfuser.Core.Impl.Manglers.Values
{
    /// <summary>
    /// This class describes injected de-mangler for simple constants (strings, ints etc)
    /// </summary>
    public class ValueDemangler : InjectedFeature
    {
        /// <summary>
        /// Codec that performs actual mangling and can emit de-mangling code
        /// </summary>
        public readonly ICodec Codec;

        /// <summary>
        /// Location of mangled constant
        /// </summary>
        public readonly Var Input;


        public ValueDemangler(InjectableMethod method, ICodec codec, IRng rng, Var input, Var output)
            : base(method, output, rng)
        {
            Codec = codec;
            Input = input;
        }

        /// <summary>
        /// Loads mangled constant, emits de-mangling code and stores it in the output variable
        /// </summary>
        /// <param name="target"></param>
        /// <param name="emitter">IL emitter</param>
        /// <param name="rng">Pseudo-random number generator</param>
        public override void Emit(MethodDef target, IILEmitter emitter, IRng rng)
        {
            Input.Load(emitter);
            Codec.EmitDemangler(emitter);
            Output.Store(emitter);
        }

        /// <summary>
        /// Emits call to the method that contains de-mangler (injected as an <see cref="InjectedFeature"/>).
        /// This is to replace instruction(s) that load original constant in the IL
        /// </summary>
        /// <param name="emitter">IL emitter</param>
        /// <param name="rng">Pseudo-random number generator</param>
        /// <param name="instance">instance of the class where the method with injected de-mangler is located</param>
        /// <param name="mangled">mangled value</param>
        public void EmitCall(IILEmitter emitter, IRng rng, Local instance, object mangled)
        {
            var pi = Input is Var.Arg a ? a.Parameter : null;
            if (pi == null)
            {
                emitter.Ldloc(instance);
                Codec.LoadValue(emitter, mangled, true);
                Codec.EmitConversion(emitter, mangled.GetType(), Input.Type.ElementType.ToType());
                ((Var.Fld) Input).Stfld(emitter);
            }

            var method = Method.Method;
            emitter.Ldloc(instance);
            foreach (var pd in method.Parameters)
                if (pd.IsNormalMethodParameter)
                {
                    if (pi == pd)
                    {
                        Codec.LoadValue(emitter, mangled, true);
                        Codec.EmitConversion(emitter, mangled.GetType(), pd.Type.ElementType.ToType());
                    }
                    else
                        Utils.RandomConst(emitter, pd.Type, rng);
                }

            emitter.Callvirt(emitter.Importer.Import(method));
            if (method.HasReturnType)
            {
                if ((Output.Flags & VarFlags.Ret) == 0)
                    emitter.Emit(OpCodes.Pop);
            }

            if (Output is Var.Fld f)
            {
                emitter.Ldloc(instance);
                f.Ldfld(emitter);
            }
        }
    }
}