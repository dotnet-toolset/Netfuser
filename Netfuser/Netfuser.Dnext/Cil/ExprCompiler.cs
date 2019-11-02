using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Base.Lang;
using dnlib.DotNet.Emit;
using Netfuser.Dnext.Impl.Cil;
using OpCode = dnlib.DotNet.Emit.OpCode;
using OpCodes = dnlib.DotNet.Emit.OpCodes;

namespace Netfuser.Dnext.Cil
{
    public class ExprCompiler
    {
        static readonly Dictionary<ExpressionType, OpCode> Expr2Op = new Dictionary<ExpressionType, OpCode>
        {
            [ExpressionType.Add] = OpCodes.Add,
            [ExpressionType.Subtract] = OpCodes.Sub,
            [ExpressionType.Multiply] = OpCodes.Mul,
            [ExpressionType.Divide] = OpCodes.Div,
            [ExpressionType.Or] = OpCodes.Or,
            [ExpressionType.And] = OpCodes.Add,
            [ExpressionType.ExclusiveOr] = OpCodes.Xor,
            [ExpressionType.LeftShift] = OpCodes.Shl,
            [ExpressionType.RightShift] = OpCodes.Shr,
            [ExpressionType.Not] = OpCodes.Not,
            [ExpressionType.Negate] = OpCodes.Neg,
        };

        private readonly IILEmitter _ilg;

        public Func<ParameterExpression, Local> ParameterResolver;

        public ExprCompiler(IILEmitter emitter)
        {
            _ilg = emitter;
        }

        public void Compile(Expression e)
        {
            Emit(e);
        }

        internal void EmitConvertToType(Type typeFrom, Type typeTo, bool isChecked)
        {
            if (TypeUtils.AreEquivalent(typeFrom, typeTo))
                return;

            if (typeFrom == typeof(void) || typeTo == typeof(void))
                throw new CodeBug.Unreachable();

            var flag = typeFrom.IsNullableType();
            var flag2 = typeTo.IsNullableType();
            var nonNullableType = typeFrom.GetNonNullableType();
            var nonNullableType2 = typeTo.GetNonNullableType();
            if (typeFrom.IsInterface || typeTo.IsInterface || typeFrom == typeof(object) || typeTo == typeof(object) ||
                typeFrom == typeof(Enum) || typeFrom == typeof(ValueType) ||
                TypeUtils.IsLegalExplicitVariantDelegateConversion(typeFrom, typeTo))
            {
                EmitCastToType(typeFrom, typeTo);
                return;
            }

            if (flag || flag2)
            {
                EmitNullableConversion(typeFrom, typeTo, isChecked);
                return;
            }

            if ((!TypeUtils.IsConvertible(typeFrom) || !TypeUtils.IsConvertible(typeTo)) &&
                (nonNullableType.IsAssignableFrom(nonNullableType2) ||
                 nonNullableType2.IsAssignableFrom(nonNullableType)))
            {
                EmitCastToType(typeFrom, typeTo);
                return;
            }

            if (typeFrom.IsArray && typeTo.IsArray)
            {
                EmitCastToType(typeFrom, typeTo);
                return;
            }

            _ilg.NumericConversion(typeFrom, typeTo, isChecked);
        }

        private void EmitCastToType(Type typeFrom, Type typeTo)
        {
            if (!typeFrom.IsValueType && typeTo.IsValueType)
            {
                _ilg.Emit(OpCodes.Unbox_Any, _ilg.Importer.Import(typeTo));
                return;
            }

            if (typeFrom.IsValueType && !typeTo.IsValueType)
            {
                _ilg.Emit(OpCodes.Box, _ilg.Importer.Import(typeFrom));
                if (typeTo != typeof(object))
                {
                    _ilg.Emit(OpCodes.Castclass, _ilg.Importer.Import(typeTo));
                    return;
                }

                return;
            }

            if (!typeFrom.IsValueType && !typeTo.IsValueType)
            {
                _ilg.Emit(OpCodes.Castclass, _ilg.Importer.Import(typeTo));
                return;
            }

            throw new NotSupportedException($"{typeFrom} => {typeTo}");
        }


