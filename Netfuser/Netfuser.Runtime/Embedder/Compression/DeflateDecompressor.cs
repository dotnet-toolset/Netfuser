using System.IO;
using System.IO.Compression;

namespace Netfuser.Runtime.Embedder.Compression
{
    public class DeflateDecompressor:IDecompressor
    {
        public Stream Decompress(Stream input)
        {
            return new DeflateStream(input, CompressionMode.Decompress);
        }
    }
}