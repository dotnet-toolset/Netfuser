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

        public CompressionResult(long uncompressedSize, long compressedSize)
        {
            UncompressedSize = uncompressedSize;
            CompressedSize = compressedSize;
        }
    }
}