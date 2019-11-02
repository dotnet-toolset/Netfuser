using System;
using System.IO;
using System.Reactive.Linq;
using Base.IO;
using Base.IO.Impl;
using Netfuser.Core.Embedder.Compression;
using Netfuser.Core.Impl.Embedder.Compression.Lzma;
using Netfuser.Runtime.Embedder.Compression;

namespace Netfuser.Core.Impl.Embedder.Compression
{
    class LzmaCompression : AbstractPlugin, ICompression
    {
        public string Name => "lzma";

        public LzmaCompression(IContextImpl context)
            : base(context)
        {
        }

        public Type RuntimeDecompressorType => typeof(LzmaDecompressor);

        public CompressionResult Compress(Stream input, Stream output)
        {
            long bytesRead = 0, bytesWritten = 0;
            using (var i = StreamWrapper.From(input, false))
            using (var o = StreamWrapper.From(output, false))
            using (i.OfType<StreamEvent.AfterRead>().Subscribe(r => { bytesRead += r.Result; }))
            using (o.OfType<StreamEvent.BeforeWrite>().Subscribe(r => { bytesWritten += r.Buffer.Count; })) {

                var encoder = new Encoder(new EncoderOptions());
                encoder.WriteCoderProperties(o);
                long fileSize;
                fileSize = i.Length;
                for (int j = 0; j < 8; j++)
                    o.WriteByte((byte)(fileSize >> (8 * j)));
                encoder.Code(i, o, -1, -1);
            }
            return new CompressionResult(bytesRead, bytesWritten);
        }
    }
}
