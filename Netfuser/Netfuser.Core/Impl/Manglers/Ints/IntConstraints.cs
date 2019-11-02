using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;
using Base.Collections;
using Base.Lang;
using Base.Rng;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Netfuser.Dnext;
using Netfuser.Dnext.Cil;

namespace Netfuser.Core.Impl.Manglers.Ints
{
    public class IntConstraints
    {
        public readonly int Bytes;
        public readonly bool Signed;
        private readonly Constraint? _lower;
        private readonly Constraint? _upper;

        readonly struct Constraint
        {
            public readonly long Value;
            public readonly bool Inclusive;

            internal Constraint(long value, bool inclusive)
            {
                Value = value;
                Inclusive = inclusive;
            }

            internal Expression MakeExpr(ParameterExpression p, int bytes, bool signed, bool upper)
            {
                var op = upper ? Inclusive ? ExpressionType.LessThanOrEqual : ExpressionType.LessThan :
                    Inclusive ? ExpressionType.GreaterThanOrEqual : ExpressionType.GreaterThan;
                var c = Base.Lang.Ints.Const(Value, bytes, signed);
                var t = c.Type;
                return Expression.MakeBinary(op, p.Type == t ? (Expression) p : Expression.Convert(p, t),
                    c);
            }
        }

        public ElementType ElementType => DnextFactory.GetIntElementType(Bytes, Signed);

        IntConstraints(int bytes, bool signed, Constraint? lower, Constraint? upper)
        {
            Bytes = bytes;
            Signed = signed;
            _lower = lower;
            _upper = upper;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            if (_lower.HasValue)
                sb.Append(_lower.Value.Inclusive ? '[' : '(').Append(Base.Lang.Ints.ToString(_lower.Value.Value, Bytes, Signed));
            else
                sb.Append("[min");
            sb.Append(",");
            if (_upper.HasValue)
                sb.Append(Base.Lang.Ints.ToString(_upper.Value.Value, Bytes, Signed)).Append(_upper.Value.Inclusive ? ']' : ')');
            else
                sb.Append("max]");
            return sb.ToString();
        }

        long GetInclusiveBoundary(bool upper)
        {
            Constraint? bound = upper ? _upper : _lower;
            switch (ElementType)
            {
                case ElementType.I1:
                {
                    if (!bound.HasValue) return upper ? sbyte.MaxValue : sbyte.MinValue;
                    var v = (sbyte) bound.Value.Value;
                    if (bound.Value.Inclusive) return v;
                    return upper ? v - 1 : v + 1;
                }
                case ElementType.I2:
                {
                    if (!bound.HasValue) return upper ? short.MaxValue : short.MinValue;
                    var v = (short) bound.Value.Value;
                    if (bound.Value.Inclusive) return v;
                    return upper ? v - 1 : v + 1;
                }
                case ElementType.I4:
                {
                    if (!bound.HasValue) return upper ? int.MaxValue : int.MinValue;
                    var v = (int) bound.Value.Value;
                    if (bound.Value.Inclusive) return v;
                    return upper ? v - 1 : v + 1;
                }
                case ElementType.I8:
                {
                    if (!bound.HasValue) return upper ? long.MaxValue : long.MinValue;
                    var v = bound.Value.Value;
                    if (bound.Value.Inclusive) return v;
                    return upper ? v - 1 : v + 1;
                }
                case ElementType.U1:
                {
                    if (!bound.HasValue) return upper ? byte.MaxValue : byte.MinValue;
                    var v = (byte) bound.Value.Value;
                    if (bound.Value.Inclusive) return v;
                    return upper ? v - 1 : v + 1;
                }
                case ElementType.U2:
                {
                    if (!bound.HasValue) return upper ? ushort.MaxValue : ushort.MinValue;
                    var v = (ushort) bound.Value.Value;
                    if (bound.Value.Inclusive) return v;
                    return upper ? v - 1 : v + 1;
                }
                case ElementType.U4:
                {
                    if (!bound.HasValue) return upper ? uint.MaxValue : uint.MinValue;
                    var v = (uint) bound.Value.Value;
                    if (bound.Value.Inclusive) return v;
                    return upper ? v - 1 : v + 1;
                }
                case ElementType.U8:
                {
                    if (!bound.HasValue) return (long) (upper ? ulong.MaxValue : ulong.MinValue);
                    var v = (ulong) bound.Value.Value;
                    if (bound.Value.Inclusive) return (long) v;
                    return (long) (upper ? v - 1 : v + 1);
                }
                default: throw new CodeBug.Unreachable();
            }
        }

