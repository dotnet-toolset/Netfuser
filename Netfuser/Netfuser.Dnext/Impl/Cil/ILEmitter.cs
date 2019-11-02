using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Base.Lang;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Netfuser.Dnext.Cil;

namespace Netfuser.Dnext.Impl.Cil
{
    class ILEmitter : IILEmitter
    {
        private static ConstructorInfo CtorDecimalInt = typeof(decimal).GetConstructor(new[] {typeof(int)});
        private static ConstructorInfo CtorDecimalLong = typeof(decimal).GetConstructor(new[] {typeof(long)});

        private static ConstructorInfo CtorDecimalBits =
            typeof(decimal).GetConstructor(new[] {typeof(int), typeof(int), typeof(int), typeof(bool), typeof(byte)});

        class Insertion : IDisposable
        {
            private readonly ILEmitter _emitter;
            private readonly Instruction _instruction;
            internal readonly int Replace;
            internal readonly bool Before;
            internal readonly CilFragment Fragment;

            public Insertion(ILEmitter emitter, Instruction i, int replace, bool before)
            {
                _emitter = emitter;
                _instruction = i;
                if (!emitter._insertions.TryGetValue(i, out var l))
                    emitter._insertions.Add(i, l = new List<Insertion>());
                l.Add(this);
                emitter._insertionsStack.Push(this);
                Before = before;
                Replace = replace;
                Fragment = new CilFragment();
            }

            public void Dispose()
            {
                var top = _emitter._insertionsStack.Pop();
                Debug.Assert(top == this); // can't inline top because it won't be called in RELEASE build
                if (Fragment.IsEmpty)
                {
                    if (Replace > 0) throw new CodeBug("empty insertion with replace is not allowed");
                    _emitter._insertions.Remove(_instruction);
                }
            }
        }

        private readonly Importer _importer;
        private readonly CilFragment _mainFragment;
        private readonly TempLocals _tempLocals;
        private readonly Dictionary<Instruction, List<Insertion>> _insertions;
        private readonly Stack<Insertion> _insertionsStack;
        private Queue<Instruction> _replaceQueue;

        public Importer Importer => _importer;
        public CilFragment MainFragment => _mainFragment;

        public CilFragment CurrentFragment =>
            _insertionsStack.Count > 0 ? _insertionsStack.Peek().Fragment : _mainFragment;

        public ITempLocals TempLocals => _tempLocals;
        public ModuleDef TargetModule { get; }

        public ILEmitter(ModuleDef targetModule, Importer? importer = null, CilFragment fragment = null)
        {
            TargetModule = targetModule;
            _importer = importer ?? new Importer(targetModule);
            _mainFragment = fragment ?? new CilFragment();
            _tempLocals = new TempLocals();
            _insertions = new Dictionary<Instruction, List<Insertion>>();
            _insertionsStack = new Stack<Insertion>();
        }

        private Instruction Copy(Instruction target, Instruction source)
        {
            target.OpCode = source.OpCode;
            target.Operand = source.Operand;
            if (source.SequencePoint != null) // unlikely
                target.SequencePoint = source.SequencePoint;
            return target;
        }

        public IILEmitter Emit(Instruction instr)
        {
            if (_replaceQueue != null && _replaceQueue.Count > 0)
                instr = Copy(_replaceQueue.Dequeue(), instr);
            CurrentFragment.Add(instr);
            return this;
        }

        public IILEmitter EmitWithReplace(Instruction instr)
        {
            if (_replaceQueue == null)
                _replaceQueue = new Queue<Instruction>();
            _replaceQueue.Enqueue(instr);
            return this;
        }


        public IDisposable BeginInsertion(Instruction i, int replace = 1, bool before = false) =>
            new Insertion(this, i, replace, before);

