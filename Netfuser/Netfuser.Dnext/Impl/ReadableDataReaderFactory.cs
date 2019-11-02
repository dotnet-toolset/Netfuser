using System;
using Base.IO;
using dnlib.IO;

namespace Netfuser.Dnext.Impl
{
    public class ReadableDataReaderFactory : DataReaderFactory
    {
        private readonly IReadable _readable;
        public override string Filename { get; }
        public override uint Length => _readable is ILengthAwareReadable r ? (uint) r.Length : 0;

        public ReadableDataReaderFactory(IReadable readable, string filename)
        {
            _readable = readable;
            Filename = filename;
        }

        public override DataReader CreateReader(uint offset, uint length)
        {
            if (offset != 0) throw new ArgumentException(nameof(offset));
            // if (length != Length) throw new ArgumentException(nameof(length));
            var bytes = _readable.ReadAllBytes();
            return new DataReader(DataStreamFactory.Create(bytes), offset, (uint) bytes.Length);
        }

        public override void Dispose()
        {
        }
    }
}