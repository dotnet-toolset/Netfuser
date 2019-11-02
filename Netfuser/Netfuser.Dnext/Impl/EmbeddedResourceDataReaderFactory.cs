using System;
using dnlib.DotNet;
using dnlib.IO;

namespace Netfuser.Dnext.Impl
{
    public class EmbeddedResourceDataReaderFactory : DataReaderFactory
    {
        private readonly EmbeddedResource _resource;

        public override string Filename => _resource.Name;
        public override uint Length => _resource.Length;

        public EmbeddedResourceDataReaderFactory(EmbeddedResource res)
        {
            _resource = res;
        }

        public override DataReader CreateReader(uint offset, uint length)
        {
            if (offset != 0) throw new ArgumentException(nameof(offset));
            if (length != Length) throw new ArgumentException(nameof(length));
            return _resource.CreateReader();
        }

        public override void Dispose()
        {
        }
    }
}