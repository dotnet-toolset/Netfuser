using Base.Rng;
using Netfuser.Core.FeatureInjector;

namespace Netfuser.Core.Manglers.Ints
{
    /// <summary>
    /// Int mangler is at the heart of the obfuscators. It mutates integer contants 
    /// in series of arithmetic operations, to disguise original constants.
    /// </summary>
    public interface IIntMangler : IPlugin
    {
        /// <summary>
        /// Random number generator for this plugin
        /// </summary>
        IRng Rng { get; }
        
        /// <summary>
        /// Given the integer value <see cref="value"/>, emits random set of operations 
        /// that leave the original value on stack, but never expose it in the code.
        /// </summary>
        /// <param name="request">Feature request used for this IL generation</param>
        /// <param name="value">original integer value to obfuscate</param>
        void Emit(FeatureRequest request, int value);
    }
}