using Netfuser.Core.Manglers.ControlFlow;
using Netfuser.Dnext.Cil;

namespace Netfuser.Core.Impl.Manglers.ControlFlow.Methods
{
    public abstract class CFMangleMethod : AbstractPlugin, ICFMangleMethod
    {
        public ICFMangler Mangler { get; }
        public string Name { get; }

        protected CFMangleMethod(IContextImpl context, string name) 
            : base(context)
        {
            Name = name;
            Mangler = context.Plugin<ICFMangler>() ?? throw context.Error($"add ${nameof(ICFMangler)} to the context first");
        }

        public abstract void Mangle(ICFMangleContext context, Block.Regular block);
    }
}