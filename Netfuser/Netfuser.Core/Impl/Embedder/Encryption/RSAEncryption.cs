using Base.IO;
using Netfuser.Core.Embedder.Encryption;
using Netfuser.Runtime.Embedder.Encryption;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Netfuser.Core.Impl.Embedder.Encryption
{
    /// <summary>
    /// This class is more an example of how encryption of resources can be performed.
    /// It doesn't really make sense to use it as is, because it stores private keys 
    /// along with the encrypted data.
    /// Encryption of resources makes sense when the actual decryption is performed at 
    /// the secure location (i.e. hadware device, remote server etc)
    /// </summary>
    public class RSAEncryption : AbstractPlugin, IEncryption
    {
        private int _bits;
        private RSAParameters _private, _public;
        public string Name { get; }

        public RSAEncryption(IContextImpl context, string name, int bits, RSAParameters privkey, RSAParameters pubkey) : base(context)
        {
            Name = name;
            _bits = bits;
            _private = privkey;
            _public = pubkey;
        }

        public Type RuntimeDecryptorType => typeof(RSADecryptor);

        public void Encrypt(Stream input, Stream output)
        {
            var csp = new RSACryptoServiceProvider(_bits);
            csp.ImportParameters(_public);
            csp.ImportParameters(_private);
            var encrypted = csp.Encrypt(input.ReadAllBytes(), false);
            var key = csp.ExportCspBlob(true);
            output.Write(BitConverter.GetBytes(_bits));
            output.Write(BitConverter.GetBytes(key.Length));
            output.Write(key);
            output.Write(encrypted);
        }
    }
}