        private void EmitNullableToNullableConversion(Type typeFrom, Type typeTo, bool isChecked)
        {
            using (_ilg.UseTempLocal(typeFrom, out var local))
            using (_ilg.UseTempLocal(typeTo, out var local2))
            {
                _ilg.Stloc(local);
                _ilg.Ldloca(local);
                EmitHasValue(typeFrom);
                var brfs = Instruction.Create(OpCodes.Brfalse_S);
                _ilg.Emit(brfs);
                _ilg.Ldloca(local);
                EmitGetValueOrDefault(typeFrom);
                var nonNullableType = typeFrom.GetNonNullableType();
                var nonNullableType2 = typeTo.GetNonNullableType();
                EmitConvertToType(nonNullableType, nonNullableType2, isChecked);
                var constructor = typeTo.GetConstructor(new[] {nonNullableType2});
                _ilg.Newobj(constructor);
                _ilg.Stloc(local2);
                var brs = Instruction.Create(OpCodes.Br_S);
                _ilg.Emit(brs);
                var lt = Instruction.Create(OpCodes.Ldloca, local2);
                brfs.Operand = lt;
                _ilg.Emit(lt);
                _ilg.Initobj(typeTo);
                var lt2 = Instruction.Create(OpCodes.Ldloc, local2);
                brs.Operand = lt2;
                _ilg.Emit(lt2);
            }
        }

        internal void EmitGetValueOrDefault(Type nullableType)
        {
            var method = nullableType.GetMethod("GetValueOrDefault", Type.EmptyTypes);
            _ilg.Call(method);
        }

        private void EmitNonNullableToNullableConversion(Type typeFrom, Type typeTo, bool isChecked)
        {
            using (_ilg.UseTempLocal(typeFrom, out var local))
            {
                var nonNullableType = typeTo.GetNonNullableType();
                EmitConvertToType(typeFrom, nonNullableType, isChecked);
                var constructor = typeTo.GetConstructor(new[] {nonNullableType});
                _ilg.Newobj(constructor);
                _ilg.Stloc(local);
                _ilg.Ldloc(local);
            }
        }

        private void EmitNullableToNonNullableConversion(Type typeFrom, Type typeTo, bool isChecked)
        {
            if (typeTo.IsValueType)

                EmitNullableToNonNullableStructConversion(typeFrom, typeTo, isChecked);
            else
                EmitNullableToReferenceConversion(typeFrom);
        }

        private void EmitNullableToNonNullableStructConversion(Type typeFrom, Type typeTo, bool isChecked)
        {
            using (_ilg.UseTempLocal(typeFrom, out var local))
            {
                _ilg.Stloc(local);
                _ilg.Ldloca(local);
                EmitGetValue(typeFrom);
                var nonNullableType = typeFrom.GetNonNullableType();
                EmitConvertToType(nonNullableType, typeTo, isChecked);
            }
        }

        internal void EmitHasValue(Type nullableType)
        {
            var method = nullableType.GetMethod("get_HasValue", BindingFlags.Instance | BindingFlags.Public);
            _ilg.Call(method);
        }

        internal void EmitGetValue(Type nullableType)
        {
            var method = nullableType.GetMethod("get_Value", BindingFlags.Instance | BindingFlags.Public);
            _ilg.Call(method);
        }

        private void EmitNullableToReferenceConversion(Type typeFrom)
        {
            _ilg.Emit(OpCodes.Box, _ilg.Importer.Import(typeFrom));
        }

        private void EmitNullableConversion(Type typeFrom, Type typeTo, bool isChecked)
        {
            var flag = typeFrom.IsNullableType();
            var flag2 = typeTo.IsNullableType();
            if (flag && flag2)
            {
                EmitNullableToNullableConversion(typeFrom, typeTo, isChecked);
                return;
            }

            if (flag)
            {
                EmitNullableToNonNullableConversion(typeFrom, typeTo, isChecked);
                return;
            }

            EmitNonNullableToNullableConversion(typeFrom, typeTo, isChecked);
        }

        private void EmitIndexAssignment(BinaryExpression node)
        {
            var indexExpression = (IndexExpression) node.Left;
            Type objectType = null;
            if (indexExpression.Object != null)
            {
                Emit(indexExpression.Object);
                objectType = indexExpression.Object.Type;
            }

            foreach (var argument in indexExpression.Arguments)
                Emit(argument);
            Emit(node.Right);
            _ilg.Stelem(objectType);
        }

