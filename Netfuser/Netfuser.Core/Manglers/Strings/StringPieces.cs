using System.Collections.Generic;
using Netfuser.Dnext;

namespace Netfuser.Core.Manglers.Strings
{
    public class StringPieces
    {
        internal readonly List<InstrRef> References;
        public IReadOnlyList<StringPiece> Pieces { get; private set; }

        internal StringPieces()
        {
            References = new List<InstrRef>();
        }

        public void Set(IReadOnlyList<StringPiece> pieces)
        {
            Pieces = pieces;
        }
    }
}