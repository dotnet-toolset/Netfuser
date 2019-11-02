using dnlib.DotNet;
using Netfuser.Runtime;

namespace Netfuser.Core.FeatureInjector
{
    /// <summary>
    /// Base class for feature injector events
    /// </summary>
    public abstract class FeatureInjectorEvent : NetfuserEvent
    {
        private FeatureInjectorEvent(IContext context)
            : base(context)
        {
        }

        /// <summary>
        /// This event is fired when feature injector has finalized the list of types suitable for injection
        /// </summary>
        public class HaveInjectableTypes : FeatureInjectorEvent
        {
            internal HaveInjectableTypes(IContext context)
                : base(context)
            {
            }
        }

        /// <summary>
        /// This event is fired when feature provider wants to emit a call to the method with injected code.
        /// Observers may set <see cref="Vetoed"/> to <see langword="true"/> to disable this injection and force
        /// feature provider to find other ways 
        /// </summary>
        public class Requested : FeatureInjectorEvent
        {
            /// <summary>
            /// Feature request
            /// </summary>
            public readonly FeatureRequest Request;
            
            /// <summary>
            /// Method that wants to call injected code
            /// </summary>
            public readonly MethodDef RequestingMethod;
            
            /// <summary>
            /// Set to <see langword="true"/> to stop this injection
            /// </summary>
            public bool Vetoed;

            internal Requested(FeatureRequest request, MethodDef method)
                : base(request.Context)
            {
                Request = request;
                RequestingMethod = method;
                Vetoed = method.DeclaringType.Namespace.StartsWith(RuntimeUtils.Namespace);
            }
        }
    }
}