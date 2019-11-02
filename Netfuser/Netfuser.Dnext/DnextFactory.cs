using System;
using System.Collections.Generic;
using Base.Lang;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Netfuser.Dnext.Cil;
using Netfuser.Dnext.Hierarchy;
using Netfuser.Dnext.Impl;
using Netfuser.Dnext.Impl.Cil;
using Netfuser.Dnext.Impl.Inheritance;

namespace Netfuser.Dnext
{
    public static class DnextFactory
    {
        /// <summary>
        /// Analyze method body and split it into blocks that correspond to the boundaries of exception handlers
        /// Code then may be rearranged, added or removed within each block, while preserving exception handling logic.
        /// Blocks may form a tree to reflect nested exception handlers.
        /// </summary>
        /// <param name="instructions">list of instructions</param>
        /// <param name="handlers">list of exception handlers</param>
        /// <returns><see cref="Block.Root"/></returns>
        public static Block.Root ParseBlocks(IEnumerable<Instruction> instructions,
            IEnumerable<ExceptionHandler> handlers) => new BlockParser(instructions, handlers).Parse();

        public static IVTables NewVTables() => new VTables();

        public static IILEmitter NewILEmitter(ModuleDef targetModule, Importer? importer = null,
            CilFragment fragment = null) =>
            new ILEmitter(targetModule, importer, fragment);

        public static ITypeKey NewTypeKey(IScope scope, string fullName) =>
            TypeKey.Create(scope, fullName);

        public static ITypeKey NewTypeKey(ModuleDef module, Resource resource) =>
            TypeKey.Create(module, "global::resource::" + resource.ResourceType + "::" + resource.Name);

        public static IVChains BuildVNameChains(IVTables vtables, IEnumerable<TypeDef> types,
            Func<MethodDef, bool> dontRename) =>
            new VChains(vtables, types, dontRename);

        public static ElementType GetIntElementType(int bytes, bool signed)
        {
            switch (bytes)
            {
                case 1:
                    return signed ? ElementType.I1 : ElementType.U1;
                case 2:
                    return signed ? ElementType.I2 : ElementType.U2;
                case 4:
                    return signed ? ElementType.I4 : ElementType.U4;
                case 8:
                    return signed ? ElementType.I8 : ElementType.U8;
                default:
                    throw new CodeBug.Unreachable();
            }
        }

        public static OpCode CondBranch(bool less, bool inclusive, bool signed) =>
            less ? inclusive ? signed ? OpCodes.Ble : OpCodes.Ble_Un :
            signed ? OpCodes.Blt : OpCodes.Blt_Un :
            inclusive ? signed ? OpCodes.Bge : OpCodes.Bge_Un :
            signed ? OpCodes.Bgt : OpCodes.Bgt_Un;

        public static OpCode InverseBranch(OpCode opCode)
        {
            switch (opCode.Code)
            {
                case Code.Bge:
                    return OpCodes.Blt;
                case Code.Bge_Un:
                    return OpCodes.Blt_Un;
                case Code.Blt:
                    return OpCodes.Bge;
                case Code.Blt_Un:
                    return OpCodes.Bge_Un;
                case Code.Bgt:
                    return OpCodes.Ble;
                case Code.Bgt_Un:
                    return OpCodes.Ble_Un;
                case Code.Ble:
                    return OpCodes.Bgt;
                case Code.Ble_Un:
                    return OpCodes.Bgt_Un;
                case Code.Brfalse:
                    return OpCodes.Brtrue;
                case Code.Brtrue:
                    return OpCodes.Brfalse;
                case Code.Beq:
                    return OpCodes.Bne_Un;
                case Code.Bne_Un:
                    return OpCodes.Beq;
            }

            throw new NotSupportedException();
        }
    }
}