using System;
using System.IO;
using Netfuser.Core.Writers;

namespace Netfuser.Core.Impl.Writers
{
    class AssemblyWriterPlugin : AbstractPlugin.Subscribed, IAssemblyWriterPlugin
    {
        static readonly string[] ConfigSuffixes = { ".exe.config", ".runtimeconfig.json", ".runtimeconfig.dev.json" };
        static readonly string ExeSuffix = ".exe";

        private readonly DirectoryInfo _dest;

        internal AssemblyWriterPlugin(IContextImpl context, DirectoryInfo dest)
            : base(context)
        {
            if (dest != null)
                context.OutputFolder = dest;
            else
                dest = context.OutputFolder;
            _dest = dest ?? throw new ArgumentException(nameof(dest));
        }

        protected override void Handle(NetfuserEvent ev)
        {
            switch (ev)
            {
                case NetfuserEvent.Complete ce:
                    var ctx = (IContextImpl) ce.Context;
                    var opt = new dnlib.DotNet.Writer.ModuleWriterOptions(ctx.TargetModule)
                    {
                        WritePdb = ctx.MainSourceModule.PdbState != null
                    };
                    if (!_dest.Exists) _dest.Create();
                    var mainDestPath = Path.Combine(_dest.FullName, ctx.MainSourceModule.Name);
                    ctx.TargetModule.Write(mainDestPath, opt);
                    if (ctx.SourceModules.TryGetValue(ModuleTreat.Copy, out var toCopy))
                        foreach (var mod in toCopy)
                            File.Copy(mod.Location, Path.Combine(_dest.FullName, mod.Name), true);
                    var srcFolder = Path.GetDirectoryName(ctx.MainSourceModule.Location);
                    var srcNameMinusExt = Path.GetFileNameWithoutExtension(ctx.MainSourceModule.Location);
                    var srcPathMinusExt = Path.Combine(srcFolder, srcNameMinusExt);
                    var dstPathMinusExt = Path.Combine(_dest.FullName, Path.GetFileNameWithoutExtension(ctx.MainSourceModule.Name));
                    foreach (var suffix in ConfigSuffixes)
                    {
                        var cfgPath = srcPathMinusExt + suffix;
                        if (File.Exists(cfgPath))
                            File.Copy(cfgPath, dstPathMinusExt + suffix, true);
                    }
                    ctx.MainSourceModule.Assembly.TryGetOriginalTargetFrameworkAttribute(out var fw, out var ver, out _);
                    if (fw == ".NETCoreApp" && ver.Major == 3 && !ctx.MainSourceModule.Name.EndsWith(ExeSuffix)) {
                        var launcher = srcPathMinusExt+ExeSuffix;
                        if (File.Exists(launcher))
                            File.Copy(launcher, dstPathMinusExt + ExeSuffix, true);
                    }
                    break;
            }
        }
    }
}