using System.Collections.Generic;
using System.Linq;
using System.Text;
using Base.Rng;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Netfuser.Core.Manglers.Strings;
using Netfuser.Core.Rng;
using Netfuser.Dnext;

namespace Netfuser.Core.Impl.Manglers.Strings
{
    class StringMangler : AbstractPlugin.Subscribed, IStringMangler
    {
        private readonly Dictionary<MethodDef, Dictionary<Instruction, IReadOnlyList<StringPiece>>> _byMethod;
        private IReadOnlyList<IStringMangleMethod> _allDemanglers;

        public IRng Rng { get; }

        internal StringMangler(IContextImpl context)
            : base(context)
        {
            _byMethod = new Dictionary<MethodDef, Dictionary<Instruction, IReadOnlyList<StringPiece>>>();
            Rng = context.Plugin<IRngPlugin>().Get(NetfuserFactory.StringManglerName);
        }

        protected override void Handle(NetfuserEvent ev)
        {
                switch (ev)
                {
                    case NetfuserEvent.TypeSkeletonsImported tsi:
                        _allDemanglers = Context.Plugins<IStringMangleMethod>();
                        if (_allDemanglers.Count == 0) break;
                        var strings = new Dictionary<string, StringPieces>();
                        foreach (var tm in Context.MappedTypes.Values)
                            foreach (var m in tm.Source.Methods)
                            {
                                var mBody = m.Body;
                                if (mBody != null)
                                    foreach (var i in mBody.Instructions.Where(i =>
                                        i.OpCode.Code == Code.Ldstr)
                                    )
                                    {
                                        var s = Context.Fire(new StringManglerEvent.WillMangle(Context, tm, m, i)).String;
                                        if (!string.IsNullOrEmpty(s))
                                        {
                                            if (!strings.TryGetValue(s, out var p))
                                                strings.Add(s, p = new StringPieces());
                                            p.References.Add(new InstrRef(m, i));
                                        }
                                    }
                            }
                        var splitter = Context.Plugin<IStringSplitter>();
                        if (splitter != null)
                        {
                            splitter.Split(strings);
                            foreach (var kv in strings)
                            {
                                var pieces = kv.Value.Pieces;
                                if (pieces != null)
                                {
                                    string fault = null;
                                    if (pieces.Count == 1)
                                    {
                                        var value = pieces[0].Value;
                                        if (value != kv.Key) fault = value;
                                    }
                                    else if (pieces.Count > 0)
                                    {
                                        var sb = new StringBuilder();
                                        foreach (var piece in pieces) sb.Append(piece.Value);
                                        if (sb.ToString() != kv.Key) fault = sb.ToString();
                                    }
                                    if (fault != null)
                                        throw Context.Error($"string splitter faulted: {fault}!={kv.Key}");
                                    if (pieces.Any(p => string.IsNullOrEmpty(p.Value)))
                                        throw Context.Error($"string splitter returned empty string piece for {kv.Key}");
                                }
                                else kv.Value.Set(new[] { new StringPiece(kv.Key) });
                            }
                        }
                        else foreach (var kv in strings) // prefer to do it here rather than below, because it's exact string->string correspondence here, and below we may create unnecesary duplicates of StringPieces for the same strings
                                kv.Value.Set(new[] { new StringPiece(kv.Key) });
                        
                        // group strings by method and referencing ldstrs
                        foreach (var r in strings.Values)
                            foreach (var ir in r.References)
                            {
                                if (!_byMethod.TryGetValue(ir.Method, out var srl))
                                    _byMethod.Add(ir.Method, srl = new Dictionary<Instruction, IReadOnlyList<StringPiece>>());
                                srl.Add(ir.Instruction, r.Pieces);
                            }

                        strings.Clear();
                        break;
                    case NetfuserEvent.CilBodyBuilding bb:
                        if (!_byMethod.TryGetValue(bb.Source, out var list)) break;
                        _byMethod.Remove(bb.Source);
                        var il = bb.GetEmitter();
                        foreach (var kv in list)
                        {
                            var maxIter = kv.Value.Count * 100;
                            using (il.BeginInsertion(bb.Map(kv.Key)))
                            using (var sdmc = new StringMangleContext(this, bb.Source, il, kv.Value))
                            {
                                while (!sdmc.IsEmpty)
                                {
                                    var demangler = _allDemanglers.RandomElementOrDefault(Rng);
                                    if (demangler != null) // theoretically all demanglers may refuse on this attempt, but some may accept on the next, as their decision may be random
                                        sdmc.Use(demangler);
                                    if (maxIter-- <= 0) throw Context.Error("could not emit demangler for " + sdmc);

                                }
                            }
                        }

                        il.Commit();
                        break;
                }
        }
    }
}