        public bool Contains(long value)
        {
            var l = GetInclusiveBoundary(false);
            var u = GetInclusiveBoundary(true);
            if (Signed)
                return value >= l && value <= u;
            return (ulong) value >= (ulong) l && (ulong) value <= (ulong) u;
        }

        public bool Overlaps(IntConstraints other)
        {
            var et = ElementType;
            Debug.Assert(et == other.ElementType, $"{et}!={other.ElementType}");
            var l = GetInclusiveBoundary(false);
            var u = GetInclusiveBoundary(true);
            var ol = other.GetInclusiveBoundary(false);
            var ou = other.GetInclusiveBoundary(true);
            switch (et)
            {
                case ElementType.I1:
                {
                    var x1 = (sbyte) l;
                    var x2 = (sbyte) u;
                    var y1 = (sbyte) ol;
                    var y2 = (sbyte) ou;
                    return x1 <= y2 && y1 <= x2;
                }
                case ElementType.I2:
                {
                    var x1 = (short) l;
                    var x2 = (short) u;
                    var y1 = (short) ol;
                    var y2 = (short) ou;
                    return x1 <= y2 && y1 <= x2;
                }
                case ElementType.I4:
                {
                    var x1 = (int) l;
                    var x2 = (int) u;
                    var y1 = (int) ol;
                    var y2 = (int) ou;
                    return x1 <= y2 && y1 <= x2;
                }
                case ElementType.I8:
                {
                    var x1 = l;
                    var x2 = u;
                    var y1 = ol;
                    var y2 = ou;
                    return x1 <= y2 && y1 <= x2;
                }
                case ElementType.U1:
                {
                    var x1 = (byte) l;
                    var x2 = (byte) u;
                    var y1 = (byte) ol;
                    var y2 = (byte) ou;
                    return x1 <= y2 && y1 <= x2;
                }
                case ElementType.U2:
                {
                    var x1 = (ushort) l;
                    var x2 = (ushort) u;
                    var y1 = (ushort) ol;
                    var y2 = (ushort) ou;
                    return x1 <= y2 && y1 <= x2;
                }
                case ElementType.U4:
                {
                    var x1 = (uint) l;
                    var x2 = (uint) u;
                    var y1 = (uint) ol;
                    var y2 = (uint) ou;
                    return x1 <= y2 && y1 <= x2;
                }
                case ElementType.U8:
                {
                    var x1 = (ulong) l;
                    var x2 = (ulong) u;
                    var y1 = (ulong) ol;
                    var y2 = (ulong) ou;
                    return x1 <= y2 && y1 <= x2;
                }
                default:
                    throw new CodeBug.Unreachable();
            }
        }

        public long MakeValue(IRng rng)
        {
            var upper = _upper?.Value;
            var lower = _lower?.Value;
            if (!(_upper?.Inclusive ?? true))
                upper--;
            if (!(_lower?.Inclusive ?? true))
                lower++;
            long value = 0;
            while (value == 0)
                switch (Bytes)
                {
                    case 1:
                        value = Signed
                            ? (long) rng.NextInt8((sbyte) (lower ?? sbyte.MinValue), (sbyte) (upper ?? sbyte.MaxValue))
                            : rng.NextUInt8((byte) (lower ?? byte.MinValue), (byte) (upper ?? byte.MaxValue));
                        break;
                    case 2:
                        value = Signed
                            ? (long) rng.NextInt16((short) (lower ?? short.MinValue), (short) (upper ?? short.MaxValue))
                            : rng.NextUInt16((ushort) (lower ?? ushort.MinValue), (ushort) (upper ?? ushort.MaxValue));
                        break;
                    case 4:
                        value = Signed
                            ? (long) rng.NextInt32((int) (lower ?? int.MinValue), (int) (upper ?? int.MaxValue))
                            : rng.NextUInt32((uint) (lower ?? uint.MinValue), (uint) (upper ?? uint.MaxValue));
                        break;
                    case 8:
                        value = Signed
                            ? rng.NextInt64(lower ?? long.MinValue, upper ?? long.MaxValue)
                            : (long) rng.NextUInt64((ulong) (lower ?? (long) ulong.MinValue),
                                (ulong) (upper ?? unchecked((long) ulong.MaxValue)));
                        break;
                    default: throw new NotSupportedException();
                }
            return value;
        }

