using System;
using System.IO;

namespace Netfuser.Core.Embedder.Compression
{
    /// <summary>
    /// Base interface for plugins able to compress and de-compress resources
    /// </summary>
    public interface ICompression : INamedPlugin
    {
        /// <summary>
        /// Type to inject in the target module that contains de-compression code and implements <see cref="IDecompressor"/> interface
        /// </summary>
        Type RuntimeDecompressorType { get; }
        
        /// <summary>
        /// Compress data stream
        /// </summary>
        /// <param name="input">input stream</param>
        /// <param name="output">output stream</param>
        /// <returns>result of the compression</returns>
        CompressionResult Compress(Stream input, Stream output);
    }
}