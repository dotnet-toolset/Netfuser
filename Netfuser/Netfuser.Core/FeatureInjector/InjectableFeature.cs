using dnlib.DotNet;

namespace Netfuser.Core.FeatureInjector
{
    /// <summary>
    /// Base class for all injectable features
    /// </summary>
    public abstract class InjectableFeature
    {
        /// <summary>
        /// This must be implemented by a particular feature provider to rate potentially injectable method
        /// </summary>
        /// <param name="t">injectable type</param>
        /// <param name="m">method candidate for injection</param>
        /// <returns>rated injectable method or <see langword="null"/> if this method cannot be used for injection of this particular feature</returns>
        public abstract InjectableMethod Rate(InjectableType t, MethodDef m);
    }
}