using Base.IO;
using Base.Rng;
using dnlib.DotNet;
using dnlib.IO;
using Netfuser.Core.Embedder.Compression;
using Netfuser.Core.Embedder.Encryption;
using Netfuser.Core.Impl;
using Netfuser.Core.Impl.Embedder.Compression;
using Netfuser.Core.Rng;
using Netfuser.Dnext.Impl;
using Netfuser.Runtime.Embedder;
using System.Collections.Generic;

namespace Netfuser.Core.Embedder
{
    /// <summary>
    /// This class describes an entry in the named resource bundle, the actual stream of bytes 
    /// (may be compressed and/or encrypted) that is stored in the targed module.
    /// </summary>
    public class Embedding
    {

        private string _uniqueName;

        public readonly IContextImpl Context;
        /// <summary>
        /// Name of the resource entry
        /// </summary>
        public readonly string Name;
        /// <summary>
        /// Raw bytes
        /// </summary>
        public readonly IReadable Readable;
        /// <summary>
        /// Properties of this resource entry (see <see cref="ResourceEntry"/> for details)
        /// </summary>
        public readonly Dictionary<string, string> Properties;

        /// <summary>
        /// The .NET resource corresponding to this resource entry
        /// </summary>
        public EmbeddedResource Resource { get; private set; }
        /// <summary>
        /// Compression algorithm used to compress this resource (or <see langword="null"/>) if no compression was used
        /// </summary>
        public ICompression Compression { get; private set; }
        /// <summary>
        /// Encryption algorithm used to encrypt this resource (or <see langword="null"/>) if no encryption was used
        /// </summary>
        public IEncryption Encryption { get; private set; }


        internal Embedding(IContextImpl context, string name, IReadable readable)
        {
            Name = name;
            Context = context;
            Readable = readable;
            Properties = new Dictionary<string, string>();
        }

        internal void Initialize()
        {
            var compressors = Context.Fire(new EmbedderEvent.SelectCompression(Context, this, Context.Plugins<ICompression>())).Compressions;
            Encryption = Context.Fire(new EmbedderEvent.Encrypt(Context, this, Context.Plugins<IEncryption>().RandomElementOrDefault(Context.Plugin<IRngPlugin>().Get(NetfuserFactory.EmbedderName)))).Encryption;
            var drf = new CompressEncryptDataReaderFactory(compressors, Encryption, Readable, _uniqueName);
            var compression = Context.Fire(new EmbedderEvent.Compress(Context, this) { Compression = drf.CompressionState.Compressor }).Compression;
            if (compression != drf.CompressionState.Compressor)
            {
                drf.Dispose();
                drf = new CompressEncryptDataReaderFactory(new[] { compression }, Encryption, Readable, _uniqueName);
            }
            Compression = drf.CompressionState.Compressor;
            Resource = new EmbeddedResource(_uniqueName, drf, 0, 0);
        }

        /// <summary>
        /// We can't rely on the Context's dedupe mechanism because it expects that the name is still unique within a [source] module. 
        /// Here we can have duplicate names within a module, for example for RID-tagged native assemblies or native libs
        /// </summary>
        /// <param name="uniqueNames"></param>
        internal void CreateUniqueName(HashSet<string> uniqueNames)
        {
            var baseName = Name;
            if (Properties.TryGetValue(ResourceEntry.KeyRid, out var rid))
                baseName +=$"_{rid}";
            int i = 0;
            _uniqueName = baseName;
            while (uniqueNames.Contains(_uniqueName))
                _uniqueName = baseName + $"_{i++}";
            uniqueNames.Add(_uniqueName);
        }
    }
}