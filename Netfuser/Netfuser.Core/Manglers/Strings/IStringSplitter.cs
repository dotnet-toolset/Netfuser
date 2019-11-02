using System.Collections.Generic;

namespace Netfuser.Core.Manglers.Strings
{
    public interface IStringSplitter : IPlugin
    {
        void Split(IReadOnlyDictionary<string, StringPieces> strings);
    }
}
