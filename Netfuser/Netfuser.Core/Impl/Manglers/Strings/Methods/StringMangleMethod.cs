using Netfuser.Core.Manglers.Strings;

namespace Netfuser.Core.Impl.Manglers.Strings.Methods
{
    public abstract class StringMangleMethod : AbstractPlugin, IStringMangleMethod
    {
        protected readonly IStringMangler Mangler;
        public string Name { get; }
        public abstract StringMangleStackTop? Emit(IStringMangleContext context);

        protected StringMangleMethod(IContextImpl context, string name)
            : base(context)
        {
            Name = name;
            Mangler=context.Plugin<IStringMangler>(() => new StringMangler(context));
        }
    }
}