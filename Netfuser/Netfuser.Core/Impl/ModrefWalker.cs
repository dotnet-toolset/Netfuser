using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Base.Collections;
using Base.Lang;
using Base.Logging;
using System.Text;

namespace Netfuser.Core.Impl
{
    class ModrefWalker
    {
        private class ModState
        {
            public readonly ModuleDef Module;
            public readonly ISet<ModState> ReferencesToMe;

            public ModuleTreat Treat;
            public bool Resolve, Resolved;
            public ModState(ModuleDef module)
            {
                Module = module;
                ReferencesToMe = new HashSet<ModState>();
            }

            public override string ToString()
            {
                var result = new StringBuilder().Append(Treat);
                if (Resolve) result.Append(", will resolve references");
                if (Resolved) result.Append(", references resolved");
                return result.ToString();
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(this, obj)) return true;
                return obj is ModState other && Equals(Module, other.Module);
            }

            public override int GetHashCode()
            {
                return Module.GetHashCode();
            }
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
                var name = source.Location ?? source.Name;
                if (_recursionCounter.Value > 100)
                    throw _context.Error("recursive module reference to " + name);
                if (!_states.TryGetValue(source, out var state))
                {
                    var ev = new NetfuserEvent.ResolveSourceModules(_context, source)
                    { ResolveReferences = true, Treat = ModuleTreat.Merge };
                    if (!IsInMainDir(source.Location))
                    {
                        ev.Treat = ModuleTreat.Ignore;
                        ev.ResolveReferences = false;
                    }
                    else if (parent != null && (parent.Treat == ModuleTreat.Copy || parent.Treat == ModuleTreat.Embed))
                        ev.Treat = parent.Treat;

                    if (ev.Treat == ModuleTreat.Merge && source.Assembly.CustomAttributes.Any(a => a.TypeFullName == "System.Reflection.AssemblyMetadataAttribute" && a.HasConstructorArguments && Convert.ToString(a.ConstructorArguments[0].Value) == "NotSupported" && Convert.ToString(a.ConstructorArguments[1].Value) == "True")) // these are ref assemblies
                    {
                        ev.Treat = ModuleTreat.Ignore;
                    }

                    _context.Fire(ev);
                    _states.Add(source, state = new ModState(source) { Treat = ev.Treat, Resolve = ev.ResolveReferences });
                    _context.Info($"module {name}: {state}");
                }
                if (parent != null)
                    state.ReferencesToMe.Add(parent);

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

        void AdjustEmbeddedReferences()
        {
            while (true)
            {
                var mustChangeToEmbed = _states.Values.Where(s => s.ReferencesToMe.Any(r => r.Treat == ModuleTreat.Embed) && s.Treat == ModuleTreat.Merge).ToList();
                if (mustChangeToEmbed.Count == 0) break;
                foreach (var s in mustChangeToEmbed)
                {
                    _context.Info($"module {s.Module.Name} must be embedded because it is referenced by embedded module(s)");
                    s.Treat = ModuleTreat.Embed;
                }
            }
        }

        public IReadOnlyDictionary<ModuleTreat, IReadOnlySet<ModuleDef>> Resolve()
        {
            foreach (var source in _sources)
                Resolve(source, null);
            AdjustEmbeddedReferences();
            return _states.ToLookup(kv => kv.Value.Treat, kv => kv.Key).Where(l => l.Any())
                .ToDictionary(l => l.Key, l => l.AsReadOnlySet());
        }
    }
}