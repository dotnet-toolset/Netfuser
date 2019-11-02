using Base.Rng;
using Netfuser.Core.FeatureInjector;
using Netfuser.Core.Manglers.Strings;

namespace Netfuser.Core.Impl.Manglers.Strings.Methods
{
    class AsInt : StringMangleMethod
    {
        public AsInt(IContextImpl context)
            : base(context, "int")
        {
        }

        public override StringMangleStackTop? Emit(IStringMangleContext context)
        {
            var part = context.Pieces.Peek();
            if (part.Bits * part.Value.Length > 32) return null;
            var emitter = context.Emitter;
            var fr = new FeatureRequest(Context, emitter);
            using (var r = fr.Enter(context.SourceMethod))
                if (r != null)
                {
                    part = context.Pieces.Dequeue();
                    var be = context.Mangler.Rng.NextBoolean();
                    var i = SmUtils.String2Int(part.Value, part.Bits, be);
                    Context.IntMangler().Emit(fr, i);
                    return SmUtils.Int2String(context, part.Value.Length, part.Bits,
                        be);
                }
            return null;
        }
    }
}