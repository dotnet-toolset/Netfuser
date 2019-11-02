using System;
using System.IO;
using System.IO.Compression;
using System.Reactive.Linq;
using Base.IO;
using Base.IO.Impl;
using Netfuser.Core.Embedder.Compression;
using Netfuser.Runtime.Embedder.Compression;

namespace Netfuser.Core.Impl.Embedder.Compression
{
    class DeflateCompression : AbstractPlugin, ICompression
    {
        public string Name => "deflate";

        public DeflateCompression(IContextImpl context)
            : base(context)
        {
        }

        public Type RuntimeDecompressorType => typeof(DeflateDecompressor);

        public CompressionResult Compress(Stream input, Stream output)
        {
            long bytesRead = 0, bytesWritten = 0;
            using (var i = StreamWrapper.From(input, false))
            using (var o = StreamWrapper.From(output, false))
            using (i.OfType<StreamEvent.AfterRead>().Subscribe(r => { bytesRead += r.Result; }))
            using (o.OfType<StreamEvent.BeforeWrite>().Subscribe(r => { bytesWritten += r.Buffer.Count; }))
            using (var c = new DeflateStream(o, CompressionLevel.Optimal))
                i.CopyTo(c);
            return new CompressionResult(bytesRead, bytesWritten);
        }
    }
}