        public Expression MakeExpr(IRng rng, ref ParameterExpression p)
        {
            if (p == null)
                p = Expression.Parameter(Bytes == 8 ? typeof(long) : typeof(int));
            var up = _upper?.MakeExpr(p, Bytes, Signed, true);
            var lo = _lower?.MakeExpr(p, Bytes, Signed, false);
            if (up != null && lo != null)
                return rng.NextBoolean() ? Expression.AndAlso(lo, up) : Expression.AndAlso(up, lo);
            return up ?? lo;
        }

        bool Emit(IILEmitter emitter, ref Local local, bool upper, bool invert, Instruction falseTarget)
        {
            var ic = upper ? _upper : _lower;
            if (!ic.HasValue) return false;
            var c = Base.Lang.Ints.Const(ic.Value.Value, Bytes, Signed);
            var compiler = new ExprCompiler(emitter);
            if (invert)
            {
                if (local == null)
                {
                    local = emitter.RequestTempLocal(c.Type);
                    emitter.Stloc(local);
                }

                compiler.Compile(c);
                emitter.Ldloc(local);
                var op = DnextFactory.CondBranch(upper, !ic.Value.Inclusive, Signed);
                emitter.Emit(op, falseTarget);
            }
            else
            {
                if (local != null)
                    emitter.Ldloc(local);
                compiler.Compile(c);
                var op = DnextFactory.CondBranch(!upper, !ic.Value.Inclusive, Signed);
                emitter.Emit(op, falseTarget);
            }

            return true;
        }

        bool IsZeroTolerant()
        {
            if (_lower.HasValue)
                if (_lower.Value.Value == 0 && _lower.Value.Inclusive)
                    return true;
                else if (!Signed || _lower.Value.Value > 0)
                    return false;
            // if we got here, either there's no lower limit, or it is <0
            if (_upper.HasValue)
                if (_upper.Value.Value == 0 && _upper.Value.Inclusive)
                    return true;
                else if (Signed && _upper.Value.Value < 0)
                    return false;
            // (lower limit is absent or <0) and (upper limit is absent or >0)
            return true;
        }

        public void EmitChecker(IRng rng, IILEmitter emitter, Instruction falseTarget)
        {
            Local local = null;
            try
            {
                var zti = IsZeroTolerant() ? rng.NextInt32(0, 2) : -1;
                if (zti >= 0 || (_upper.HasValue && _lower.HasValue))
                {
                    local = emitter.RequestTempLocal(ElementType.ToType());
                    emitter.Stloc(local);
                }

                var upperFirst = rng.NextBoolean();
                if (zti == 0) EmitNz();
                Emit(emitter, ref local, upperFirst, rng.NextBoolean(), falseTarget);
                if (zti == 1) EmitNz();
                Emit(emitter, ref local, !upperFirst, rng.NextBoolean(), falseTarget);
                if (zti == 2) EmitNz();
            }
            finally
            {
                if (local != null)
                    emitter.TempLocals.Release(local);
            }

            void EmitNz()
            {
                emitter.Ldloc(local);
                if (rng.NextBoolean())
                    emitter.Emit(OpCodes.Brfalse, falseTarget);
                else
                {
                    emitter.Emit(OpCodes.Ldc_I4_0);
                    emitter.NumericConversion(ElementType.I4, ElementType, false);
                    emitter.Emit(OpCodes.Beq, falseTarget);
                }
            }
        }

