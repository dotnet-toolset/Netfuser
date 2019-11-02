using Base.Rng;

namespace Netfuser.Core.Manglers.Strings
{
    public interface IStringMangler : IPlugin
    {
        IRng Rng { get; }
    }
}
