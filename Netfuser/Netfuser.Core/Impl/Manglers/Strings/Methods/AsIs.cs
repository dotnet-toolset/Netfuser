using Netfuser.Core.Manglers.Strings;

namespace Netfuser.Core.Impl.Manglers.Strings.Methods
{
    class AsIs : StringMangleMethod
    {
        public AsIs(IContextImpl context)
            : base(context, "asis")
        {
        }

        public override StringMangleStackTop? Emit(IStringMangleContext context)
        {
            var emitter = context.Emitter;
            var part = context.Pieces.Dequeue();
            emitter.Const(part.Value);
            return StringMangleStackTop.String;
        }
    }
}