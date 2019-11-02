using Base.Rng;

namespace Netfuser.Core.Manglers.ControlFlow
{
    /// <summary>
    /// Control flow mangler interface.
    /// See <see cref="Extensions.MangleControlFlow"/>
    /// </summary>
    public interface ICFMangler : IPlugin
    {
        /// <summary>
        /// Control flow mangler options
        /// </summary>
        CFMangleOptions Options { get; }
        
        /// <summary>
        /// Pseudo random number generator for this mangler
        /// </summary>
        IRng Rng { get; }
    }
}