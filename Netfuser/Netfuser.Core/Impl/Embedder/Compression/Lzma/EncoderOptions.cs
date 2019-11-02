namespace Netfuser.Core.Impl.Embedder.Compression.Lzma
{
    enum EMatchFinderType
    {

        BT2,
        BT4,

    };

    class EncoderOptions
    {
        public int DictionarySize = 1 << 23;
        public int PosStateBits = 2;
        public int LitContextBits = 3;
        public int LitPosBits = 0;
        public int Algorithm = 2;
        public int NumFastBytes = 128;
        public EMatchFinderType MatchFinder = EMatchFinderType.BT4;
        public bool EndMarker = false;
    }
}
