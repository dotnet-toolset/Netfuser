using System;
using Netfuser.Core.Manglers.Values;
using Netfuser.Dnext;
using Netfuser.Dnext.Cil;

namespace Netfuser.Core.Impl.Manglers.Values
{
    public class DelegateCodec<TV, TM>:ICodec
    {
        public readonly IContextImpl Context;
        public readonly Func<TV, TM> Mangler; 
        public readonly Func<TM, TV> Demangler;

        public DelegateCodec(IContextImpl context, Func<TV, TM> mangler, Func<TM, TV> demangler)
        {
            Context = context;
            Mangler = mangler;
            Demangler = demangler;
        }

        public object Mangle(object value)
        {
            return Mangler((TV)value);
        }

        public object Demangle(object mangled)
        {
            return Demangler((TM)mangled);
        }

        public void LoadValue(IILEmitter emitter, object value, bool mangled)
        {
            emitter.Const(value, mangled?typeof(TM):typeof(TV));
        }

        public void EmitDemangler(IILEmitter emitter)
        {
            if (!Context.MappedTypes.TryGetValue(Demangler.Method.DeclaringType.CreateKey(), out _))
                Context.Error("demangler must be defined in the target assembly");
            var method = emitter.Importer.Import(Demangler.Method);
            var mi = emitter.Importer.Import(method);
            emitter.Call(mi);
        }

        public virtual void EmitConversion(IILEmitter emitter, Type fromType, Type toType)
        {
            if (fromType!=toType) throw new NotSupportedException();
        }
    }
}