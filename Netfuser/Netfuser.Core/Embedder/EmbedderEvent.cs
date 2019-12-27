using Netfuser.Core.Embedder.Compression;
using Netfuser.Core.Embedder.Encryption;
using System;
using System.Collections.Generic;
using System.Text;

namespace Netfuser.Core.Embedder
{
    /// <summary>
    /// Base class for resource embedder events
    /// </summary>
    public abstract class EmbedderEvent : NetfuserEvent
    {
        /// <summary>
        /// Resource to be embedded
        /// </summary>
        public readonly Embedding Embedding;
        private EmbedderEvent(IContext context, Embedding embedding)
            : base(context)
        {
            Embedding = embedding;
        }

        /// <summary>
        /// This event is fired before Netfuser starts selecting the best compression algorithm for a given resource.
        /// Observers may add/remove compression algorithms in the <see cref="Compressions"/> list.
        /// </summary>
        public class SelectCompression : EmbedderEvent
        {
            /// <summary>
            /// Add or remove compressors as needed.
            /// By default, this list contains all registered <see cref="ICompression"/> plugins.
            /// Once the event is observed, Netfuser will run all of the compressors in this list 
            /// on the resorce data and select compressor that yields the smallest result.
            /// If the list is empty, resource will not be compressed.
            /// </summary>
            public readonly List<ICompression> Compressions;
            internal SelectCompression(IContext context, Embedding embedding, IEnumerable<ICompression> compressions) 
                : base(context, embedding)
            {
                Compressions = new List<ICompression>(compressions);
            }
        }

        /// <summary>
        /// This event is fired before the resource is compressed.
        /// Observers may change compression algorithm or set it to <see langword="null"/> to disable compression
        /// </summary>
        public class Compress: EmbedderEvent
        {
            /// <summary>
            /// Pre-selected compression algorithm, change as needed
            /// </summary>
            public ICompression Compression;
            internal Compress(IContext context, Embedding embedding) : base(context, embedding)
            {
            }
        }
        
        /// <summary>
        /// This event is fired before the resource is encrypted.
        /// Observers may change encryption algorithm or set it to <see langword="null"/> to disable encryption
        /// </summary>
        public class Encrypt: EmbedderEvent
        {
            /// <summary>
            /// Pre-selected encryption algorithm, change as needed
            /// </summary>
            public readonly IEncryption Encryption;
            internal Encrypt(IContext context, Embedding embedding, IEncryption encryption)
                : base(context, embedding)
            {
                Encryption = encryption;
            }
        }
    }
}
