using System.Collections.Generic;
using System.Linq;
using System.Text;
using Base.Lang;
using Base.Rng;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Netfuser.Core.Manglers.Strings;
using Netfuser.Dnext;
using Netfuser.Dnext.Cil;

namespace Netfuser.Core.Impl.Manglers.Strings
{
    public class StringMangleContext : Disposable, IStringMangleContext
    {
        private Local _sbLocal;

        public IStringMangler Mangler { get; }
        public MethodDef SourceMethod { get; }
        public IILEmitter Emitter { get; }
        public Queue<StringPiece> Pieces { get; }
        public bool IsEmpty => Pieces.Count == 0;
        public StringMangleStackTop StackTop { get; private set; }
        public StringMangleContext(IStringMangler mangler, MethodDef sourceMethod, IILEmitter emitter, IEnumerable<StringPiece> parts)
        {
            Mangler = mangler;
            SourceMethod = sourceMethod;
            Emitter = emitter;
            Pieces = new Queue<StringPiece>(parts);
            if (Pieces.Count == 0) throw new CodeBug.Unreachable();
            if (Pieces.Count > 1 && Mangler.Rng.NextBoolean())
                EnsureStringBuilderOnStackTop(); // if there are multiple parts, we sometimes initialize StringBuilder, and sometimes leave it to the point when the second part is about to be demangled
        }

        protected override void OnDispose()
        {
            switch (StackTop)
            {
                case StringMangleStackTop.StringBuilder:
                    SmUtils.ToString(Emitter);
                    break;
                case StringMangleStackTop.String:
                    break;
                case StringMangleStackTop.Unknown:
                    throw new CodeBug.Unreachable();
            }
            if (_sbLocal != null)
                Emitter.TempLocals.Release(_sbLocal);
        }

        public override string ToString()
        {
            return Pieces.Select(p => p.Value).Join("+");
        }

        public void EnsureStringBuilderOnStackTop()
        {
            switch (StackTop)
            {
                case StringMangleStackTop.Unknown:
                    LoadOrCreateStringBuilder();
                    break;
                case StringMangleStackTop.String:
                    using (Emitter.UseTempLocal<string>(out var stringVar))
                    {
                        Emitter.Stloc(stringVar);
                        LoadOrCreateStringBuilder();
                        Emitter.Ldloc(stringVar);
                    }
                    Emitter.Callvirt(SmUtils.Method_StringBuilder_Append_String);
                    break;
                case StringMangleStackTop.StringBuilder:
                    break;
                default: throw new CodeBug.Unreachable();

            }
            StackTop = StringMangleStackTop.StringBuilder;
        }

        public Local GetStringBuilderLocal(bool create = false) =>
            _sbLocal ?? (create ? (_sbLocal = Emitter.RequestTempLocal<StringBuilder>()) : null);

        void LoadOrCreateStringBuilder(Local initString)
        {
            if (_sbLocal != null)
            {
                Emitter.Ldloc(_sbLocal);
                Emitter.Ldloc(initString);
                Emitter.Callvirt(SmUtils.Method_StringBuilder_Append_String);
            }
            else SmUtils.NewStringBuilder(Emitter, initString);
        }
        void LoadOrCreateStringBuilder()
        {
            if (_sbLocal != null) Emitter.Ldloc(_sbLocal);
            else SmUtils.NewStringBuilder(Emitter);
        }

        internal void Use(IStringMangleMethod demangler)
        {
            if (StackTop == StringMangleStackTop.String) // means we have more than one part, and previous demangler left us with a string
                EnsureStringBuilderOnStackTop();
            var top = demangler.Emit(this);
            if (!top.HasValue) return; // demangler refused to process current piece, calling code will select another demangler
            switch (top.Value)
            {
                case StringMangleStackTop.String:
                    switch (StackTop)
                    {
                        case StringMangleStackTop.String:
                            using (Emitter.UseTempLocal<int>(out var stringVar1))
                            using (Emitter.UseTempLocal<int>(out var stringVar2))
                            {
                                Emitter.Stloc(stringVar1);
                                Emitter.Stloc(stringVar2);
                                LoadOrCreateStringBuilder(stringVar1);
                                Emitter.Ldloc(stringVar2);
                            }
                            Emitter.Callvirt(SmUtils.Method_StringBuilder_Append_String);
                            break;
                        case StringMangleStackTop.StringBuilder:
                            Emitter.Callvirt(SmUtils.Method_StringBuilder_Append_String);
                            top = StringMangleStackTop.StringBuilder;
                            break;
                    }
                    break;
                case StringMangleStackTop.StringBuilder:
                    if (StackTop == StringMangleStackTop.String)
                        throw Mangler.Context.Error("demangler must call EnsureStringBuilderOnStackTop() if it needs a string builder instead of creating its own");
                    // StackTop == Unknown is Okay, means StringBuilder was created (and hopefully populated) by the demangler
                    // StackTop == StringBuilder is Okay, means StringBuilder was added to (and hopefully not created) by the demangler
                    break;
                default: throw Mangler.Context.Error("demangler must leave either string or StringBuilder on stack");
            }
            StackTop = top.Value;
        }
    }
}