using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Netfuser.Runtime.Embedder.Encryption
{
    public class RSADecryptor : IDecryptor
    {
        public Stream Decrypt(Stream input)
        {
            var buf = new byte[4];
            input.Read(buf, 0, buf.Length);
            var bits = BitConverter.ToInt32(buf, 0);
            input.Read(buf, 0, buf.Length);
            var keylen = BitConverter.ToInt32(buf, 0);
            buf = new byte[keylen];
            input.Read(buf, 0, buf.Length);
            var csp = new RSACryptoServiceProvider(bits);
            csp.ImportCspBlob(buf);
            using (var memIn = new MemoryStream())
            {
                input.CopyTo(memIn);
                buf = csp.Decrypt(memIn.ToArray(), false);
            }
            return new MemoryStream(buf, false);
        }
    }
}
