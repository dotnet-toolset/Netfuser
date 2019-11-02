using dnlib.DotNet;
using Netfuser.Dnext;

namespace Netfuser.Core.Impl.Merger
{
    class ModuleMerger : BaseMerger
    {
        private readonly ModuleDef _source;

        private ModuleMerger(ContextImpl context, Importer importer, ModuleDef source)
            : base(context, importer)
        {
            _source = source;
        }

        FileDef Clone(FileDef source)
        {
            var result = new FileDefUser(source.Name, source.Flags, source.HashValue);
            CopyCustomAttributes(source, result);
            CopyCustomDebugInfo(source, result);
            return result;
        }

        internal void Run()
        {
            var target = Context.TargetModule;
            if (_source.HasResources)
                foreach (var r in _source.Resources)
                    if (r is LinkedResource lr &&
                        Context.MappedResources.TryGetValue(DnextFactory.NewTypeKey(_source, r), out var mapping) &&
                        mapping.Target is LinkedResource tlr && tlr.File == lr.File)
                    {
                        tlr.File = Clone(lr.File);
                    }


            if (_source == Context.MainSourceModule)
            {
                if (_source.Win32Resources != null)
                    target.Win32Resources = _source.Win32Resources;
                var sourceAssembly = _source.Assembly;
                var targetAssembly = target.Assembly;
                CopyCustomAttributes(_source, target);
                CopyCustomAttributes(sourceAssembly, targetAssembly);
                CopyCustomDebugInfo(_source, target);
                CopyCustomDebugInfo(sourceAssembly, targetAssembly);
                CopyDeclSecurities(sourceAssembly, targetAssembly);
                if (_source.IsEntryPointValid)
                    target.EntryPoint = Importer.Import(_source.EntryPoint).ResolveMethodDef();
            }
        }

        internal static ModuleMerger Create(ContextImpl context, ModuleDef source)
        {
            return new ModuleMerger(context, context.BasicImporter, source);
        }
    }
}