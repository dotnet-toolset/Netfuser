using Base.Rng;

namespace Netfuser.Core.Rng
{
    /// <summary>
    /// Pseudo-random number generator plugin
    /// </summary>
    public interface IRngPlugin: IPlugin
    {
        /// <summary>
        /// Gets or creates pseudo-random number generator for a given name.
        /// Only one PRNG with the given name may exist in the context.
        /// </summary>
        /// <param name="name">name of the PRNG</param>
        /// <returns>pseudo-random number generator</returns>
        IRng Get(string name);
    }
}
