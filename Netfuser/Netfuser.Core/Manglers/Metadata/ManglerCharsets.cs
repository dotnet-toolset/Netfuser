using System.Linq;

namespace Netfuser.Core.Manglers.Metadata
{
    /// <summary>
    /// Different character sets to encode metadata names
    /// </summary>
    public static class ManglerCharsets
    {
        // credits to https://yck1509.github.io/ConfuserEx/

        private static readonly char[] Reserved = ".,*`~<>/! ".ToCharArray();

        public static readonly string Ascii = new string(Enumerable.Range(32, 95).Select(c=>(char)c).Except(Reserved).ToArray());

        public static readonly string Latin = new string(Enumerable.Range(0, 26).SelectMany(ord => new[] { (char)('a' + ord), (char)('A' + ord) }).ToArray());

        public static readonly string NonPrintable = "\u200b\u200c\u200d\u200e\u200f\u202a\u202b\u202c\u202d\u202e\u206a\u206b\u206c\u206d\u206e\u206f";

    }
}
