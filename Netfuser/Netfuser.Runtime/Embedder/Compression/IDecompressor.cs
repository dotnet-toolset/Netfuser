using System.IO;

namespace Netfuser.Runtime.Embedder.Compression
{
    /// <summary>
    /// Resource decompressors MUST implement this interface
    /// </summary>
    public interface IDecompressor
    {
        /// <summary>
        /// Decompress input stream
        /// </summary>
        /// <param name="input">compressed data stream</param>
        /// <returns>decompressed data stream</returns>
        Stream Decompress(Stream input);
    }
}