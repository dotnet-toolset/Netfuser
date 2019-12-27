using System;
using System.Collections.Generic;
using System.Diagnostics;
using Base.Logging;
using dnlib.DotNet;
using Netfuser.Core.Impl;
using Netfuser.Dnext.Cil;

namespace Netfuser.Core.FeatureInjector
{
    /// <summary>
    /// This provides a mechanism to ensure we don't end up in infinite recursion if injected feature
    /// calls another method with injected feature, which in turn calls the first method 
    /// </summary>
    public class FeatureRequest
    {
        /// <summary>
        /// Netfuser context
        /// </summary>
        public readonly IContextImpl Context;

        /// <summary>
        ///  IL emitter
        /// </summary>
        public readonly IILEmitter Emitter;

        /// <summary>
        /// Methods with injected features up the stack
        /// </summary>
        public readonly Stack<MethodDef> CallStack;

        /// <summary>
        /// Constructs new feature request
        /// </summary>
        /// <param name="context">Netfuser context</param>
        /// <param name="emitter">IL emitter</param>
        public FeatureRequest(IContextImpl context, IILEmitter emitter)
        {
            Context = context;
            Emitter = emitter;
            CallStack = new Stack<MethodDef>();
        }

        /// <summary>
        /// Checks if the given method is on the feature request stack
        /// </summary>
        /// <param name="methodDef">method to check</param>
        /// <returns></returns>
        public bool IsOnStack(MethodDef methodDef)
        {
            return CallStack.Contains(methodDef);
        }

        class U : IDisposable
        {
            private readonly FeatureRequest _request;
            private readonly MethodDef _method;

            public U(FeatureRequest request, MethodDef method)
            {
                _request = request;
                _method = method;
            }

            public void Dispose()
            {
                var popped = _request.CallStack.Pop();
                Debug.Assert(popped == _method);
            }
        }

        /// <summary>
        /// Feature providers call this when they intend to inject a call to another method with injected feature
        /// </summary>
        /// <param name="method">method that requests the injection</param>
        /// <returns><see cref="IDisposable"/> that must be disposed when the injection is complete,
        /// or <see langword="null"/> if request is vetoed and this particular injection must not be done</returns>
        public IDisposable Enter(MethodDef method)
        {
            var ev = Context.Fire(new FeatureInjectorEvent.Requested(this, method));
            if (ev.Vetoed)
            {
                Context.Debug($"vetoed feature request by {method}");
                return null;
            }

            CallStack.Push(method);
            return new U(this, method);
        }
    }
}