        private IEnumerable<Instruction> WithInsertions(IList<Instruction> source)
        {
            var i = 0;
            var c = source.Count;
            while (i < c)
            {
                var instr = source[i++];
                if (_insertions.TryGetValue(instr, out var insl))
                {
                    List<Insertion> after = null;
                    var rc = 0;
                    foreach (var ins in insl)
                        if (ins.Before)
                        {
                            Debug.Assert(ins.Replace == 0);
                            foreach (var v in WithInsertions(ins.Fragment.Instructions))
                                yield return v;
                        }
                        else
                        {
                            if (after == null) after = new List<Insertion>(insl.Count);
                            after.Add(ins);
                            if (ins.Replace > rc) rc = ins.Replace;
                        }

                    if (after != null)
                        if (rc == 0)
                        {
                            yield return instr;
                            foreach (var ins in after)
                            foreach (var v in WithInsertions(ins.Fragment.Instructions))
                                yield return v;
                        }
                        else
                        {
                            yield return Copy(instr, after[0].Fragment.Instructions[0]);
                            if (--rc == 0)
                            {
                                foreach (var v in WithInsertions(after[0].Fragment.Instructions.Skip(1).ToList()))
                                    yield return v;
                                foreach (var ins in after.Skip(1))
                                foreach (var v in WithInsertions(ins.Fragment.Instructions))
                                    yield return v;
                            }
                            else
                                foreach (var ai in WithInsertions(after.SelectMany(ins => ins.Fragment.Instructions)
                                    .Skip(1).ToList()))
                                    if (--rc >= 0)
                                    {
                                        instr = source[i++];
                                        yield return Copy(instr, ai);
                                    }
                                    else
                                        yield return ai;
                        }
                    else yield return instr;

                    Debug.Assert(rc == 0);
                    var removed = _insertions.Remove(instr);
                    Debug.Assert(removed); // don't inline - mind side effect!
                }
                else yield return instr;
            }
        }

        public void Commit()
        {
            Debug.Assert(_insertionsStack.Count == 0);
            if (_insertions.Count == 0) return;
            var list = new List<Instruction>(_mainFragment.Count +
                                             _insertions.Values.SelectMany(v => v)
                                                 .Sum(ins => ins.Fragment.Count - ins.Replace));
            list.AddRange(WithInsertions(_mainFragment.Instructions));

            if (_insertions.Count > 0)
                throw new Exception(
                    $"the following insertion points were not found: {_insertions.Keys.Select(v => v.ToString()).Aggregate((a, s) => a + ", " + s)}");
            _mainFragment.Reset(list);
        }

        public IILEmitter Ldelem(TypeSig type)
        {
            Instruction instr;
            if (!type.IsValueType)
                instr = new Instruction(OpCodes.Ldelem_Ref);
            else
            {
                OpCode opCode = null;
                switch (type.ElementType)
                {
                    case ElementType.Boolean:
                    case ElementType.I1:
                        opCode = OpCodes.Ldelem_I1;
                        break;
                    case ElementType.U1:
                        opCode = OpCodes.Ldelem_U1;
                        break;
                    case ElementType.I2:
                        opCode = OpCodes.Ldelem_I2;
                        break;
                    case ElementType.Char:
                    case ElementType.U2:
                        opCode = OpCodes.Ldelem_U2;
                        break;
                    case ElementType.I4:
                        opCode = OpCodes.Ldelem_I4;
                        break;
                    case ElementType.U4:
                        opCode = OpCodes.Ldelem_U4;
                        break;
                    case ElementType.I8:
                    case ElementType.U8:
                        opCode = OpCodes.Ldelem_I8;
                        break;
                    case ElementType.R4:
                        opCode = OpCodes.Ldelem_R4;
                        break;
                    case ElementType.R8:
                        opCode = OpCodes.Ldelem_R8;
                        break;
                }

                instr = opCode != null ? new Instruction(opCode) : new Instruction(OpCodes.Ldelem, type);
            }

            return Emit(instr);
        }

