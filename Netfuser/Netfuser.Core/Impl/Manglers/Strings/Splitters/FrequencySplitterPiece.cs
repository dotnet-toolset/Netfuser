using Netfuser.Core.Manglers.Strings;

namespace Netfuser.Core.Impl.Manglers.Strings.Splitters
{
    class FrequencySplitterPiece : StringPiece
    {
        public int Frequency, Usage;

        public FrequencySplitterPiece(string value) 
            : base(value)
        {
        }
    }
}
