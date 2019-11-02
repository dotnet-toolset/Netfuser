using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Base.Collections;
using Base.Lang;

namespace Netfuser.Core.Impl
{
    class ModrefWalker
    {
        private class ModState
        {
            public ModuleTreat Treat;
            public bool Resolve, Resolved;
        }

        private readonly DepthCounter _recursionCounter;
        private readonly ContextImpl _context;
        private readonly IEnumerable<ModuleDef> _sources;
        private readonly Dictionary<ModuleDef, ModState> _states;

        private readonly string _mainDir;
        private readonly bool _mainDirCaseInsensitive;

        public ModrefWalker(ContextImpl context, IEnumerable<ModuleDef> sources)
        {
            _context = context;
            _sources = sources;
            _states = new Dictionary<ModuleDef, ModState>();
            _recursionCounter = new DepthCounter();
            var loc = context.MainSourceModule?.Location;
            if (!string.IsNullOrEmpty(loc))
            {
                var maindir = Path.GetDirectoryName(Path.GetFullPath(loc));
                if (Directory.Exists(maindir))
                {
                    _mainDir = maindir;
                    _mainDirCaseInsensitive =
                        Directory.Exists(maindir.ToLower()) && Directory.Exists(maindir.ToUpper());
                }
            }
        }

        bool IsInMainDir(string d)
        {
            if (_mainDir == null || string.IsNullOrEmpty(d)) return false;
            var path = Path.GetDirectoryName(Path.GetFullPath(d));
            if (!Directory.Exists(path)) return false;
            return string.Equals(_mainDir, path,
                _mainDirCaseInsensitive
                    ? StringComparison.InvariantCultureIgnoreCase
                    : StringComparison.InvariantCulture);
        }


        void Resolve(ModuleDef source, ModState parent)
        {
            using (_recursionCounter.Enter())
            {
                if (_recursionCounter.Value > 100)
                    throw _context.Error("recursive module reference to " + (source.Location ?? source.Name));
                if (!_states.TryGetValue(source, out var state))
                {
                    var ev = new NetfuserEvent.ResolveSourceModules(_context, source)
                        {ResolveReferences = true, Treat = ModuleTreat.Merge};
                    if (!IsInMainDir(source.Location))
                    {
                        ev.Treat = ModuleTreat.Ignore;
                        ev.ResolveReferences = false;
                    }
                    else if (parent != null && (parent.Treat == ModuleTreat.Copy || parent.Treat == ModuleTreat.Embed))
                        ev.Treat = parent.Treat;

                    _context.Fire(ev);
                    _states.Add(source, state = new ModState {Treat = ev.Treat, Resolve = ev.ResolveReferences});
                }

                if (!state.Resolve || state.Resolved) return;
                state.Resolved = true;
                foreach (var aref in source.GetAssemblyRefs())
                {
                    var adef = source.Context.AssemblyResolver.Resolve(aref, source);
                    if (adef != null)
                        foreach (var mdef in adef.Modules)
                            Resolve(mdef, state);
                }
            }
        }

        public IReadOnlyDictionary<ModuleTreat, IReadOnlySet<ModuleDef>> Resolve()
        {
            foreach (var source in _sources)
                Resolve(source, null);
            return _states.ToLookup(kv => kv.Value.Treat, kv => kv.Key).Where(l => l.Any())
                .ToDictionary(l => l.Key, l => l.AsReadOnlySet());
        }
    }
}