        public IILEmitter Ldelem(Type type)
        {
            Instruction instr;
            if (!type.IsValueType)
                instr = new Instruction(OpCodes.Ldelem_Ref);
            else
            {
                OpCode opCode = null;
                if (!type.IsEnum)
                    switch (Type.GetTypeCode(type))
                    {
                        case TypeCode.Boolean:
                        case TypeCode.SByte:
                            opCode = OpCodes.Ldelem_I1;
                            break;
                        case TypeCode.Byte:
                            opCode = OpCodes.Ldelem_U1;
                            break;
                        case TypeCode.Int16:
                            opCode = OpCodes.Ldelem_I2;
                            break;
                        case TypeCode.Char:
                        case TypeCode.UInt16:
                            opCode = OpCodes.Ldelem_U2;
                            break;
                        case TypeCode.Int32:
                            opCode = OpCodes.Ldelem_I4;
                            break;
                        case TypeCode.UInt32:
                            opCode = OpCodes.Ldelem_U4;
                            break;
                        case TypeCode.Int64:
                        case TypeCode.UInt64:
                            opCode = OpCodes.Ldelem_I8;
                            break;
                        case TypeCode.Single:
                            opCode = OpCodes.Ldelem_R4;
                            break;
                        case TypeCode.Double:
                            opCode = OpCodes.Ldelem_R8;
                            break;
                    }

                instr = opCode != null
                    ? new Instruction(opCode)
                    : new Instruction(OpCodes.Ldelem, _importer.Import(type));
            }

            return Emit(instr);
        }

        public IILEmitter Stelem(TypeSig type)
        {
            Instruction instr;
            if (!type.IsValueType)
                instr = new Instruction(OpCodes.Stelem_Ref);
            else
            {
                OpCode opCode = null;
                switch (type.ElementType)
                {
                    case ElementType.Boolean:
                    case ElementType.I1:
                    case ElementType.U1:
                        opCode = OpCodes.Stelem_I1;
                        break;
                    case ElementType.I2:
                    case ElementType.Char:
                    case ElementType.U2:
                        opCode = OpCodes.Stelem_I2;
                        break;
                    case ElementType.I4:
                    case ElementType.U4:
                        opCode = OpCodes.Stelem_I4;
                        break;
                    case ElementType.I8:
                    case ElementType.U8:
                        opCode = OpCodes.Stelem_I8;
                        break;
                    case ElementType.R4:
                        opCode = OpCodes.Stelem_R4;
                        break;
                    case ElementType.R8:
                        opCode = OpCodes.Stelem_R8;
                        break;
                }

                instr = opCode != null ? new Instruction(opCode) : new Instruction(OpCodes.Ldelem, type);
            }

            return Emit(instr);
        }

        public IILEmitter Stelem(Type type)
        {
            Instruction instr;
            if (!type.IsValueType)
                instr = new Instruction(OpCodes.Stelem_Ref);
            else
            {
                OpCode opCode = null;
                if (!type.IsEnum)

                    switch (Type.GetTypeCode(type))
                    {
                        case TypeCode.Boolean:
                        case TypeCode.SByte:
                        case TypeCode.Byte:
                            opCode = OpCodes.Stelem_I1;
                            break;
                        case TypeCode.Char:
                        case TypeCode.Int16:
                        case TypeCode.UInt16:
                            opCode = OpCodes.Stelem_I2;
                            break;
                        case TypeCode.Int32:
                        case TypeCode.UInt32:
                            opCode = OpCodes.Stelem_I4;
                            break;
                        case TypeCode.Int64:
                        case TypeCode.UInt64:
                            opCode = OpCodes.Stelem_I8;
                            break;
                        case TypeCode.Single:
                            opCode = OpCodes.Stelem_R4;
                            break;
                        case TypeCode.Double:
                            opCode = OpCodes.Stelem_R8;
                            break;
                    }

                instr = opCode != null
                    ? new Instruction(opCode)
                    : new Instruction(OpCodes.Stelem, _importer.Import(type));
            }

            return Emit(instr);
        }

        public IILEmitter Ldloc(Local v)
        {
            OpCode op;
            var index = v.Index;
            object arg = null;
            switch (index)
            {
                case 0:
                    op = OpCodes.Ldloc_0;
                    break;
                case 1:
                    op = OpCodes.Ldloc_1;
                    break;
                case 2:
                    op = OpCodes.Ldloc_2;
                    break;
                case 3:
                    op = OpCodes.Ldloc_3;
                    break;
                default:
                    if (byte.MinValue <= index && index <= byte.MaxValue)
                        op = OpCodes.Ldloc_S;
                    else
                        op = OpCodes.Ldloc;
                    arg = v;
                    break;
            }

            return Emit(new Instruction(op, arg));
        }

