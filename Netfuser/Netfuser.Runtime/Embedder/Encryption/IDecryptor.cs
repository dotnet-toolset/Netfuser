using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Netfuser.Runtime.Embedder.Encryption
{
    /// <summary>
    /// Resource decryptors MUST implement this interface
    /// </summary>
    public interface IDecryptor
    {
        /// <summary>
        /// Decrypt input stream
        /// </summary>
        /// <param name="input">encrypted data stream</param>
        /// <returns>decrypted data stream</returns>
        Stream Decrypt(Stream input);
    }
}
