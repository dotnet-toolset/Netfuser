using System;
using System.Reflection;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Netfuser.Dnext.Cil
{
    public interface IILEmitter
    {
        ModuleDef TargetModule { get; }
        Importer Importer { get; }

        /// <summary>
        /// Main fragment that represents method body
        /// </summary>
        CilFragment MainFragment { get; }

        /// <summary>
        /// All emissions will be made into this fragment.
        /// If insertion is used (see below), this is the current (topmost) insertion's fragment.
        /// Otherwise, it is the main fragment 
        /// </summary>
        CilFragment CurrentFragment { get; }

        ITempLocals TempLocals { get; }

        IILEmitter Emit(Instruction instr);

        /// <summary>
        /// Suppose we need to replace instruction <param name="instr"></param> in the method body.
        /// So we copy all instructions up to <param name="instr"></param> to the fragment, then write our new instruction, then copy
        /// everything after <param name="instr"></param> , and finally replace method body with the new fragment.
        /// Works great, unless exception handler(s) reference(s) instruction <param name="instr"></param> .
        /// To work around this case, we call <see cref="EmitWithReplace(instr)"/> right before writing new instruction.
        /// Now when we do <see cref="Emit(new_instr)"/>, all fields from new_instr will be copied into
        /// <param name="instr"></param>  and <param name="instr"></param>  will be
        /// emitted instead of new_instr, preserving any and all references that may exist. 
        /// </summary>
        /// <param name="instr">instruction to emit with replace</param>
        /// <returns></returns>
        IILEmitter EmitWithReplace(Instruction instr);

        /// <summary>
        /// Convenience feature to insert blocks of code into the existing method body.
        /// The main benefit is speed - instead of copying arrays on every insert in the instructions list,
        /// we emit to the end of the isolated fragment, and then re-create the main fragment in one pass.
        /// <see cref="Commit"/> must be called to actually insert emitted instructions into the main fragment.
        /// </summary>
        /// <param name="i">anchor instruction, the new fragment will be inserted either after or before this instruction</param>
        /// <param name="replace">number of instructions to replace</param>
        /// <param name="before">insert before or after the anchor</param>
        /// <returns></returns>
        IDisposable BeginInsertion(Instruction i, int replace = 1, bool before = false);

        /// <summary>
        /// Flushes all pending insertions into the main fragment
        /// All insertions must be finalized by calling <c>Dispose()</c> method of every object returned by
        /// <see cref="BeginInsertion"/> before <see cref="Commit"/> is called.
        /// </summary>
        void Commit();

        IILEmitter Ldelem(TypeSig type);
        IILEmitter Ldelem(Type type);
        IILEmitter Stelem(TypeSig type);
        IILEmitter Stelem(Type type);

        IILEmitter Ldloc(Local v);
        IILEmitter Ldloca(Local v);
        IILEmitter Stloc(Local v);
        IILEmitter Ldarg(Parameter p);
        IILEmitter Ldarga(Parameter p);
        IILEmitter Starg(Parameter p);

        IILEmitter Const(bool value);
        IILEmitter Const(char value);
        IILEmitter Const(byte value);
        IILEmitter Const(sbyte value);
        IILEmitter Const(short value);
        IILEmitter Const(ushort value);
        IILEmitter Const(int value);
        IILEmitter Const(uint value);
        IILEmitter Const(long value);
        IILEmitter Const(ulong value);
        IILEmitter Const(float value);
        IILEmitter Const(double value);
        IILEmitter Const(decimal value);
        IILEmitter Const(string value);
        IILEmitter Const(object value, TypeSig type);
        IILEmitter Const(object value, Type type);
        IILEmitter Const(long value, int bytes, bool signed);
        void NumericConversion(Type typeFrom, Type typeTo, bool isChecked);
        void NumericConversion(ElementType typeFrom, ElementType typeTo, bool isChecked);

        IILEmitter Call(IMethod method);
        IILEmitter Call(MethodInfo method);
        IILEmitter Callvirt(IMethod method);
        IILEmitter Callvirt(MethodInfo method);

        IILEmitter Newobj(IMethod ctor);
        IILEmitter Newobj(ConstructorInfo ctor);

        IILEmitter Initobj(ITypeDefOrRef type);
        IILEmitter Initobj(Type ctor);

        void Replace(CilBody body);
    }
}