        public IILEmitter Ldloca(Local v)
        {
            OpCode op;
            if (byte.MinValue <= v.Index && v.Index <= byte.MaxValue)
                op = OpCodes.Ldloca_S;
            else
                op = OpCodes.Ldloca;
            return Emit(new Instruction(op, v));
        }

        public IILEmitter Stloc(Local v)
        {
            OpCode op;
            var index = v.Index;
            object arg = null;
            switch (index)
            {
                case 0:
                    op = OpCodes.Stloc_0;
                    break;
                case 1:
                    op = OpCodes.Stloc_1;
                    break;
                case 2:
                    op = OpCodes.Stloc_2;
                    break;
                case 3:
                    op = OpCodes.Stloc_3;
                    break;
                default:
                    if (byte.MinValue <= index && index <= byte.MaxValue)
                        op = OpCodes.Stloc_S;
                    else
                        op = OpCodes.Stloc;
                    arg = v;
                    break;
            }

            return Emit(new Instruction(op, arg));
        }

        public IILEmitter Ldarg(Parameter p)
        {
            OpCode op;
            var index = p.Index;
            object arg = null;
            switch (index)
            {
                case 0:
                    op = OpCodes.Ldarg_0;
                    break;
                case 1:
                    op = OpCodes.Ldarg_1;
                    break;
                case 2:
                    op = OpCodes.Ldarg_2;
                    break;
                case 3:
                    op = OpCodes.Ldarg_3;
                    break;
                default:
                    if (byte.MinValue <= index && index <= byte.MaxValue)
                        op = OpCodes.Ldarg_S;
                    else
                        op = OpCodes.Ldarg;
                    arg = p;
                    break;
            }

            return Emit(new Instruction(op, arg));
        }

        public IILEmitter Ldarga(Parameter p)
        {
            OpCode op;
            if (byte.MinValue <= p.Index && p.Index <= byte.MaxValue)
                op = OpCodes.Ldarg_S;
            else
                op = OpCodes.Ldarg;
            return Emit(new Instruction(op, p));
        }

        public IILEmitter Starg(Parameter p)
        {
            OpCode op;
            if (byte.MinValue <= p.Index && p.Index <= byte.MaxValue)
                op = OpCodes.Starg_S;
            else
                op = OpCodes.Starg;
            return Emit(new Instruction(op, p));
        }


        public IILEmitter Const(bool value) =>
            this.Emit(value ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);

        public IILEmitter Const(int value) =>
            Emit(Instruction.CreateLdcI4(value));

        public IILEmitter Const(byte value) => Const((int) value).Emit(OpCodes.Conv_U1);

        public IILEmitter Const(char value) => Const((int) value).Emit(OpCodes.Conv_U2);

        public IILEmitter Const(sbyte value) => Const((int) value).Emit(OpCodes.Conv_I1);

        public IILEmitter Const(short value) => Const((int) value).Emit(OpCodes.Conv_I2);

        public IILEmitter Const(string value) =>
            this.Emit(Instruction.Create(OpCodes.Ldstr, value ?? throw new ArgumentNullException(nameof(value))));

        public IILEmitter Const(uint value) => Const((int) value).Emit(OpCodes.Conv_U4);

        public IILEmitter Const(ushort value) => Const((int) value).Emit(OpCodes.Conv_U2);

        public IILEmitter Const(long value) => Emit(Instruction.Create(OpCodes.Ldc_I8, value)).Emit(OpCodes.Conv_I8);

        public IILEmitter Const(ulong value) =>
            Emit(Instruction.Create(OpCodes.Ldc_I8, (long) value)).Emit(OpCodes.Conv_U8);

        public IILEmitter Const(float value) =>
            Emit(new Instruction(OpCodes.Ldc_R4, value));

        public IILEmitter Const(double value) =>
            Emit(new Instruction(OpCodes.Ldc_R8, value));

        public IILEmitter Const(decimal value)
        {
            if (decimal.Truncate(value) == value)
            {
                if (-2147483648m <= value && value <= 2147483647m)
                {
                    var value2 = decimal.ToInt32(value);
                    Const(value2);
                    Newobj(CtorDecimalInt);
                }
                else if (new decimal(-9223372036854775808L) <= value && value <= new decimal(9223372036854775807L))
                {
                    var value3 = decimal.ToInt64(value);
                    Const(value3);
                    Newobj(CtorDecimalLong);
                }
                else
                {
                    EmitDecimalBits(value);
                }
            }
            else
            {
                EmitDecimalBits(value);
            }

            return this;
        }

