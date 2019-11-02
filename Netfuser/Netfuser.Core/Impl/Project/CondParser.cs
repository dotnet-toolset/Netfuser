using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Base.Cil;
using Base.Collections;
using Base.Text.Impl;
using Netfuser.Core.Project;

namespace Netfuser.Core.Impl.Project
{
    public class CondParser
    {
        internal static readonly ParameterExpression ContextParameter = Expression.Parameter(typeof(IProject), "project");
        internal static readonly MethodInfo InvokeMethod = Marked.GetMember<ProjectLoader.Project, MethodInfo>(1);

        private readonly CondTokenizer _tokenizer;

        public CondParser(string text)
        {
            _tokenizer = new CondTokenizer(new StringBuffer(text));
        }

        CondToken ExpectCurrent(CondToken expected)
        {
            var result = _tokenizer.Token;
            if (!_tokenizer.Matches(expected))
                throw new Exception("unexpected token: " + expected);
            return result;
        }

        private Expression PrimaryExpression()
        {
            switch (_tokenizer.Token)
            {
                case CondToken.Identifier:
                    var n = _tokenizer.Value;
                    _tokenizer.Next();
                    return Expression.Constant(new Ident((string)n));
                case CondToken.Number:
                     n = _tokenizer.Value;
                    _tokenizer.Next();
                    switch (n)
                    {
                        case int i:
                            return Expression.Constant(i);
                        case double d:
                            return Expression.Constant(d);
                        case uint u:
                            return Expression.Constant(u);
                        case long l:
                            return Expression.Constant(l);
                    }

                    return Expression.Constant(Convert.ToDouble(n));
                case CondToken.String:
                    _tokenizer.Next();
                    return Expression.Constant(((string) _tokenizer.Value));
            }

            throw new Exception("Unexpected");
        }

        private Expression PostfixExpression()
        {
            var n = PrimaryExpression();
            while (_tokenizer.Token != CondToken.Eof)
            {
                switch (_tokenizer.Token)
                {
                    case CondToken.Lpar:
                        _tokenizer.Next();
                        IReadOnlyList<Expression> list;
                        if (_tokenizer.Matches(CondToken.Rpar))
                            list = Empty.List<Expression>();
                        else
                        {
                            list = ExpressionList();
                            ExpectCurrent(CondToken.Rpar);
                        }

                        n = Expression.Call(InvokeMethod, ContextParameter, n,
                            Expression.NewArrayInit(typeof(object), list));
                        continue;
                }

                break;
            }

            return n;
        }

        private Expression UnaryExpression()
        {
            var t = _tokenizer.Token;
            switch (t)
            {
                case CondToken.Not:
                    _tokenizer.Next();
                    return Expression.Not(UnaryExpression());
            }

            return PostfixExpression();
        }

        private Expression RelationalExpression()
        {
            var n = UnaryExpression();
            while (_tokenizer.Token != CondToken.Eof)
            {
                ExpressionType? type = null;
                switch (_tokenizer.Token)
                {
                    case CondToken.Lower:
                        type = ExpressionType.LessThan;
                        break;
                    case CondToken.LowerEqual:
                        type = ExpressionType.LessThanOrEqual;
                        break;
                    case CondToken.Greater:
                        type = ExpressionType.GreaterThan;
                        break;
                    case CondToken.GreaterEqual:
                        type = ExpressionType.GreaterThanOrEqual;
                        break;
                }

                if (!type.HasValue) break;
                _tokenizer.Next();
                n = Expression.MakeBinary(type.Value, n, UnaryExpression());
            }

            return n;
        }

        private Expression EqualityExpression()
        {
            var n = RelationalExpression();
            while (_tokenizer.Token != CondToken.Eof)
            {
                ExpressionType? type = null;
                switch (_tokenizer.Token)
                {
                    case CondToken.Equal:
                        type = ExpressionType.Equal;
                        break;
                    case CondToken.NotEqual:
                        type = ExpressionType.NotEqual;
                        break;
                }

                if (!type.HasValue) break;
                _tokenizer.Next();
                n = Expression.MakeBinary(type.Value, n, RelationalExpression());
            }

            return n;
        }

        private IReadOnlyList<Expression> ExpressionList()
        {
            var expressions = new List<Expression> { EqualityExpression() };
            while (_tokenizer.Matches(CondToken.Comma))
                expressions.Add(EqualityExpression());
            return expressions;
        }

        public Func<IProject, object> Parse()
        {
            _tokenizer.Next();
            var expr=EqualityExpression();
            return Expression.Lambda<Func<IProject, object>>(Expression.Convert(expr, typeof(object)), ContextParameter)
                .Compile();
        }
    }
}