using System;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Netfuser.Dnext.Cil;

namespace Netfuser.Dnext
{
    [Flags]
    public enum VarFlags
    {
        None = 0,
        Input = 1 << 0,
        Output = 1 << 1,
        Ret = 1 << 2
    }

    public abstract class Var
    {
        public abstract TypeSig Type { get; }
        public abstract VarFlags Flags { get; }

        public abstract void Load(IILEmitter emitter);
        public abstract void Store(IILEmitter emitter, Action pushValue = null);

        public class Arg : Var
        {
            public readonly Parameter Parameter;
            public override TypeSig Type => Parameter.Type;

            public override VarFlags Flags =>
                Parameter.IsReturnTypeParameter ? (VarFlags.Output | VarFlags.Ret) : VarFlags.Input;

            public Arg(Parameter parameter)
            {
                Parameter = parameter;
            }

            public override void Load(IILEmitter emitter)
            {
                if (Parameter.IsReturnTypeParameter)
                    throw new NotSupportedException();
                if (Parameter.ParamDef?.IsOut ?? false)
                    emitter.Ldarga(Parameter);
                else
                    emitter.Ldarg(Parameter);
            }

            public override void Store(IILEmitter emitter, Action pushValue = null)
            {
                pushValue?.Invoke();
                if (Parameter.IsReturnTypeParameter)
                    emitter.Emit(OpCodes.Ret);
                else
                    emitter.Starg(Parameter);
            }
        }

        public class Loc : Var
        {
            public readonly Local Local;
            public override TypeSig Type => Local.Type;
            public override VarFlags Flags => VarFlags.None;

            public Loc(Local local)
            {
                Local = local;
            }

            public override void Load(IILEmitter emitter)
            {
                emitter.Ldloc(Local);
            }

            public override void Store(IILEmitter emitter, Action pushValue = null)
            {
                pushValue?.Invoke();
                emitter.Stloc(Local);
            }
        }

        public class Fld : Var
        {
            public readonly IField Field;
            private readonly Func<IField, IField> _resolver;
            public override TypeSig Type => Field.FieldSig.GetFieldType();
            public override VarFlags Flags => VarFlags.Input | VarFlags.Output;

            public Fld(IField field, Func<IField, IField> resolver)
            {
                Field = field;
                _resolver = resolver;
            }

            public override void Load(IILEmitter emitter)
            {
                emitter.Emit(OpCodes.Ldarg_0);
                Ldfld(emitter);
            }

            public override void Store(IILEmitter emitter, Action pushValue = null)
            {
                if (pushValue == null)
                    using (emitter.UseTempLocal(Type, out var local))
                    {
                        emitter.Stloc(local);
                        emitter.Emit(OpCodes.Ldarg_0);
                        emitter.Ldloc(local);
                    }
                else
                {
                    emitter.Emit(OpCodes.Ldarg_0);
                    pushValue.Invoke();
                }

                Stfld(emitter);
            }

            public void Stfld(IILEmitter emitter)
            {
                emitter.Emit(Instruction.Create(OpCodes.Stfld, _resolver?.Invoke(Field) ?? Field));
            }

            public void Ldfld(IILEmitter emitter)
            {
                emitter.Emit(Instruction.Create(OpCodes.Ldfld, _resolver?.Invoke(Field) ?? Field));
            }
        }

        public class Arr : Var
        {
            public readonly Var Array;
            public readonly int Index;
            public override TypeSig Type => Array.Type;
            public override VarFlags Flags => Array.Flags;

            public Arr(Var array, int index)
            {
                Array = array;
                Index = index;
            }

            public override void Load(IILEmitter emitter)
            {
                Array.Load(emitter);
                emitter.Const(Index);
                emitter.Ldelem(Array.Type.Next);
            }

            public override void Store(IILEmitter emitter, Action pushValue = null)
            {
                if (pushValue == null)
                    using (emitter.UseTempLocal(Array.Type.Next, out var local))
                    {
                        emitter.Stloc(local);
                        Array.Load(emitter);
                        emitter.Const(Index);
                        emitter.Ldloc(local);
                    }
                else
                {
                    Array.Load(emitter);
                    emitter.Const(Index);
                    pushValue.Invoke();
                }

                emitter.Stelem(Array.Type.Next);
            }
        }
    }
}