        void EmitDecimalBits(decimal value)
        {
            var bits = decimal.GetBits(value);
            Const(bits[0]);
            Const(bits[1]);
            Const(bits[2]);
            Const((bits[3] & 2147483648u) != 0);
            Const((byte) (bits[3] >> 16));
            Newobj(CtorDecimalBits);
        }

        public IILEmitter Const(object value, TypeSig type)
        {
            switch (type.ElementType)
            {
                case ElementType.Boolean:
                    Const((bool) value);
                    break;
                case ElementType.I1:
                    Const((sbyte) value);
                    break;
                case ElementType.I2:
                    Const((short) value);
                    break;
                case ElementType.I4:
                    Const((int) value);
                    break;
                case ElementType.I8:
                    Const((long) value);
                    break;
                case ElementType.R4:
                    Const((float) value);
                    break;
                case ElementType.R8:
                    Const((double) value);
                    break;
                case ElementType.Char:
                    Const((char) value);
                    break;
                case ElementType.U1:
                    Const((byte) value);
                    break;
                case ElementType.U2:
                    Const((ushort) value);
                    break;
                case ElementType.U4:
                    Const((uint) value);
                    break;
                case ElementType.U8:
                    Const((ulong) value);
                    break;
                case ElementType.String:
                    Const((string) value);
                    break;
                default:
                    throw new NotSupportedException();
            }

            return this;
        }

        public IILEmitter Const(object value, Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                    Const((bool) value);
                    break;
                case TypeCode.SByte:
                    Const((sbyte) value);
                    break;
                case TypeCode.Int16:
                    Const((short) value);
                    break;
                case TypeCode.Int32:
                    Const((int) value);
                    break;
                case TypeCode.Int64:
                    Const((long) value);
                    break;
                case TypeCode.Single:
                    Const((float) value);
                    break;
                case TypeCode.Double:
                    Const((double) value);
                    break;
                case TypeCode.Char:
                    Const((char) value);
                    break;
                case TypeCode.Byte:
                    Const((byte) value);
                    break;
                case TypeCode.UInt16:
                    Const((ushort) value);
                    break;
                case TypeCode.UInt32:
                    Const((uint) value);
                    break;
                case TypeCode.UInt64:
                    Const((ulong) value);
                    break;
                case TypeCode.Decimal:
                    Const((decimal) value);
                    break;
                case TypeCode.String:
                    Const((string) value);
                    break;
                default:
                    throw new CodeBug.Unreachable();
            }

            return this;
        }

        public IILEmitter Const(long value, int Bytes, bool Signed)
        {
            switch (Bytes)
            {
                case 1:
                    return Signed ? Const((sbyte) value) : Const((byte) value);
                case 2:
                    return Signed ? Const((short) value) : Const((ushort) value);
                case 4:
                    return Signed ? Const((int) value) : Const((uint) value);
                case 8:
                    return Signed ? Const(value) : Const((ulong) value);
                default:
                    throw new CodeBug.Unreachable();
            }
        }

