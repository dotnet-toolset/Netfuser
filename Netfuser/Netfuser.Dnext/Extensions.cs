using System;
using System.Collections.Generic;
using System.Linq;
using Base.Lang;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Netfuser.Dnext.Cil;
using Netfuser.Dnext.Cil.Graph;
using Netfuser.Dnext.Impl;
using Netfuser.Dnext.Impl.Cil;

namespace Netfuser.Dnext
{
    public static class Extensions
    {
        public static Type ToType(this ElementType t)
        {
            switch (t)
            {
                case ElementType.I: return typeof(IntPtr);
                case ElementType.U: return typeof(UIntPtr);
                case ElementType.I1: return typeof(sbyte);
                case ElementType.I2: return typeof(short);
                case ElementType.I4: return typeof(int);
                case ElementType.I8: return typeof(long);
                case ElementType.U1: return typeof(byte);
                case ElementType.U2: return typeof(ushort);
                case ElementType.U4: return typeof(uint);
                case ElementType.U8: return typeof(ulong);
                case ElementType.R4: return typeof(float);
                case ElementType.R8: return typeof(double);
                case ElementType.Char: return typeof(char);
                case ElementType.Void: return typeof(void);
                case ElementType.Object: return typeof(object);
                case ElementType.String: return typeof(string);
                case ElementType.Boolean: return typeof(bool);
                default: throw new CodeBug.Unreachable();
            }
        }

        public static TypeCode ToTypeCode(this ElementType t)
        {
            switch (t)
            {
                case ElementType.I1: return TypeCode.SByte;
                case ElementType.I2: return TypeCode.Int16;
                case ElementType.I4: return TypeCode.Int32;
                case ElementType.I8: return TypeCode.Int64;
                case ElementType.U1: return TypeCode.Byte;
                case ElementType.U2: return TypeCode.UInt16;
                case ElementType.U4: return TypeCode.UInt32;
                case ElementType.U8: return TypeCode.UInt64;
                case ElementType.R4: return TypeCode.Single;
                case ElementType.R8: return TypeCode.Double;
                case ElementType.Char: return TypeCode.Char;
                case ElementType.Object: return TypeCode.Object;
                case ElementType.String: return TypeCode.String;
                case ElementType.Boolean: return TypeCode.Boolean;
                default: throw new CodeBug.Unreachable();
            }
        }

        public static ElementType ToElementType(this TypeCode t)
        {
            switch (t)
            {
                case TypeCode.SByte: return ElementType.I1;
                case TypeCode.Int16: return ElementType.I2;
                case TypeCode.Int32: return ElementType.I4;
                case TypeCode.Int64: return ElementType.I8;
                case TypeCode.Byte: return ElementType.U1;
                case TypeCode.UInt16: return ElementType.U2;
                case TypeCode.UInt32: return ElementType.U4;
                case TypeCode.UInt64: return ElementType.U8;
                case TypeCode.Single: return ElementType.R4;
                case TypeCode.Double: return ElementType.R8;
                case TypeCode.Char: return ElementType.Char;
                case TypeCode.Object: return ElementType.Object;
                case TypeCode.String: return ElementType.String;
                case TypeCode.Boolean: return ElementType.Boolean;
                default: throw new CodeBug.Unreachable();
            }
        }

        public static CorLibTypeSig GetCorLibTypeSig(this ICorLibTypes clt, ElementType t)
        {
            switch (t)
            {
                case ElementType.I: return clt.IntPtr;
                case ElementType.U: return clt.UIntPtr;
                case ElementType.I1: return clt.SByte;
                case ElementType.I2: return clt.Int16;
                case ElementType.I4: return clt.Int32;
                case ElementType.I8: return clt.Int64;
                case ElementType.U1: return clt.Byte;
                case ElementType.U2: return clt.UInt16;
                case ElementType.U4: return clt.UInt32;
                case ElementType.U8: return clt.UInt64;
                case ElementType.R4: return clt.Single;
                case ElementType.R8: return clt.Double;
                case ElementType.Char: return clt.Char;
                case ElementType.Void: return clt.Void;
                case ElementType.Object: return clt.Object;
                case ElementType.String: return clt.String;
                case ElementType.Boolean: return clt.Boolean;
                default: throw new CodeBug.Unreachable();
            }
        }

