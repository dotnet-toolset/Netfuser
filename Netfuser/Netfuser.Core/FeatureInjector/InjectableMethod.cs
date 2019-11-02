using dnlib.DotNet;

namespace Netfuser.Core.FeatureInjector
{
    /// <summary>
    /// This class describes <see cref="MethodDef"/> that is potentially suitable for injection of a specific feature.
    /// </summary>
    public class InjectableMethod
    {
        /// <summary>
        /// Injectable type
        /// </summary>
        public readonly InjectableType Type;
        /// <summary>
        /// Method from the source assembly that may be used for injection
        /// </summary>
        public readonly MethodDef Method;
        /// <summary>
        /// Score of this method - the higher it is, the higher the chances are that it will actually be used for injection
        /// </summary>
        public readonly int Score;

        public InjectableMethod(InjectableType type, MethodDef method, int score)
        {
            Type = type;
            Method = method;
            Score = score;
        }
    }
}