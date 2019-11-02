using Base.Rng;
using Netfuser.Core.Manglers.ControlFlow;
using Netfuser.Core.Rng;

namespace Netfuser.Core.Impl.Manglers.ControlFlow
{
    public class CFManglerPlugin : AbstractPlugin.Subscribed, ICFMangler
    {
        public CFMangleOptions Options { get; }
        public IRng Rng { get; }

        internal CFManglerPlugin(IContextImpl context, CFMangleOptions options) 
            : base(context)
        {
            Options = options;
            Rng = context.Plugin<IRngPlugin>().Get(NetfuserFactory.CodeFlowManglerName);
        }

        protected override void Handle(NetfuserEvent ev)
        {
	        switch (ev)
	        {
		        case NetfuserEvent.MethodImported mi:
                    var method = mi.Target;
                    if (!method.HasBody) break;
                    using (var context = new CFMangleContext(this, method, mi.Importer))
                        context.Run();
			        break;
	        }
        }

       
    }
}