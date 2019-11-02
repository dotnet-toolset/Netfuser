using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Base.IO;
using Base.IO.Impl;
using Base.Lang;
using dnlib.IO;
using Netfuser.Core.Embedder.Compression;
using Netfuser.Core.Embedder.Encryption;

namespace Netfuser.Core.Impl.Embedder.Compression
{
    public class CompressEncryptDataReaderFactory : DataReaderFactory
    {
        private readonly IReadable _readable;
        private readonly IEncryption _encryption;
        public override string Filename { get; }
        public override uint Length => (uint)(CompressionState?.Result.CompressedSize ?? 0);

        public readonly State CompressionState;

        public class State : Disposable
        {
            private readonly IReadable _readable;
            public readonly ICompression Compressor;
            public readonly TempStream Temp;
            public CompressionResult Result;

            public bool Acceptable =>
                Result.CompressedSize < Result.UncompressedSize && Result.CompressedSize < uint.MaxValue;

            public State(IReadable readable, ICompression compressor)
            {
                _readable = readable;
                Compressor = compressor;
                Temp = new TempStream();
            }

            protected override void OnDispose()
            {
                Temp.Dispose();
            }

            public void Run()
            {
                using var input = _readable.OpenReader();
                Result = Compressor.Compress(input, Temp);
            }
        }

        public CompressEncryptDataReaderFactory(IReadOnlyList<ICompression> compressors, IEncryption encryption, IReadable readable,
            string filename)
        {
            _readable = readable;
            _encryption = encryption;
            Filename = filename;
            if (compressors.Count > 0)
            {
                var states = compressors.Select(c => new State(readable, c)).ToList();
                Parallel.ForEach(states, s => s.Run());
                CompressionState = states.Where(s => s.Acceptable).OrderBy(s => s.Result.CompressedSize).FirstOrDefault();
                states.ForEach(s =>
                {
                    if (s != CompressionState)
                        s.Dispose();
                });
            }
        }

        public override DataReader CreateReader(uint offset, uint length)
        {
            if (offset != 0) throw new ArgumentException(nameof(offset));
            var readable = CompressionState?.Temp ?? _readable;
            if (_encryption != null) { 
                var encrypted= new TempStream();
                using var raw = readable.OpenReader();
                _encryption.Encrypt(raw, encrypted);
                readable = encrypted;
            }
            var bytes = readable.ReadAllBytes();
            return new DataReader(DataStreamFactory.Create(bytes), offset, (uint)bytes.Length);
        }

        public override void Dispose()
        {
            CompressionState?.Dispose();
        }
    }
}