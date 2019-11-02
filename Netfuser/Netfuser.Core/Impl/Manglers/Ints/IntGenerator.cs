using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Base.Rng;
using Netfuser.Core.Impl.Manglers.Values;

namespace Netfuser.Core.Impl.Manglers.Ints
{
    public class IntGenerator
    {
        private readonly IRng _rng;
        private readonly int _maxNodes;
        public readonly ParameterExpression Argument;

        struct Pair
        {
            public readonly Expression C;
            public readonly ExpressionType T;

            public Pair(Expression c, ExpressionType t)
            {
                C = c;
                T = t;
            }
        }

        public IntGenerator(IRng rng, int maxNodes, ParameterExpression arg = null)
        {
            _rng = rng;
            _maxNodes = maxNodes;
            Argument = arg ?? Expression.Variable(typeof(int));
        }


        public ExprCodec Generate()
        {
            Expression enc = Argument;
            var c = _rng.NextInt32(_maxNodes / 2, _maxNodes);
            var stack = new Stack<Pair>();
            int last = -1;
            while (c > 0)
            {
                var m = (int)_rng.NextUInt32(5);
                if (m == last) continue;
                var rc = _rng.NextInt32();
                if (m == 4) rc |= 1;
                var rv = m > 1 ? Expression.Constant(rc) : null;
                switch (m)
                {
                    case 0:
                        enc = Expression.Negate(enc);
                        stack.Push(new Pair(rv, ExpressionType.Negate));
                        break;
                    case 1:
                        enc = Expression.Not(enc);
                        stack.Push(new Pair(rv, ExpressionType.Not));
                        break;
                    case 2:
                        if (last == 3) continue; // avoid simplifiable cases such as (x-2+3)
                        enc = Expression.Add(enc, rv);
                        stack.Push(new Pair(rv, ExpressionType.Subtract));
                        break;
                    case 3:
                        if (last == 2) continue; // avoid simplifiable cases such as (x+2-3)
                        enc = Expression.Subtract(enc, rv);
                        stack.Push(new Pair(rv, ExpressionType.Add));
                        break;
                    case 4:
                        enc = Expression.Multiply(enc, rv);
                        stack.Push(new Pair(Expression.Constant((int) ModInv((uint) rc)), ExpressionType.Multiply));
                        break;
                    case 5:
                        enc = Expression.ExclusiveOr(enc, rv);
                        stack.Push(new Pair(rv, ExpressionType.ExclusiveOr));
                        break;
                    default: throw new NotSupportedException();
                }

                c--;
                last = m;
            }

            Expression dec = Argument;
            while (stack.Count > 0)
            {
                var p = stack.Pop();
                if (p.C == null)
                    dec = Expression.MakeUnary(p.T, dec, Argument.Type);
                else
                    dec = Expression.MakeBinary(p.T, dec, p.C);
            }

            return new ExprCodec(enc, Argument, dec, Argument);
        }

        static ulong ModInv(ulong num, ulong mod)
        {
            ulong a = mod, b = num % mod;
            ulong p0 = 0, p1 = 1;
            while (b != 0)
            {
                if (b == 1) return p1;
                p0 += a / b * p1;
                a = a % b;

                if (a == 0) break;
                if (a == 1) return mod - p0;

                p1 += b / a * p0;
                b = b % a;
            }

            return 0;
        }

        static uint ModInv(uint num) => (uint) ModInv(num, 0x100000000);
    }
}