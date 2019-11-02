using System;
using System.Collections.Generic;
using System.Linq;
using Base.Collections.Props;
using Base.Logging;
using Base.Rng;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Netfuser.Core.FeatureInjector;
using Netfuser.Core.Naming;
using Netfuser.Core.Rng;
using Netfuser.Dnext;
using Netfuser.Dnext.Cil;

namespace Netfuser.Core.Impl.FeatureInjector
{
    public class FeatureInjectorPlugin : AbstractPlugin.Subscribed, IFeatureInjector
    {
        static readonly InjectableType NonInjectableType = new InjectableType(null, null, null);

        static readonly PropKey<TypeMapping, InjectableType>
            PropInjectable = new PropKey<TypeMapping, InjectableType>();

        private readonly List<InjectableType> _injectableTypes;
        private readonly INaming _ns;

        public IReadOnlyList<InjectableType> InjectableTypes => _injectableTypes;
        public IRng Rng { get; }

        public FeatureInjectorPlugin(IContextImpl context)
            : base(context)
        {
            Rng = context.Plugin<IRngPlugin>().Get(NetfuserFactory.FeatureInjectorName);
            _injectableTypes = new List<InjectableType>();
            _ns = context.Plugin<INaming>();
        }

        public IEnumerable<InjectableMethod> Rate(InjectableFeature feature) => from it in InjectableTypes
            from m in it.TypeMapping.Source.Methods
            where !_ns.IsPreserved(it.TypeMapping, m)
            let r = feature.Rate(it, m)
            where r != null
            select r;

        public InjectableType GetInjectableType(TypeMapping tm) => PropInjectable.GetOrAdd(tm, () => CheckSuitable(tm));

        protected override void Handle(NetfuserEvent ev)
        {
            switch (ev)
            {
                case NetfuserEvent.TypeSkeletonsImported tsi:
                    foreach (var tm in Context.MappedTypes.Values)
                        GetInjectableType(tm);
                    Context.Fire(new FeatureInjectorEvent.HaveInjectableTypes(Context));
                    break;
                case NetfuserEvent.InjectMembers ime:
                    var it = PropInjectable[ime.TypeMapping];
                    if (it != null && it != NonInjectableType)
                    {
                        foreach (var flag in it.Flags.Values)
                        {
                            ime.Add(flag.Field);
                            if (flag.Ctor == null && (it.Ctors.Count == 0 || Rng.NextBoolean()))
                                ime.Add(CreateFlagCtor(flag, s => !ime.HasInstanceCtor(s)));
                        }
                    }

                    break;
                case NetfuserEvent.CilBodyBuilding me:
                    it = PropInjectable[me.TypeMapping];
                    var features = it.Flags.Values.SelectMany(f => f.Features)
                        .Where(f => f.Method.Method == me.Source).ToList();
                    if (features.Count == 0) break;
                    var emitter = me.GetEmitter();
                    var returns = ScanReturns(me);
                    var mc = me.Fragment.First;
                    foreach (var feature in features)
                    {
                        Logger.Debug($"insertion for {me.Target.Name} at {mc}");
                        using (emitter.BeginInsertion(mc, 0, true))
                        {
                            feature.EmitChecker(emitter, Rng, mc);
                            feature.Emit(me.Target, emitter, Rng);
                            EmitEpilogue(emitter, feature, me, returns);
                            mc = emitter.CurrentFragment.First;
                        }
                    }

                    emitter.Commit();

                    break;
            }
        }

        void EmitEpilogue(IILEmitter emitter, InjectedFeature feature, NetfuserEvent.CilBodyBuilding me,
            IReadOnlyList<Tuple<Instruction, Var>> returns)
        {
            var target = me.Target;
            var rt = returns.RandomElementOrDefault(Rng);
            if (rt == null)
            {
                if (target.HasReturnType && (feature.Output.Flags & VarFlags.Ret) == 0)
                    Utils.RandomConst(emitter, target.ReturnType, Rng);
                emitter.Emit(OpCodes.Ret);
            }
            else
            {
                if (target.HasReturnType)
                {
                    if ((feature.Output.Flags & VarFlags.Ret) == 0)
                        Utils.RandomConst(emitter, target.ReturnType, Rng);
                    rt.Item2.Store(emitter);
                }

                emitter.Emit(OpCodes.Br, rt.Item1);
            }
        }

        IReadOnlyList<Tuple<Instruction, Var>> ScanReturns(NetfuserEvent.CilBodyBuilding me)
        {
            var result = new List<Tuple<Instruction, Var>>();
            var block = me.ParseBlocks().EnumRegular().FirstOrDefault();
            if (block != null)
            {
                var c = block.Fragment.Count;
                var hasRet = me.Target.HasReturnType;
                for (var i = 0; i < c; i++)
                {
                    var instr = block.Fragment.Instructions[i];
                    if (instr.OpCode.Code == Code.Ret)
                    {
                        if (!hasRet)
                            result.Add(Tuple.Create(instr, (Var) null));
                        else if (i > 0)
                        {
                            instr = block.Fragment.Instructions[i - 1];
                            if (instr.IsLdloc())
                                result.Add(Tuple.Create(instr, (Var) new Var.Loc(instr.GetLocal(me.Locals))));
                            else if (instr.OpCode.Code == Code.Ldfld && i > 1)
                                result.Add(Tuple.Create(block.Fragment.Instructions[i - 2], (Var) new Var.Fld((IField) instr.Operand, null)));
                        }
                    }
                }
            }

            return result;
        }

