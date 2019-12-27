namespace Netfuser.Core.Embedder.Compression
{
    /// <summary>
    /// Result of the compression operation
    /// </summary>
    public class CompressionResult
    {
        /// <summary>
        /// Sise of the original data (in bytes)
        /// </summary>
        public readonly long UncompressedSize;
        /// <summary>
        /// Sise of the compressed data (in bytes)
        /// </summary>
        public readonly long CompressedSize;
        
        /// <summary>
        /// Constructor for the result of compression operation
        /// </summary>
        /// <param name="uncompressedSize">original size of the uncompressed blob</param>
        /// <param name="compressedSize">size of the compressed blob</param>
        public CompressionResult(long uncompressedSize, long compressedSize)
        {
            UncompressedSize = uncompressedSize;
            CompressedSize = compressedSize;
        }
    }
}