        private void EmitAssign(BinaryExpression node)
        {
            switch (node.Left.NodeType)
            {
                case ExpressionType.Index:
                    EmitIndexAssignment(node);
                    break;
/*                case ExpressionType.MemberAccess:
                    EmitMemberAssignment(node);
                    break;
                case ExpressionType.Parameter:
                    EmitVariableAssignment(node);
                    break;*/
                default:
                    throw new NotSupportedException();
            }
        }

        private void Emit(Expression exp)
        {
            OpCode op;
            switch (exp)
            {
                case BinaryExpression be:
                    if (exp.NodeType == ExpressionType.Assign)
                    {
                        EmitAssign(be);
                        break;
                    }

                    Emit(be.Left);
                    Emit(be.Right);
                    switch (exp.NodeType)
                    {
                        case ExpressionType.ArrayIndex:
                            _ilg.Ldelem(be.Left.Type.GetElementType());
                            break;
                        case ExpressionType.RightShift:
                            _ilg.Emit(
                                new Instruction(TypeUtils.IsUnsigned(be.Left.Type) ? OpCodes.Shr_Un : OpCodes.Shr));
                            break;
                        default:
                            if (Expr2Op.TryGetValue(exp.NodeType, out op))
                                _ilg.Emit(new Instruction(op));
                            else
                                throw new NotSupportedException();
                            break;
                    }

                    break;
                case IndexExpression ie:
                    Emit(ie.Object);
                    break;
                case UnaryExpression ue:
                    Emit(ue.Operand);
                    if (Expr2Op.TryGetValue(exp.NodeType, out op))
                        _ilg.Emit(new Instruction(op));
                    else
                        throw new NotSupportedException();
                    break;
                case ConstantExpression ce:
                    _ilg.Const(ce.Value, ce.Type);
                    break;
                case ParameterExpression pe:
                    var local = ParameterResolver?.Invoke(pe);
                    if (local != null)
                        _ilg.Emit(Instruction.Create(OpCodes.Ldloc, local));
                    // else assume parameter is on stack and do nothing
                    break;

                default:
                    throw new NotSupportedException();
            }
        }

        /*void EmitStore(Expression exp, Expression value)
        {
            if (exp is ArrayIndexExpression)
            {
                var arrIndex = (ArrayIndexExpression) exp;
                EmitLoad(arrIndex.Array);
                Emit(Instruction.CreateLdcI4(arrIndex.Index));
                EmitLoad(value);
                Emit(Instruction.Create(OpCodes.Stelem_I4));
            }
            else if (exp is VariableExpression)
            {
                var var = (VariableExpression) exp;
                EmitLoad(value);
                StoreVar(var.Variable);
            }
            else
                throw new NotSupportedException();
        }

        void EmitStatement(Statement statement)
        {
            if (statement is AssignmentStatement)
            {
                var assignment = (AssignmentStatement) statement;
                EmitStore(assignment.Target, assignment.Value);
            }
            else if (statement is LoopStatement)
            {
                var loop = (LoopStatement) statement;
                /*
                 *      ldc.i4  begin
                 *      br      cmp
                 *      ldc.i4  dummy   //hint for dnlib
                 * lop: nop
                 *      ...
                 *      ...
                 *      ldc.i4.1
                 *      add
                 * cmp: dup
                 *      ldc.i4  limit
                 *      blt     lop
                 *      pop
                 */
        /*Instruction lbl = Instruction.Create(OpCodes.Nop);
        Instruction dup = Instruction.Create(OpCodes.Dup);
        Emit(Instruction.CreateLdcI4(loop.Begin));
        Emit(Instruction.Create(OpCodes.Br, dup));
        Emit(Instruction.CreateLdcI4(loop.Begin));
        Emit(lbl);

        foreach (Statement child in loop.Statements)
            EmitStatement(child);

        Emit(Instruction.CreateLdcI4(1));
        Emit(Instruction.Create(OpCodes.Add));
        Emit(dup);
        Emit(Instruction.CreateLdcI4(loop.Limit));
        Emit(Instruction.Create(OpCodes.Blt, lbl));
        Emit(Instruction.Create(OpCodes.Pop));
    }
    else if (statement is StatementBlock)
    {
        foreach (Statement child in ((StatementBlock) statement).Statements)
            EmitStatement(child);
    }
    else
        throw new NotSupportedException();
}*/
    }
}