        public void NumericConversion(Type typeFrom, Type typeTo, bool isChecked)
        {
            if (typeFrom == typeTo) return;
            var isUnsigned = TypeUtils.IsUnsigned(typeFrom);
            var isReal = TypeUtils.IsFloatingPoint(typeFrom);
            if (typeTo == typeof(float))
            {
                if (isUnsigned)
                    this.Emit(OpCodes.Conv_R_Un);
                this.Emit(OpCodes.Conv_R4);
                return;
            }

            if (typeTo == typeof(double))
            {
                if (isUnsigned)
                    this.Emit(OpCodes.Conv_R_Un);
                this.Emit(OpCodes.Conv_R8);
                return;
            }

            var typeCode = Type.GetTypeCode(typeTo);
            if (isChecked)
            {
                if (isUnsigned)
                {
                    switch (typeCode)
                    {
                        case TypeCode.Char:
                        case TypeCode.UInt16:
                            this.Emit(OpCodes.Conv_Ovf_U2_Un);
                            return;
                        case TypeCode.SByte:
                            this.Emit(OpCodes.Conv_Ovf_I1_Un);
                            return;
                        case TypeCode.Byte:
                            this.Emit(OpCodes.Conv_Ovf_U1_Un);
                            return;
                        case TypeCode.Int16:
                            this.Emit(OpCodes.Conv_Ovf_I2_Un);
                            return;
                        case TypeCode.Int32:
                            this.Emit(OpCodes.Conv_Ovf_I4_Un);
                            return;
                        case TypeCode.UInt32:
                            this.Emit(OpCodes.Conv_Ovf_U4_Un);
                            return;
                        case TypeCode.Int64:
                            this.Emit(OpCodes.Conv_Ovf_I8_Un);
                            return;
                        case TypeCode.UInt64:
                            this.Emit(OpCodes.Conv_Ovf_U8_Un);
                            return;
                        default:
                            throw new NotSupportedException(typeTo.ToString());
                    }
                }

                switch (typeCode)
                {
                    case TypeCode.Char:
                    case TypeCode.UInt16:
                        this.Emit(OpCodes.Conv_Ovf_U2);
                        return;
                    case TypeCode.SByte:
                        this.Emit(OpCodes.Conv_Ovf_I1);
                        return;
                    case TypeCode.Byte:
                        this.Emit(OpCodes.Conv_Ovf_U1);
                        return;
                    case TypeCode.Int16:
                        this.Emit(OpCodes.Conv_Ovf_I2);
                        return;
                    case TypeCode.Int32:
                        this.Emit(OpCodes.Conv_Ovf_I4);
                        return;
                    case TypeCode.UInt32:
                        this.Emit(OpCodes.Conv_Ovf_U4);
                        return;
                    case TypeCode.Int64:
                        this.Emit(OpCodes.Conv_Ovf_I8);
                        return;
                    case TypeCode.UInt64:
                        this.Emit(OpCodes.Conv_Ovf_U8);
                        return;
                    default:
                        throw new NotSupportedException(typeTo.ToString());
                }
            }

            switch (typeCode)
            {
                case TypeCode.Char:
                case TypeCode.UInt16:
                    this.Emit(OpCodes.Conv_U2);
                    return;
                case TypeCode.SByte:
                    this.Emit(OpCodes.Conv_I1);
                    return;
                case TypeCode.Byte:
                    this.Emit(OpCodes.Conv_U1);
                    return;
                case TypeCode.Int16:
                    this.Emit(OpCodes.Conv_I2);
                    return;
                case TypeCode.Int32:
                    this.Emit(OpCodes.Conv_I4);
                    return;
                case TypeCode.UInt32:
                    this.Emit(OpCodes.Conv_U4);
                    return;
                case TypeCode.Int64:
                    this.Emit(isUnsigned ? OpCodes.Conv_U8 : OpCodes.Conv_I8);
                    return;
                case TypeCode.UInt64:
                    if (isUnsigned || isReal)
                        this.Emit(OpCodes.Conv_U8);
                    else
                        this.Emit(OpCodes.Conv_I8);
                    return;
                default:
                    throw new NotSupportedException(typeTo.ToString());
            }
        }

