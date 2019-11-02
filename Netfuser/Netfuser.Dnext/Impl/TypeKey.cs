using System;
using Base.Collections;
using Base.Collections.Impl;
using dnlib.DotNet;

namespace Netfuser.Dnext.Impl
{
    class TypeKey : ITypeKey, IEquatable<TypeKey>
    {
        private readonly string _scopeName;
        private readonly string _fullName;
        private readonly IScope _scope;

        public IScope Scope => _scope;
        public string ScopeName => _scopeName;
        public string FullName => _fullName;

        private TypeKey(IScope scope, string fullName)
        {
            _scope = scope;
            _fullName = fullName;
            switch (scope)
            {
                case null:
                    // possible for injected resources
                    break;
                case AssemblyRef ar:
                    _scopeName = ar.Name;
                    break;
                case ModuleDef md:
                    _scopeName = md.Assembly.Name;
                    break;
                default:
                    throw new NotSupportedException("unsupported scope: " + scope);
            }
        }

        public override int GetHashCode()
        {
            return (_scopeName?.GetHashCode() ?? 0) + _fullName.GetHashCode();
        }

        public bool Equals(TypeKey p)
        {
            return p._scopeName == _scopeName && p._fullName == _fullName;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            return obj is TypeKey other && other._scopeName == _scopeName && other._fullName == _fullName;
        }

        public override string ToString()
        {
            return "[" + _scopeName + "]" + _fullName;
        }

        /// <summary>
        /// Computing full name of module, assembly and type may be somewhat expensive. Hence the cache.
        /// </summary>
        static readonly ICache<IType, TypeKey> Cache = new WeakCache<IType, TypeKey>();

        public static TypeKey Create(IType reference)
        {
            return Cache.Get(reference, r =>
            {
                if (r is TypeSpec ts)
                    r = ts.ScopeType;
                return new TypeKey(r.Scope, r.FullName);
            });
        }

        public static TypeKey Create(IScope scope, string fullName)
        {
            return new TypeKey(scope, fullName);
        }

        public static TypeKey Create(Type type)
        {
            return new TypeKey(new AssemblyRefUser(type.Assembly.GetName()), type.FullName);
        }
    }
}