        public static bool IsUnsigned(this ElementType t)
        {
            switch (t)
            {
                case ElementType.Char:
                case ElementType.U1:
                case ElementType.U2:
                case ElementType.U4:
                case ElementType.U8:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsFloatingPoint(this ElementType t)
        {
            switch (t)
            {
                case ElementType.R4:
                case ElementType.R8:
                    return true;
                default:
                    return false;
            }
        }

        public static Block.Root ParseBlocks(this CilBody body) => new BlockParser(body).Parse();

        /// <summary>
        /// Constructs Control Flow Graph from the specified method body.
        /// </summary>
        /// <param name="body">The method body.</param>
        /// <returns>Control Flow Graph for the given method body.</returns>
        public static ICfGraph ParseGraph(this CilBody body) => new CfGraph(body);

        public static ITypeKey CreateKey(this IType type) => TypeKey.Create(type);
        public static ITypeKey CreateKey(this Type type) => TypeKey.Create(type);

        public static IEnumerable<Block.Regular> EnumRegular(this Block scope)
        {
            foreach (var child in scope)
            {
                if (child is Block.Regular r)
                    yield return r;
                else
                    foreach (var block in child.EnumRegular())
                        yield return block;
            }
        }


        public static EmbeddedResource SharedClone(this EmbeddedResource res) => new EmbeddedResource(res.Name,
            new EmbeddedResourceDataReaderFactory(res), 0, res.Length, res.Attributes);

        public static Local RequestTempLocal(this IILEmitter emitter, TypeSig type) => emitter.TempLocals.Request(type);

        public static Local RequestTempLocal(this IILEmitter emitter, Type type) =>
            emitter.TempLocals.Request(emitter.Importer.ImportAsTypeSig(type));

        public static Local RequestTempLocal<T>(this IILEmitter emitter) =>
            emitter.TempLocals.Request(emitter.Importer.ImportAsTypeSig(typeof(T)));

        public static IDisposable UseTempLocal(this IILEmitter emitter, TypeSig type, out Local local) =>
            emitter.TempLocals.Use(type, out local);

        public static IDisposable UseTempLocal(this IILEmitter emitter, Type type, out Local local) =>
            emitter.TempLocals.Use(emitter.Importer.ImportAsTypeSig(type), out local);

        public static IDisposable UseTempLocal<T>(this IILEmitter emitter, out Local local) =>
            emitter.TempLocals.Use(emitter.Importer.ImportAsTypeSig(typeof(T)), out local);

        public static IILEmitter Emit(this IILEmitter emitter, IEnumerable<Instruction> instructions)
        {
            foreach (var i in instructions)
                emitter.Emit(i.Clone());
            return emitter;
        }

        public static IILEmitter Emit(this IILEmitter emitter, OpCode code) => emitter.Emit(Instruction.Create(code));

        public static IILEmitter Emit(this IILEmitter emitter, params OpCode[] codes)
        {
            foreach (var code in codes)
                emitter.Emit(Instruction.Create(code));
            return emitter;
        }

        public static IILEmitter Emit(this IILEmitter emitter, OpCode code, ITypeDefOrRef type) =>
            emitter.Emit(Instruction.Create(code, type));

        public static IILEmitter Emit(this IILEmitter emitter, OpCode code, MemberRef mr) =>
            emitter.Emit(Instruction.Create(code, mr));

        public static IILEmitter Emit(this IILEmitter emitter, OpCode code, IField f) =>
            emitter.Emit(Instruction.Create(code, f));

        public static IILEmitter Emit(this IILEmitter emitter, OpCode code, IMethod m) =>
            emitter.Emit(Instruction.Create(code, m));

        public static IILEmitter Emit(this IILEmitter emitter, OpCode code, ITokenOperand to) =>
            emitter.Emit(Instruction.Create(code, to));

        public static IILEmitter Emit(this IILEmitter emitter, OpCode code, MethodSig ms) =>
            emitter.Emit(Instruction.Create(code, ms));

        public static IILEmitter Emit(this IILEmitter emitter, OpCode code, Parameter p) =>
            emitter.Emit(Instruction.Create(code, p));

        public static IILEmitter Emit(this IILEmitter emitter, OpCode code, Local l) =>
            emitter.Emit(Instruction.Create(code, l));

        public static IILEmitter Emit(this IILEmitter emitter, OpCode code, byte v) =>
            emitter.Emit(Instruction.Create(code, v));

        public static IILEmitter Emit(this IILEmitter emitter, OpCode code, sbyte v) =>
            emitter.Emit(Instruction.Create(code, v));

        public static IILEmitter Emit(this IILEmitter emitter, OpCode code, int v) =>
            emitter.Emit(Instruction.Create(code, v));

        public static IILEmitter Emit(this IILEmitter emitter, OpCode code, long v) =>
            emitter.Emit(Instruction.Create(code, v));

        public static IILEmitter Emit(this IILEmitter emitter, OpCode code, double v) =>
            emitter.Emit(Instruction.Create(code, v));

        public static IILEmitter Emit(this IILEmitter emitter, OpCode code, float v) =>
            emitter.Emit(Instruction.Create(code, v));

        public static IILEmitter Emit(this IILEmitter emitter, OpCode code, string v) =>
            emitter.Emit(Instruction.Create(code, v));

        public static IILEmitter Emit(this IILEmitter emitter, OpCode code, Instruction target) =>
            emitter.Emit(Instruction.Create(code, target));

        public static IILEmitter Switch(this IILEmitter emitter, IList<Instruction> targets) =>
            emitter.Emit(Instruction.Create(OpCodes.Switch, targets));

        public static bool Matches(this LinkedListNode<Instruction> i, params Code[] codes)
        {
            foreach (var c in codes)
            {
                if (i == null) return false;
                if (i.Value.OpCode.Code != c) return false;
                i = i.Next;
            }

            return true;
        }

        public static bool IsLdarg(this Instruction i, int index) => GetLdargIndex(i) == index;

        public static int GetLdargIndex(this Instruction i)
        {
            switch (i.OpCode.Code)
            {
                case Code.Ldarg_0:
                    return 0;
                case Code.Ldarg_1:
                    return 1;
                case Code.Ldarg_2:
                    return 2;
                case Code.Ldarg_3:
                    return 3;
                case Code.Ldarg_S:
                case Code.Ldarg:
                    return Convert.ToInt32(i.Operand);
                default:
                    return -1;
            }
        }

        // The methods below are from https://yck1509.github.io/ConfuserEx/

        /// <summary>
        /// Determines whether the object has the specified custom attribute.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="fullName">The full name of the type of custom attribute.</param>
        /// <returns><c>true</c> if the specified object has custom attribute; otherwise, <c>false</c>.</returns>
        public static bool HasAttribute(this IHasCustomAttribute obj, string fullName) =>
            obj.CustomAttributes.Any(attr => attr.TypeFullName == fullName);

        /// <summary>
        /// Determines whether the specified type is COM import.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns><c>true</c> if specified type is COM import; otherwise, <c>false</c>.</returns>
        public static bool IsComImport(this TypeDef type) =>
            type.IsImport ||
            type.HasAttribute("System.Runtime.InteropServices.ComImportAttribute") ||
            type.HasAttribute("System.Runtime.InteropServices.TypeLibTypeAttribute");
    }
}