        public void NumericConversion(ElementType typeFrom, ElementType typeTo, bool isChecked)
        {
            if (typeFrom == typeTo) return;
            var isUnsigned = typeFrom.IsUnsigned();
            var isReal = typeFrom.IsFloatingPoint();
            if (typeTo == ElementType.R4)
            {
                if (isUnsigned)
                    this.Emit(OpCodes.Conv_R_Un);
                this.Emit(OpCodes.Conv_R4);
                return;
            }

            if (typeTo == ElementType.R8)
            {
                if (isUnsigned)
                    this.Emit(OpCodes.Conv_R_Un);
                this.Emit(OpCodes.Conv_R8);
                return;
            }

            if (isChecked)
            {
                if (isUnsigned)
                    switch (typeTo)
                    {
                        case ElementType.Char:
                        case ElementType.U2:
                            this.Emit(OpCodes.Conv_Ovf_U2_Un);
                            return;
                        case ElementType.I1:
                            this.Emit(OpCodes.Conv_Ovf_I1_Un);
                            return;
                        case ElementType.U1:
                            this.Emit(OpCodes.Conv_Ovf_U1_Un);
                            return;
                        case ElementType.I2:
                            this.Emit(OpCodes.Conv_Ovf_I2_Un);
                            return;
                        case ElementType.I4:
                            this.Emit(OpCodes.Conv_Ovf_I4_Un);
                            return;
                        case ElementType.U4:
                            this.Emit(OpCodes.Conv_Ovf_U4_Un);
                            return;
                        case ElementType.I8:
                            this.Emit(OpCodes.Conv_Ovf_I8_Un);
                            return;
                        case ElementType.U8:
                            this.Emit(OpCodes.Conv_Ovf_U8_Un);
                            return;
                        default:
                            throw new NotSupportedException(typeTo.ToString());
                    }

                switch (typeTo)
                {
                    case ElementType.Char:
                    case ElementType.U2:
                        this.Emit(OpCodes.Conv_Ovf_U2);
                        return;
                    case ElementType.I1:
                        this.Emit(OpCodes.Conv_Ovf_I1);
                        return;
                    case ElementType.U1:
                        this.Emit(OpCodes.Conv_Ovf_U1);
                        return;
                    case ElementType.I2:
                        this.Emit(OpCodes.Conv_Ovf_I2);
                        return;
                    case ElementType.I4:
                        this.Emit(OpCodes.Conv_Ovf_I4);
                        return;
                    case ElementType.U4:
                        this.Emit(OpCodes.Conv_Ovf_U4);
                        return;
                    case ElementType.I8:
                        this.Emit(OpCodes.Conv_Ovf_I8);
                        return;
                    case ElementType.U8:
                        this.Emit(OpCodes.Conv_Ovf_U8);
                        return;
                    default:
                        throw new NotSupportedException(typeTo.ToString());
                }
            }

            switch (typeTo)
            {
                case ElementType.Char:
                case ElementType.U2:
                    this.Emit(OpCodes.Conv_U2);
                    return;
                case ElementType.I1:
                    this.Emit(OpCodes.Conv_I1);
                    return;
                case ElementType.U1:
                    this.Emit(OpCodes.Conv_U1);
                    return;
                case ElementType.I2:
                    this.Emit(OpCodes.Conv_I2);
                    return;
                case ElementType.I4:
                    this.Emit(OpCodes.Conv_I4);
                    return;
                case ElementType.U4:
                    this.Emit(OpCodes.Conv_U4);
                    return;
                case ElementType.I8:
                    this.Emit(isUnsigned ? OpCodes.Conv_U8 : OpCodes.Conv_I8);
                    return;
                case ElementType.U8:
                    if (isUnsigned || isReal)
                        this.Emit(OpCodes.Conv_U8);
                    else
                        this.Emit(OpCodes.Conv_I8);
                    return;
                default:
                    throw new NotSupportedException(typeTo.ToString());
            }
        }

        public IILEmitter Call(IMethod method) => Emit(Instruction.Create(OpCodes.Call, method));
        public IILEmitter Call(MethodInfo method) => Emit(Instruction.Create(OpCodes.Call, Importer.Import(method)));
        public IILEmitter Callvirt(IMethod method) => Emit(Instruction.Create(OpCodes.Callvirt, method));

        public IILEmitter Callvirt(MethodInfo method) =>
            Emit(Instruction.Create(OpCodes.Callvirt, Importer.Import(method)));

        public IILEmitter Newobj(IMethod ctor) => Emit(Instruction.Create(OpCodes.Newobj, ctor));

        public IILEmitter Newobj(ConstructorInfo ctor) =>
            Emit(Instruction.Create(OpCodes.Newobj, Importer.Import(ctor)));

        public IILEmitter Initobj(ITypeDefOrRef type) => Emit(Instruction.Create(OpCodes.Initobj, type));

        public IILEmitter Initobj(Type type) =>
            Emit(Instruction.Create(OpCodes.Initobj, Importer.Import(type)));

        public void Replace(CilBody body)
        {
            if (_replaceQueue != null && _replaceQueue.Count > 0)
                throw new Exception("replace queue must be empty");
            Commit();
            body.Instructions.Clear();
            TempLocals.Flush(body);
            _mainFragment.Write(body);
        }
    }
}