        public void EmitValue(IILEmitter emitter, IRng rng)
        {
            var value = MakeValue(rng);
            emitter.Const(value, Bytes, Signed);
        }

        public static readonly IReadOnlyList<ElementType> SupportedElementTypes = new[]
        {
            ElementType.I1,
            ElementType.I2,
            ElementType.I4,
            ElementType.I8,
            ElementType.U1,
            ElementType.U2,
            ElementType.U4,
            ElementType.U8,
        };

        private static readonly IReadOnlySet<long> EdgeCases = new HashSet<long>
        {
            byte.MinValue, byte.MaxValue, sbyte.MinValue, sbyte.MaxValue,
            short.MinValue, short.MaxValue, ushort.MinValue, ushort.MaxValue,
            int.MinValue, int.MaxValue, uint.MinValue, uint.MaxValue,
            long.MinValue, long.MaxValue, (long) ulong.MinValue, unchecked((long) ulong.MaxValue)
        }.AsReadOnlySet();

        public static Tuple<bool, int> GetProps(ElementType et)
        {
            switch (et)
            {
                case ElementType.I1: return Tuple.Create(true, 1);
                case ElementType.I2: return Tuple.Create(true, 2);
                case ElementType.I4: return Tuple.Create(true, 4);
                case ElementType.I8: return Tuple.Create(true, 8);
                case ElementType.U1: return Tuple.Create(false, 1);
                case ElementType.U2: return Tuple.Create(false, 2);
                case ElementType.U4: return Tuple.Create(false, 4);
                case ElementType.U8: return Tuple.Create(false, 8);
                default: throw new CodeBug.Unreachable();
            }
        }

        public static long NextInt(IRng rng, int bytes, bool signed)
        {
            long value;
            do
            {
                switch (bytes)
                {
                    case 1:
                        value = signed ? (long) rng.NextInt8() : rng.NextUInt8();
                        break;
                    case 2:
                        value = signed ? (long) rng.NextInt16() : rng.NextUInt16();
                        break;
                    case 4:
                        value = signed ? (long) rng.NextInt32() : rng.NextUInt32();
                        break;
                    case 8:
                        value = signed ? rng.NextInt64() : (long) rng.NextUInt64();
                        break;
                    default: throw new NotSupportedException();
                }
            } while (EdgeCases.Contains(value));

            return value;
        }

        public static IntConstraints Generate(IRng rng, int bytes = 0, bool? s = null)
        {
            if (bytes == 0)
                bytes = Base.Lang.Ints.Sizes.RandomElementOrDefault(rng);
            Constraint? upper, lower;
            bool signed;
            do
            {
                signed = s ?? rng.NextBoolean();
                long value;
                if (rng.NextBoolean())
                {
                    value = NextInt(rng, bytes, signed);
                    upper = new Constraint(value, rng.NextBoolean());
                }
                else upper = null;

                if (!upper.HasValue || rng.NextBoolean())
                {
                    value = NextInt(rng, bytes, signed);
                    lower = new Constraint(value, rng.NextBoolean());
                }
                else lower = null;
            } while (!IsSane());

            return new IntConstraints(bytes, signed, lower, upper);

            bool IsSane()
            {
                if (!lower.HasValue || !upper.HasValue) return true;
                if (lower.Value.Value == 0 || upper.Value.Value == 0) return false;
                if (signed)
                {
                    var av = lower.Value.Value;
                    var bv = upper.Value.Value;
                    if (av >= bv) return false;
                    var d = bv - av;
                    return (!lower.Value.Inclusive || !upper.Value.Inclusive) ? d > 1 : d > 0;
                }
                else
                {
                    var av = (ulong) lower.Value.Value;
                    var bv = (ulong) upper.Value.Value;
                    if (av >= bv) return false;
                    var d = bv - av;
                    return (!lower.Value.Inclusive || !upper.Value.Inclusive) ? d > 1 : d > 0;
                }
            }
        }
    }
}