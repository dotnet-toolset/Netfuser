using Microsoft.Extensions.DependencyModel;
using Netfuser.Core.Hierarchy;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Netfuser.Core.Impl.Hierarchy
{
    class DepsJsonPlugin : AbstractPlugin.Subscribed, IDepsJsonPlugin
    {
        private DependencyContext _deps;

        DependencyContext IDepsJsonPlugin.Deps => _deps;

        public DepsJsonPlugin(IContextImpl context) 
            : base(context)
        {
        }

        protected override void Handle(NetfuserEvent ev)
        {
            switch (ev)
            {
                case NetfuserEvent.WillMergeModules _:
                    var ctx = Context;
                    var jsonDeps = Path.Combine(Path.GetDirectoryName(ctx.MainSourceModule.Location), Path.GetFileNameWithoutExtension(ctx.MainSourceModule.Location) + ".deps.json");
                    if (!File.Exists(jsonDeps)) break;
                    using (var r = new DependencyContextJsonReader())
                    using (var fs = File.OpenRead(jsonDeps))
                        _deps = r.Read(fs);
                    break;
            }
        }
    }
}
