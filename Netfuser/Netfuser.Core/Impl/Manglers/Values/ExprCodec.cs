using System;
using System.Linq.Expressions;
using dnlib.DotNet.Emit;
using Netfuser.Core.Manglers.Values;
using Netfuser.Dnext.Cil;

namespace Netfuser.Core.Impl.Manglers.Values
{
    public class ExprCodec : ICodec
    {
        private readonly Expression _encoder, _decoder;
        private readonly ParameterExpression _encoderArg, _decoderArg;

        private Delegate _encoderDelegate, _decoderDelegate;

        public Func<ParameterExpression, Local> ParameterResolver;

        public ExprCodec(Expression encoder, ParameterExpression encoderArg, Expression decoder,
            ParameterExpression decoderArg)
        {
            _encoder = encoder;
            _encoderArg = encoderArg;
            _decoder = decoder;
            _decoderArg = decoderArg;
        }

        Delegate CompileEncoder()
        {
            lock (this)
                return _encoderDelegate ??= Expression.Lambda(_encoder, _encoderArg).Compile();
        }

        Delegate CompileDecoder()
        {
            lock (this)
                return _decoderDelegate ??= Expression.Lambda(_decoder, _decoderArg).Compile();
        }

        public object Mangle(object value)
        {
            return CompileEncoder().DynamicInvoke(value);
        }

        public object Demangle(object mangled)
        {
            return CompileDecoder().DynamicInvoke(mangled);
        }

        public void LoadValue(IILEmitter emitter, object value, bool mangled)
        {
            emitter.Const(value, value.GetType());
        }

        public void EmitDemangler(IILEmitter emitter)
        {
            new ExprCompiler(emitter) {ParameterResolver = ParameterResolver}.Compile(_decoder);
        }

        public void EmitConversion(IILEmitter emitter, Type fromType, Type toType)
        {
            emitter.NumericConversion(fromType, toType, false);
        }
    }
}