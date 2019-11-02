using Netfuser.Core.Manglers.Strings;

namespace Netfuser.Core.Impl.Manglers.Strings.Splitters
{
    public class FrequencySplitterOptions : StringSplitterOptions
    {
        public double FrequencyWeight = 0.44;
        public double UsageWeight = 0.55;
        public double LengthWeight = 0.01;
    }
}
