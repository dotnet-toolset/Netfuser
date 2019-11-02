using System;
using System.IO;

namespace Netfuser.Core.Embedder.Encryption
{
    /// <summary>
    /// Base interface for plugins able to encrypt and de-crypt resources
    /// </summary>
    public interface IEncryption : INamedPlugin
    {
        /// <summary>
        /// Type to inject in the target module that contains de-crypting code and implements <see cref="IDecryptor"/> interface
        /// </summary>
        Type RuntimeDecryptorType { get; }

        /// <summary>
        /// Encrypt data stream
        /// </summary>
        /// <param name="input">input stream</param>
        /// <param name="output">output stream</param>
        void Encrypt(Stream input, Stream output);
    }
}