        MethodDef CreateFlagCtor(FlagDef flag, Func<MethodSig, bool> validator)
        {
            MethodSig sig;
            var cats = new List<Tuple<FieldDef, TypeSig>>();
            var it = flag.Type;
            var sm = it.TypeMapping.Source.Module;
            do
            {
                var qarg = Rng.NextInt32(1, 5);
                cats.Add(Tuple.Create(flag.Field, flag.Field.FieldType));
                var fq = new Queue<FieldDef>(
                    it.TypeMapping.Source.Fields.Where(f => !f.IsStatic & !f.IsLiteral & !f.HasFieldRVA));
                while (cats.Count < qarg)
                {
                    if (fq.Count > 0)
                    {
                        var f = fq.Dequeue();
                        cats.Add(Tuple.Create(f, f.FieldType));
                    }
                    else
                    {
                        cats.Add(Tuple.Create<FieldDef, TypeSig>(null, sm.CorLibTypes.Int32));
                    }
                }

                cats.Shuffle(Rng);
                var args = cats.Select(c => c.Item2).ToArray();
                sig = MethodSig.CreateInstance(sm.CorLibTypes.Void, args);
            } while (!validator(sig));

            var ctor = new MethodDefUser(".ctor", sig, MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName |
                MethodAttributes.Public) {Body = new CilBody {MaxStack = 8}};

            var i = 0;
            foreach (var t in cats)
                ctor.ParamDefs.Add(new ParamDefUser("a" + (i++), (ushort) i));
            var sc = new SigComparer();
            IMethod baseCtor;
            var bc = it.Base?.Ctors.RandomElementOrDefault(Rng);
            var emitter = DnextFactory.NewILEmitter(sm);
            emitter.Emit(OpCodes.Ldarg_0);
            if (bc != null)
            {
                foreach (var pd in bc.Parameters)
                    if (pd.IsNormalMethodParameter)
                    {
                        var f = ctor.Parameters.FirstOrDefault(p =>
                            p.IsNormalMethodParameter && sc.Equals(p.Type, pd.Type));
                        if (f != null && Rng.NextBoolean())
                            emitter.Ldarg(f);
                        else
                            Utils.RandomConst(emitter, pd.Type, Rng);
                    }

                baseCtor = bc;
            }
            else
                baseCtor = new MemberRefUser(sm, ".ctor",
                    MethodSig.CreateInstance(sm.CorLibTypes.Void),
                    sm.CorLibTypes.Object.TypeRef);

            emitter.Call(baseCtor);
            Parameter flagpar = null;
            foreach (var p in ctor.Parameters)
                if (p.IsNormalMethodParameter)
                {
                    var t = cats[p.Index - 1];
                    if (t.Item1 != null)
                    {
                        emitter.Emit(OpCodes.Ldarg_0);
                        emitter.Ldarg(p);
                        emitter.Emit(Instruction.Create(OpCodes.Stfld, t.Item1));
                        if (t.Item1 == flag.Field) flagpar = p;
                    }
                }

            emitter.Emit(OpCodes.Ret);
            emitter.Replace(ctor.Body);
            flag.SetCtor(ctor, flagpar);
            return ctor;
        }

        bool IsSuitableCtor(MethodDef ctor)
        {
            if (ctor.IsStatic || !ctor.IsConstructor || !ctor.HasBody || !ctor.IsPublic ||
                ctor.HasGenericParameters) return false;

            foreach (var i in ctor.Body.Instructions)
            {
                switch (i.OpCode.Code)
                {
                    case Code.Arglist:
                    case Code.Calli:
                    case Code.Callvirt:
                    case Code.Jmp:
                    case Code.Ldarga:
                    case Code.Ldfld:
                    case Code.Ldflda:
                    case Code.Ldsfld:
                    case Code.Ldsflda:
                    case Code.Newobj:
                    case Code.Rethrow:
                    case Code.Stsfld:
                    case Code.Tailcall:
                    case Code.Throw:
                        return false;
                    case Code.Ldstr: // this is q quick hack and it shouldn't be here. We use it to avoid recursion when mangling strings. It did happen! must investigate
                        return false;
                    /*case Code.Ldarg:
                    case Code.Ldarg_0:
                    case Code.Ldarg_1:
                    case Code.Ldarg_2:
                    case Code.Ldarg_3:
                    case Code.Ldarg_S:
                        // allow loading 'this' arg
                        if (i.GetLdargIndex() != 0) return false;
                        break;*/
                    case Code.Call:
                        // allow calling base constructor
                        var m = i.Operand as IMethod;
                        if (m.Name != ".ctor") return false;
                        if (m.DeclaringType != ctor.DeclaringType.BaseType) return false;

                        break;
                }
            }

            return true;
        }

        private InjectableType CheckSuitable(TypeMapping tm)
        {
            var t = tm.Source;
            if (!t.IsPublic && !t.IsNestedPublic && !t.IsNestedAssembly) return NonInjectableType;
            if (t.IsValueType) return NonInjectableType;
            if (t.HasGenericParameters) return NonInjectableType;
            var bt = t.BaseType;
            if (bt == null) return NonInjectableType;
            InjectableType btr = null;
            if (bt.ToTypeSig().ElementType != ElementType.Object)
            {
                var k = bt.CreateKey();
                if (!Context.MappedTypes.TryGetValue(k, out var btm)) return NonInjectableType;
                btr = GetInjectableType(btm);
                if (btr == NonInjectableType) return NonInjectableType;
            }

            var ctors = t.FindInstanceConstructors().Where(IsSuitableCtor).ToList();
            var result = new InjectableType(tm, btr, ctors);
            _injectableTypes.Add(result);
            return result;
        }
    }
}